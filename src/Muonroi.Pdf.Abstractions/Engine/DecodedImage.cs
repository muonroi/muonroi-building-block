namespace Muonroi.Pdf.Abstractions.Engine;

public sealed record DecodedImage(int Width, int Height, ReadOnlyMemory<byte> Data, string ContentType);
