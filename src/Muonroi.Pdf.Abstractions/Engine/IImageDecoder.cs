namespace Muonroi.Pdf.Abstractions.Engine;

public interface IImageDecoder
{
    DecodedImage Decode(ReadOnlySpan<byte> data, string contentType);
}
