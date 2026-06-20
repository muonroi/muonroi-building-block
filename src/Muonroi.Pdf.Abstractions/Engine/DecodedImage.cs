namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Decoded raster image data returned by <see cref="IImageDecoder"/>, carrying raw pixel bytes
/// alongside dimensional and MIME-type metadata needed by the layout engine.
/// </summary>
/// <param name="Width">Image width in pixels.</param>
/// <param name="Height">Image height in pixels.</param>
/// <param name="Data">Raw decoded pixel bytes (format depends on the decoder implementation).</param>
/// <param name="ContentType">MIME type of the original encoded image (e.g. <c>image/png</c>, <c>image/jpeg</c>).</param>
public sealed record DecodedImage(int Width, int Height, ReadOnlyMemory<byte> Data, string ContentType);
