using System.Buffers.Binary;
using Muonroi.Pdf.Abstractions.Exceptions;

namespace Muonroi.Pdf.Internal.Image;

internal sealed class PureImageDecoder : IImageDecoder
{
    private static ReadOnlySpan<byte> PngMagic => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    public DecodedImage Decode(ReadOnlySpan<byte> data, string contentType)
    {
        // Auto-detect by magic bytes first
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8)
            return DecodeJpeg(data);

        if (data.Length >= 8 && data[..8].SequenceEqual(PngMagic))
            return DecodePng(data);

        // Fall back to content-type
        if (contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            return DecodePng(data);

        if (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
            return DecodeJpeg(data);

        string magic = data.Length >= 2
            ? $"{data[0]:X2} {data[1]:X2}"
            : "(too short)";
        throw new PdfFormatException("IMG-FORMAT", $"Unrecognized image format (magic: {magic})");
    }

    private static DecodedImage DecodePng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 26)
            throw new PdfFormatException("IMG-FORMAT", "PNG data too short to contain IHDR");

        if (!data[..8].SequenceEqual(PngMagic))
            throw new PdfFormatException("IMG-FORMAT", "Invalid PNG magic bytes");

        int width  = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));

        byte bitDepth  = data[24];
        byte colorType = data[25];

        if (colorType == 3)
            throw new PdfFormatException("PNG-PALETTE",
                "Palette/indexed PNG (color_type=3) is not supported. Convert to 8-bit RGB PNG.");
        if (colorType == 0)
            throw new PdfFormatException("PNG-GRAYSCALE",
                "Grayscale PNG (color_type=0) is not supported. Convert to 8-bit RGB PNG.");
        if (colorType == 4)
            throw new PdfFormatException("PNG-GRAYSCALE-ALPHA",
                "Grayscale+alpha PNG (color_type=4) is not supported. Convert to 8-bit RGB PNG.");
        if (colorType == 6)
            throw new PdfFormatException("PNG-RGBA",
                "RGBA PNG (color_type=6) is not supported. Convert to 8-bit RGB PNG (strip alpha channel).");
        if (colorType == 2 && bitDepth == 16)
            throw new PdfFormatException("PNG-16BIT",
                "16-bit RGB PNG (bit_depth=16) is not supported. Convert to 8-bit RGB PNG.");
        if (colorType != 2 || bitDepth != 8)
            throw new PdfFormatException("PNG-FORMAT",
                $"Unsupported PNG: color_type={colorType}, bit_depth={bitDepth}. Only 8-bit RGB (color_type=2, bit_depth=8) is supported.");

        return new DecodedImage(width, height, data.ToArray(), "image/png");
    }

    private static DecodedImage DecodeJpeg(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            throw new PdfFormatException("IMG-FORMAT", "JPEG data too short");

        if (data[0] != 0xFF || data[1] != 0xD8)
            throw new PdfFormatException("IMG-FORMAT", "Invalid JPEG SOI marker");

        int pos = 2;
        while (pos + 1 < data.Length)
        {
            if (data[pos] != 0xFF)
                throw new PdfFormatException("IMG-FORMAT", "Lost JPEG marker sync");

            byte markerByte = data[pos + 1];

            // Skip FF padding bytes
            if (markerByte == 0xFF)
            {
                pos++;
                continue;
            }

            // SOF0-SOF3
            if (markerByte == 0xC0 || markerByte == 0xC1 || markerByte == 0xC2 || markerByte == 0xC3)
            {
                if (pos + 9 > data.Length)
                    throw new PdfFormatException("IMG-FORMAT", "JPEG SOF segment truncated");

                int height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 5, 2));
                int width  = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 7, 2));

                return new DecodedImage(width, height, data.ToArray(), "image/jpeg");
            }

            // Skip this segment
            if (pos + 4 > data.Length)
                throw new PdfFormatException("IMG-FORMAT", "JPEG data truncated reading segment length");

            int segLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 2, 2));
            pos += 2 + segLen;

            if (pos + 3 >= data.Length)
                throw new PdfFormatException("IMG-FORMAT", "JPEG SOF marker not found");
        }

        throw new PdfFormatException("IMG-FORMAT", "JPEG SOF marker not found");
    }
}
