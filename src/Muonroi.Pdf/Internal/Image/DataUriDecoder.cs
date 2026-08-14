namespace Muonroi.Pdf.Internal.Image;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfFormatException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy.")]

internal static class DataUriDecoder
{
    internal static (ReadOnlyMemory<byte> Bytes, string ContentType) Decode(string dataUri)
    {
        if (!dataUri.StartsWith("data:", StringComparison.Ordinal))
            throw new PdfFormatException("IMG-FORMAT", "Not a data: URI");

        string rest = dataUri.Substring(5);

        int commaIdx = rest.IndexOf(',');
        if (commaIdx < 0)
            throw new PdfFormatException("IMG-FORMAT", "data: URI missing comma separator");

        string header = rest[..commaIdx];
        string dataStr = rest[(commaIdx + 1)..];

        bool isBase64 = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        string mediaType = isBase64 ? header[..^";base64".Length] : header;

        if (string.IsNullOrEmpty(mediaType))
            mediaType = "text/plain;charset=US-ASCII";

        string contentType = mediaType.Split(';')[0].Trim();
        if (string.IsNullOrEmpty(contentType))
            contentType = "text/plain";

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !isBase64)
            throw new PdfFormatException("IMG-FORMAT", "data: URI image payload must be base64-encoded");

        dataStr = dataStr.Replace("\r", "").Replace("\n", "").Replace(" ", "");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataStr);
        }
        catch (FormatException ex)
        {
            throw new PdfFormatException("IMG-FORMAT", "Invalid base64 in data: URI", ex);
        }

        return (new ReadOnlyMemory<byte>(bytes), contentType);
    }
}
