namespace Muonroi.Pdf.Tests.Fixtures.Png;

/// <summary>
/// Hand-crafts structurally valid PNG byte streams for use in PngDecoder unit tests.
/// No native dependencies — uses only BCL types. Produces real zlib-compressed IDAT data
/// and correct CRC-32 chunk checksums so the writer's ZLibStream decompressor accepts them.
/// </summary>
internal static class PngFixtureBuilder
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // ── Public fixture factories ─────────────────────────────────────────────

    /// <summary>
    /// 8-bit palette (color_type=3) PNG, 16×16 px, 4 colours, no transparency.
    /// Colour table: red, green, blue, white.
    /// Scanlines: each 4×4 pixel block gets one colour, tiled to fill the grid.
    /// </summary>
    public static byte[] Palette4Color()
    {
        const int w = 16, h = 16;
        byte[] plte = BuildPlte(
            (255, 0,   0),   // index 0 = red
            (0,   255, 0),   // index 1 = green
            (0,   0,   255), // index 2 = blue
            (255, 255, 255)  // index 3 = white
        );

        // Each pixel index = (col/4 + row/4*4) % 4
        byte[] indices = BuildIndexScanlines(w, h,
            (x, y) => (byte)((x / 4 + (y / 4) * 4) % 4));

        return Assemble(w, h, colorType: 3, bitDepth: 8,
            extraChunks: new[] { BuildChunk("PLTE", plte) },
            idatData: indices);
    }

    /// <summary>
    /// 8-bit palette (color_type=3) PNG, 16×16 px, 4 colours, colour index 0 fully transparent.
    /// tRNS: [0, 255, 255, 255] — index 0 = alpha 0 (transparent), rest opaque.
    /// </summary>
    public static byte[] PaletteTrns()
    {
        const int w = 16, h = 16;
        byte[] plte = BuildPlte(
            (255, 0,   0),   // index 0 = red (but transparent via tRNS)
            (0,   255, 0),   // index 1 = green
            (0,   0,   255), // index 2 = blue
            (255, 255, 255)  // index 3 = white
        );
        byte[] trns = new byte[] { 0, 255, 255, 255 }; // index 0 alpha=0

        byte[] indices = BuildIndexScanlines(w, h,
            (x, y) => (byte)((x / 4 + (y / 4) * 4) % 4));

        return Assemble(w, h, colorType: 3, bitDepth: 8,
            extraChunks: new[]
            {
                BuildChunk("PLTE", plte),
                BuildChunk("tRNS", trns)
            },
            idatData: indices);
    }

    /// <summary>
    /// 8-bit RGBA (color_type=6) PNG, 32×32 px.
    /// Gradient: top row fully opaque blue, bottom row fully transparent white.
    /// Alpha = 255 * (h - y) / (h - 1) per row.
    /// </summary>
    public static byte[] RgbaLogo()
    {
        const int w = 32, h = 32;
        byte[] scanlines = BuildRgbaScanlines(w, h, (x, y) =>
        {
            byte alpha = (byte)(255 * (h - 1 - y) / (h - 1));
            return (50, 100, 200, alpha); // blue-toned logo pixel
        });

        return Assemble(w, h, colorType: 6, bitDepth: 8,
            extraChunks: Array.Empty<byte[]>(),
            idatData: scanlines);
    }

    /// <summary>
    /// 8-bit grayscale (color_type=0) PNG, 8×8 px.
    /// Horizontal ramp: gray = x * 32 (column 0 = black 0, column 4 = 128).
    /// </summary>
    public static byte[] Gray8()
    {
        const int w = 8, h = 8;
        byte[] scanlines = BuildGrayScanlines(w, h, (x, _) => (byte)(x * 32));

        return Assemble(w, h, colorType: 0, bitDepth: 8,
            extraChunks: Array.Empty<byte[]>(),
            idatData: scanlines);
    }

    /// <summary>
    /// 8-bit grayscale+alpha (color_type=4) PNG, 8×8 px.
    /// Constant gray=200; alpha ramps top (opaque) → bottom (transparent): alpha = 255*(h-1-y)/(h-1).
    /// </summary>
    public static byte[] GrayAlpha8()
    {
        const int w = 8, h = 8;
        byte[] scanlines = BuildGrayAlphaScanlines(w, h, (_, y) =>
        {
            byte alpha = (byte)(255 * (h - 1 - y) / (h - 1));
            return ((byte)200, alpha);
        });

        return Assemble(w, h, colorType: 4, bitDepth: 8,
            extraChunks: Array.Empty<byte[]>(),
            idatData: scanlines);
    }

    // ── Scanline builders ────────────────────────────────────────────────────

    /// <summary>Builds raw (uncompressed) scanline bytes for a palette/index image.
    /// Each row = filter_byte(0=None) + width index bytes.</summary>
    private static byte[] BuildIndexScanlines(int w, int h, Func<int, int, byte> indexAt)
    {
        int rowBytes = 1 + w; // filter + indices
        byte[] buf = new byte[h * rowBytes];
        for (int y = 0; y < h; y++)
        {
            int off = y * rowBytes;
            buf[off] = 0; // filter=None
            for (int x = 0; x < w; x++)
                buf[off + 1 + x] = indexAt(x, y);
        }
        return buf;
    }

    /// <summary>Builds raw scanline bytes for an RGBA image.
    /// Each row = filter_byte(0=None) + w*(R,G,B,A) bytes.</summary>
    private static byte[] BuildRgbaScanlines(int w, int h,
        Func<int, int, (byte r, byte g, byte b, byte a)> pixelAt)
    {
        int rowBytes = 1 + w * 4;
        byte[] buf = new byte[h * rowBytes];
        for (int y = 0; y < h; y++)
        {
            int off = y * rowBytes;
            buf[off] = 0; // filter=None
            for (int x = 0; x < w; x++)
            {
                (byte r, byte g, byte b, byte a) = pixelAt(x, y);
                buf[off + 1 + x * 4]     = r;
                buf[off + 1 + x * 4 + 1] = g;
                buf[off + 1 + x * 4 + 2] = b;
                buf[off + 1 + x * 4 + 3] = a;
            }
        }
        return buf;
    }

    /// <summary>Builds raw scanline bytes for an 8-bit grayscale image.
    /// Each row = filter_byte(0=None) + w gray bytes.</summary>
    private static byte[] BuildGrayScanlines(int w, int h, Func<int, int, byte> grayAt)
    {
        int rowBytes = 1 + w; // filter + 1 byte per pixel
        byte[] buf = new byte[h * rowBytes];
        for (int y = 0; y < h; y++)
        {
            int off = y * rowBytes;
            buf[off] = 0; // filter=None
            for (int x = 0; x < w; x++)
                buf[off + 1 + x] = grayAt(x, y);
        }
        return buf;
    }

    /// <summary>Builds raw scanline bytes for an 8-bit grayscale+alpha image.
    /// Each row = filter_byte(0=None) + w*(gray,alpha) bytes.</summary>
    private static byte[] BuildGrayAlphaScanlines(int w, int h,
        Func<int, int, (byte gray, byte a)> pixelAt)
    {
        int rowBytes = 1 + w * 2;
        byte[] buf = new byte[h * rowBytes];
        for (int y = 0; y < h; y++)
        {
            int off = y * rowBytes;
            buf[off] = 0; // filter=None
            for (int x = 0; x < w; x++)
            {
                (byte gray, byte a) = pixelAt(x, y);
                buf[off + 1 + x * 2]     = gray;
                buf[off + 1 + x * 2 + 1] = a;
            }
        }
        return buf;
    }

    // ── PNG assembly ─────────────────────────────────────────────────────────

    private static byte[] Assemble(
        int w, int h,
        byte colorType, byte bitDepth,
        byte[][] extraChunks,
        byte[] idatData)
    {
        byte[] ihdrData = new byte[13];
        WriteUInt32BE(ihdrData, 0, (uint)w);
        WriteUInt32BE(ihdrData, 4, (uint)h);
        ihdrData[8]  = bitDepth;
        ihdrData[9]  = colorType;
        ihdrData[10] = 0; // compression = deflate
        ihdrData[11] = 0; // filter method = adaptive
        ihdrData[12] = 0; // interlace = none

        byte[] idatCompressed = Compress(idatData);

        var parts = new List<byte[]> { PngSignature };
        parts.Add(BuildChunk("IHDR", ihdrData));
        foreach (byte[] extra in extraChunks)
            parts.Add(extra);
        parts.Add(BuildChunk("IDAT", idatCompressed));
        parts.Add(BuildChunk("IEND", Array.Empty<byte>()));

        int total = parts.Sum(p => p.Length);
        byte[] result = new byte[total];
        int pos = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, result, pos, part.Length);
            pos += part.Length;
        }
        return result;
    }

    private static byte[] BuildChunk(string type, byte[] data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var chunk = new byte[4 + 4 + data.Length + 4];
        WriteUInt32BE(chunk, 0, (uint)data.Length);
        Buffer.BlockCopy(typeBytes, 0, chunk, 4, 4);
        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);
        // CRC over type + data
        byte[] crcInput = new byte[4 + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, 4);
        Buffer.BlockCopy(data, 0, crcInput, 4, data.Length);
        WriteUInt32BE(chunk, 8 + data.Length, Crc32(crcInput));
        return chunk;
    }

    private static byte[] BuildPlte(params (byte r, byte g, byte b)[] colours)
    {
        byte[] buf = new byte[colours.Length * 3];
        for (int i = 0; i < colours.Length; i++)
        {
            buf[i * 3]     = colours[i].r;
            buf[i * 3 + 1] = colours[i].g;
            buf[i * 3 + 2] = colours[i].b;
        }
        return buf;
    }

    private static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    private static void WriteUInt32BE(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)(value & 0xFF);
    }

    private static uint Crc32(byte[] data)
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}
