namespace Muonroi.Pdf.Tests.Enterprise;

public sealed class SsimScorerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static byte[] MakeRandomBuffer(int width, int height, int seed = 42)
    {
        var rng = new Random(seed);
        var buf = new byte[width * height * 3];
        rng.NextBytes(buf);
        return buf;
    }

    private static byte[] InvertBuffer(byte[] source)
    {
        var result = new byte[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = (byte)(255 - source[i]);
        return result;
    }

    /// <summary>
    /// Returns a copy of <paramref name="source"/> with approximately
    /// <paramref name="perturbFraction"/> of pixels replaced by random values.
    /// </summary>
    private static byte[] PerturbBuffer(byte[] source, double perturbFraction, int seed = 99)
    {
        var rng = new Random(seed);
        var result = new byte[source.Length];
        source.CopyTo(result, 0);

        int pixelCount = source.Length / 3;
        int toFlip = (int)(pixelCount * perturbFraction);

        for (int i = 0; i < toFlip; i++)
        {
            int pixelIndex = rng.Next(pixelCount) * 3;
            result[pixelIndex]     = (byte)rng.Next(256);
            result[pixelIndex + 1] = (byte)rng.Next(256);
            result[pixelIndex + 2] = (byte)rng.Next(256);
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Identical buffers must return exactly 1.0 — not 0.9999… — because when
    /// variance=0 the formula simplifies to C1*C2 / (C1*C2) = 1.0 exactly.
    /// </summary>
    [Fact]
    public void Compare_IdenticalBuffers_Returns1()
    {
        const int width = 64, height = 64;
        byte[] bufferA = MakeRandomBuffer(width, height, seed: 42);
        byte[] bufferB = (byte[])bufferA.Clone();

        double result = SsimScorer.Compare(bufferA, bufferB, width, height);

        result.Should().Be(1.0, because: "identical buffers must produce SSIM = 1.0 exactly");
    }

    /// <summary>
    /// Inverted buffer (B = 255 - A for every byte) produces very low SSIM.
    /// For natural random noise, inverting maximises mean distance and reverses
    /// covariance, resulting in SSIM well below 0.2.
    /// </summary>
    [Fact]
    public void Compare_InvertedBuffer_ReturnsLow()
    {
        const int width = 64, height = 64;
        byte[] bufferA = MakeRandomBuffer(width, height, seed: 42);
        byte[] bufferB = InvertBuffer(bufferA);

        double result = SsimScorer.Compare(bufferA, bufferB, width, height);

        result.Should().BeLessThan(0.2,
            because: "inverting all pixel values should yield very low structural similarity");
    }

    /// <summary>
    /// Slightly perturbed buffer (1% of pixels replaced) should remain highly
    /// similar (SSIM > 0.9).
    /// </summary>
    [Fact]
    public void Compare_SlightlyPerturbed_ReturnsHigh()
    {
        const int width = 64, height = 64;
        byte[] bufferA = MakeRandomBuffer(width, height, seed: 42);
        byte[] bufferB = PerturbBuffer(bufferA, perturbFraction: 0.01, seed: 7);

        double result = SsimScorer.Compare(bufferA, bufferB, width, height);

        result.Should().BeGreaterThan(0.9,
            because: "only 1% of pixels differ so structural similarity should remain high");
    }

    /// <summary>
    /// Verifies SSIM is monotonically decreasing as noise increases:
    /// identical > 5%-noise > 50%-noise.
    /// </summary>
    [Fact]
    public void Compare_MonotonicityCheck()
    {
        const int width = 64, height = 64;
        byte[] reference = MakeRandomBuffer(width, height, seed: 42);

        byte[] identical  = (byte[])reference.Clone();
        byte[] fiveNoise  = PerturbBuffer(reference, perturbFraction: 0.05, seed: 11);
        byte[] fiftyNoise = PerturbBuffer(reference, perturbFraction: 0.50, seed: 13);

        double ssimIdentical  = SsimScorer.Compare(reference, identical,  width, height);
        double ssimFiveNoise  = SsimScorer.Compare(reference, fiveNoise,  width, height);
        double ssimFiftyNoise = SsimScorer.Compare(reference, fiftyNoise, width, height);

        ssimIdentical.Should().BeGreaterThan(ssimFiveNoise,
            because: "identical image must score higher than 5%-noise image");

        ssimFiveNoise.Should().BeGreaterThan(ssimFiftyNoise,
            because: "5%-noise image must score higher than 50%-noise image");
    }

    /// <summary>
    /// Mismatched buffer size (length ≠ width*height*3) must throw ArgumentException.
    /// </summary>
    [Fact]
    public void Compare_MismatchedSize_Throws()
    {
        byte[] correct  = new byte[64 * 64 * 3];
        byte[] tooShort = new byte[64 * 64 * 3 - 1];

        var act = () => SsimScorer.Compare(correct, tooShort, 64, 64);

        act.Should().Throw<ArgumentException>(
            because: "buffer length mismatch must be rejected with ArgumentException");
    }

    /// <summary>
    /// Exactly 8×8 image — a single window covering the entire image — must not
    /// throw and must return a valid SSIM value.
    /// </summary>
    [Fact]
    public void Compare_SmallBuffer_8x8_Works()
    {
        const int width = 8, height = 8;
        byte[] bufferA = MakeRandomBuffer(width, height, seed: 42);
        byte[] bufferB = (byte[])bufferA.Clone();

        double result = SsimScorer.Compare(bufferA, bufferB, width, height);

        result.Should().Be(1.0, because: "identical 8×8 buffers must return 1.0");
    }
}
