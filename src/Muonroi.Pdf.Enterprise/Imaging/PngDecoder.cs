namespace Muonroi.Pdf.Enterprise.Imaging;

/// <summary>
/// Pure-managed PNG decoder that produces interleaved 8-bit RGB pixel buffers suitable for
/// <see cref="Quality.SsimScorer.Compare"/>. Supports only 8-bit RGB (color_type=2, bit_depth=8)
/// PNGs — the only format accepted by the SSIM quality gate.
/// </summary>
/// <remarks>
/// Uses <see cref="System.IO.Compression.DeflateStream"/> (BCL) to decompress IDAT data.
/// No native dependencies are introduced. Applies PNG row-filter reconstruction (Sub, Up, Average, Paeth).
/// </remarks>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PngDecoder is an internal image-decoding utility that throws ArgumentException/InvalidDataException as structural validation errors; these are appropriate for a low-level codec.")]
public static class PngDecoder
{
    private static ReadOnlySpan<byte> PngSignature
        => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>
    /// Decodes an 8-bit RGB PNG file into a raw interleaved RGB pixel buffer.
    /// </summary>
    /// <param name="pngBytes">Raw PNG file bytes.</param>
    /// <returns>
    /// A tuple of (Rgb, Width, Height) where <c>Rgb</c> is an interleaved 8-bit RGB buffer
    /// with stride = Width × 3, row-major, suitable for <see cref="Quality.SsimScorer.Compare"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="pngBytes"/> is null/empty, does not begin with the PNG
    /// signature, or encodes a color type / bit depth other than 8-bit RGB (color_type=2,
    /// bit_depth=8).
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when the PNG structure is malformed (truncated chunks, missing IDAT, bad filter byte).
    /// </exception>
    public static (byte[] Rgb, int Width, int Height) DecodeRgb(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        if (pngBytes.Length < 8 || !pngBytes.AsSpan(0, 8).SequenceEqual(PngSignature))
            throw new ArgumentException(
                "Data does not begin with a valid PNG signature.",
                nameof(pngBytes));

        int pos = 8; // skip 8-byte PNG signature

        int width = 0, height = 0;
        bool ihdrParsed = false;
        byte colorType = 0, bitDepth = 0;

        using var idatStream = new MemoryStream();

        // Parse PNG chunks
        while (pos + 12 <= pngBytes.Length)
        {
            int chunkLength = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(pos, 4));
            pos += 4;

            if (pos + 4 > pngBytes.Length)
                throw new InvalidDataException("PNG chunk type truncated.");

            string chunkType = System.Text.Encoding.ASCII.GetString(pngBytes, pos, 4);
            pos += 4;

            if (pos + chunkLength + 4 > pngBytes.Length)
                throw new InvalidDataException($"PNG chunk '{chunkType}' data truncated.");

            switch (chunkType)
            {
                case "IHDR":
                    if (chunkLength < 13)
                        throw new InvalidDataException("IHDR chunk too short.");

                    width     = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(pos, 4));
                    height    = (int)BinaryPrimitives.ReadUInt32BigEndian(pngBytes.AsSpan(pos + 4, 4));
                    bitDepth  = pngBytes[pos + 8];
                    colorType = pngBytes[pos + 9];

                    if (colorType != 2 || bitDepth != 8)
                        throw new ArgumentException(
                            $"Unsupported PNG: color_type={colorType}, bit_depth={bitDepth}. " +
                            "Only 8-bit RGB (color_type=2, bit_depth=8) is supported by the SSIM canary gate. " +
                            "Convert the image to 8-bit RGB PNG before submitting.",
                            nameof(pngBytes));

                    ihdrParsed = true;
                    break;

                case "IDAT":
                    idatStream.Write(pngBytes, pos, chunkLength);
                    break;

                case "IEND":
                    goto doneChunks;
            }

            pos += chunkLength + 4; // chunk data + CRC
        }

        doneChunks:

        if (!ihdrParsed)
            throw new InvalidDataException("PNG missing IHDR chunk.");

        if (idatStream.Length == 0)
            throw new InvalidDataException("PNG contains no IDAT chunks.");

        // Decompress all IDAT data (zlib: skip 2-byte CMF+FLG header, then raw DEFLATE)
        idatStream.Position = 2; // skip zlib header bytes
        byte[] filtered;
        using (var deflate = new DeflateStream(idatStream, CompressionMode.Decompress, leaveOpen: true))
        using (var outStream = new MemoryStream())
        {
            deflate.CopyTo(outStream);
            filtered = outStream.ToArray();
        }

        // Each row = 1 filter byte + width*3 bytes
        int rowStride = width * 3;
        int expectedFilteredLength = height * (1 + rowStride);

        if (filtered.Length < expectedFilteredLength)
            throw new InvalidDataException(
                $"PNG decompressed data too short: got {filtered.Length} bytes, " +
                $"expected {expectedFilteredLength} for {width}×{height} 8-bit RGB.");

        byte[] rgb = new byte[width * height * 3];
        byte[] prevRow = new byte[rowStride]; // zero-initialised (virtual row above first)

        for (int y = 0; y < height; y++)
        {
            int srcBase = y * (1 + rowStride);
            byte filterType = filtered[srcBase];
            int destBase = y * rowStride;

            // Copy raw scanline bytes into destination slice first, then reconstruct filter in-place
            Buffer.BlockCopy(filtered, srcBase + 1, rgb, destBase, rowStride);

            ReconstructFilter(filterType, rgb, destBase, rowStride, prevRow);

            // Save this row as prevRow for next iteration
            Buffer.BlockCopy(rgb, destBase, prevRow, 0, rowStride);
        }

        return (rgb, width, height);
    }

    private static void ReconstructFilter(
        byte filterType,
        byte[] rgb,
        int rowStart,
        int rowStride,
        byte[] prevRow)
    {
        const int bpp = 3; // bytes per pixel for 8-bit RGB

        switch (filterType)
        {
            case 0: // None — raw bytes already correct
                break;

            case 1: // Sub — a[x] += a[x - bpp]
                for (int i = bpp; i < rowStride; i++)
                    rgb[rowStart + i] = (byte)(rgb[rowStart + i] + rgb[rowStart + i - bpp]);
                break;

            case 2: // Up — a[x] += prior[x]
                for (int i = 0; i < rowStride; i++)
                    rgb[rowStart + i] = (byte)(rgb[rowStart + i] + prevRow[i]);
                break;

            case 3: // Average — a[x] += floor((a[x-bpp] + prior[x]) / 2)
                for (int i = 0; i < rowStride; i++)
                {
                    int left  = i >= bpp ? rgb[rowStart + i - bpp] : 0;
                    int above = prevRow[i];
                    rgb[rowStart + i] = (byte)(rgb[rowStart + i] + (left + above) / 2);
                }
                break;

            case 4: // Paeth predictor
                for (int i = 0; i < rowStride; i++)
                {
                    int left       = i >= bpp ? rgb[rowStart + i - bpp] : 0;
                    int above      = prevRow[i];
                    int upperLeft  = i >= bpp ? prevRow[i - bpp] : 0;
                    rgb[rowStart + i] = (byte)(rgb[rowStart + i] + PaethPredictor(left, above, upperLeft));
                }
                break;

            default:
                throw new InvalidDataException($"PNG row filter type {filterType} is not defined by the PNG specification.");
        }
    }

    /// <summary>PNG Paeth predictor function (PNG spec §9.4).</summary>
    private static int PaethPredictor(int a, int b, int c)
    {
        int p  = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }
}
