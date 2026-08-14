namespace Muonroi.Pdf.Enterprise.Quality;

/// <summary>
/// Pure-managed structural similarity (SSIM) scorer for comparing two pre-decoded
/// RGB pixel buffers. Used by the canary quality gate to detect visual regressions
/// between a reference render and a candidate render.
/// </summary>
/// <remarks>
/// Algorithm: Z. Wang, A. C. Bovik, H. R. Sheikh, E. P. Simoncelli,
/// "Image quality assessment: From error visibility to structural similarity,"
/// IEEE Transactions on Image Processing, vol. 13, no. 4, pp. 600–612, Apr. 2004.
/// doi: 10.1109/TIP.2003.819861
///
/// Luminance extraction uses Rec.709 luma coefficients (Y = 0.2126R + 0.7152G + 0.0722B).
/// Sliding 8×8 windows with biased variance estimator (divides by N=64, not N-1).
/// Edge handling: clip — windows that would exceed image bounds are not evaluated
/// (no zero-padding), so the effective sample region shrinks by up to 7 pixels at
/// the right and bottom edges. This matches the MATLAB reference implementation.
///
/// Constants: C1 = (0.01×255)² = 6.5025, C2 = (0.03×255)² = 58.5225 (Wang/Bovik defaults).
///
/// Performance: pure-managed, single-threaded baseline.
/// SIMD (System.Runtime.Intrinsics) and Parallel.For parallelism are deferred to Phase 9.x.
/// </remarks>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "SsimScorer is an internal quality-scoring utility; ArgumentException here validates pixel-buffer dimensions which are structural pre-conditions, not business-logic errors.")]
public static class SsimScorer
{
    private const int WindowSize = 8;
    private const double C1 = 6.5025;    // (0.01 * 255)^2
    private const double C2 = 58.5225;   // (0.03 * 255)^2

    // Rec.709 luma coefficients
    private const double LumaR = 0.2126;
    private const double LumaG = 0.7152;
    private const double LumaB = 0.0722;

    /// <summary>
    /// Computes the mean SSIM score between two interleaved 8-bit RGB buffers.
    /// </summary>
    /// <param name="rgbA">Reference buffer. Interleaved RGB, row-major, stride = <paramref name="width"/> × 3.
    /// Length must equal <paramref name="width"/> × <paramref name="height"/> × 3.</param>
    /// <param name="rgbB">Candidate buffer. Same layout and length as <paramref name="rgbA"/>.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <returns>
    /// Mean SSIM in the range [−1, 1]. Returns 1.0 exactly for identical buffers.
    /// Natural images produce values in [0, 1].
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="rgbA"/> and <paramref name="rgbB"/> have different lengths,
    /// or when either buffer length does not equal <paramref name="width"/> × <paramref name="height"/> × 3.
    /// </exception>
    public static double Compare(
        ReadOnlySpan<byte> rgbA,
        ReadOnlySpan<byte> rgbB,
        int width,
        int height)
    {
        int expectedLength = width * height * 3;

        if (rgbA.Length != expectedLength)
            throw new ArgumentException(
                $"Buffer rgbA length {rgbA.Length} does not match width*height*3 = {expectedLength}.",
                nameof(rgbA));

        if (rgbB.Length != expectedLength)
            throw new ArgumentException(
                $"Buffer rgbB length {rgbB.Length} does not match width*height*3 = {expectedLength}.",
                nameof(rgbB));

        if (rgbA.Length != rgbB.Length)
            throw new ArgumentException(
                $"Buffer lengths differ: rgbA={rgbA.Length}, rgbB={rgbB.Length}.",
                nameof(rgbB));

        double totalSsim = 0.0;
        int windowCount = 0;

        int rowLimit = height - WindowSize;
        int colLimit = width - WindowSize;

        // Handle images smaller than one window: evaluate a single clipped window
        // covering the entire image.
        if (rowLimit < 0 || colLimit < 0)
        {
            int effectiveRows = Math.Min(height, WindowSize);
            int effectiveCols = Math.Min(width, WindowSize);
            return ComputeWindowSsim(rgbA, rgbB, width, 0, 0, effectiveRows, effectiveCols);
        }

        for (int row = 0; row <= rowLimit; row++)
        {
            for (int col = 0; col <= colLimit; col++)
            {
                totalSsim += ComputeWindowSsim(rgbA, rgbB, width, row, col, WindowSize, WindowSize);
                windowCount++;
            }
        }

        return windowCount > 0 ? totalSsim / windowCount : 1.0;
    }

    private static double ComputeWindowSsim(
        ReadOnlySpan<byte> rgbA,
        ReadOnlySpan<byte> rgbB,
        int imageWidth,
        int startRow,
        int startCol,
        int windowRows,
        int windowCols)
    {
        double sumX = 0.0, sumY = 0.0;
        double sumX2 = 0.0, sumY2 = 0.0, sumXY = 0.0;

        for (int wy = 0; wy < windowRows; wy++)
        {
            for (int wx = 0; wx < windowCols; wx++)
            {
                int idx = ((startRow + wy) * imageWidth + (startCol + wx)) * 3;

                double lx = LumaR * rgbA[idx] + LumaG * rgbA[idx + 1] + LumaB * rgbA[idx + 2];
                double ly = LumaR * rgbB[idx] + LumaG * rgbB[idx + 1] + LumaB * rgbB[idx + 2];

                sumX  += lx;
                sumY  += ly;
                sumX2 += lx * lx;
                sumY2 += ly * ly;
                sumXY += lx * ly;
            }
        }

        double n = windowRows * windowCols;
        double muX = sumX / n;
        double muY = sumY / n;

        // Biased variance and covariance (matches Wang/Bovik 2004 — divides by N, not N-1)
        double varX  = sumX2 / n - muX * muX;
        double varY  = sumY2 / n - muY * muY;
        double covXY = sumXY / n - muX * muY;

        double numerator   = (2.0 * muX * muY + C1) * (2.0 * covXY + C2);
        double denominator = (muX * muX + muY * muY + C1) * (varX + varY + C2);

        return numerator / denominator;
    }
}
