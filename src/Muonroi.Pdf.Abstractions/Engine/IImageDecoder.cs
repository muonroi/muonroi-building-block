namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Decodes encoded image bytes (PNG, JPEG, etc.) into raw pixel data and dimensional metadata
/// consumed by the PDF layout and rendering pipeline.
/// </summary>
public interface IImageDecoder
{
    /// <summary>
    /// Decodes the encoded image bytes into a <see cref="DecodedImage"/> containing raw pixel data,
    /// dimensions, and the original MIME type.
    /// </summary>
    /// <param name="data">Encoded image bytes (e.g. the raw bytes of a PNG or JPEG file).</param>
    /// <param name="contentType">MIME type of the encoded data (e.g. <c>image/png</c>), used to select the correct codec.</param>
    /// <returns>A <see cref="DecodedImage"/> with decoded pixel bytes and image dimensions.</returns>
    DecodedImage Decode(ReadOnlySpan<byte> data, string contentType);
}
