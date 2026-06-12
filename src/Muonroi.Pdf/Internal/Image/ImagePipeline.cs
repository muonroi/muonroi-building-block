using System.Diagnostics;
using Muonroi.Pdf.Abstractions.Exceptions;

namespace Muonroi.Pdf.Internal.Image;

internal sealed class ImagePipeline
{
    internal async Task<IReadOnlyDictionary<string, DecodedImage>> ResolveAsync(
        IStyledDocument doc,
        IResourceResolver resolver,
        IImageDecoder decoder,
        PdfConfigs.PdfLimits limits,
        CancellationToken ct)
    {
        _ = limits; // all PdfLimits members are compile-time constants; referenced by type below

        var dict = new Dictionary<string, DecodedImage>(StringComparer.Ordinal);

        IEnumerable<string> srcs = CollectImageSrcs(doc.Root).Distinct(StringComparer.Ordinal);

        foreach (string src in srcs)
        {
            ct.ThrowIfCancellationRequested();

            DecodedImage decoded;

            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                (ReadOnlyMemory<byte> bytes, string contentType) = DataUriDecoder.Decode(src);
                decoded = decoder.Decode(bytes.Span, contentType);
            }
            else
            {
                Uri uri;
                try
                {
                    uri = new Uri(src, UriKind.Absolute);
                }
                catch (UriFormatException ex)
                {
                    Debug.WriteLine($"[ImagePipeline] Skipping malformed image URI: {src} — {ex.Message}");
                    continue;
                }

                ResourceResult? result = await resolver.ResolveAsync(uri, contentTypeHint: null, ct).ConfigureAwait(false);

                if (result is null)
                {
                    Debug.WriteLine($"[ImagePipeline] Image not resolved: {src}");
                    continue;
                }

                decoded = decoder.Decode(result.Bytes.Span, result.ContentType);
            }

            if ((long)decoded.Width * decoded.Height > PdfConfigs.PdfLimits.Defaults.MaxImagePixels)
                throw new PdfInputLimitException(
                    "IMG-MAX-PIXELS",
                    "MaxImagePixels",
                    (long)decoded.Width * decoded.Height,
                    PdfConfigs.PdfLimits.Defaults.MaxImagePixels);

            dict[src] = decoded;
        }

        return dict;
    }

    private static IEnumerable<string> CollectImageSrcs(IStyledNode node)
    {
        if (node.IsElement && node.LocalName == "img")
        {
            string? src = node.GetAttribute("src");
            if (!string.IsNullOrEmpty(src))
                yield return src;
        }

        // Also collect background-image data URIs from CSS
        if (!node.IsText)
        {
            string? bgImage = node.Style?.GetValue("background-image");
            if (!string.IsNullOrEmpty(bgImage) &&
                bgImage.Contains("data:", StringComparison.OrdinalIgnoreCase) &&
                bgImage.Contains("base64", StringComparison.OrdinalIgnoreCase))
            {
                int start = bgImage.IndexOf("url(", StringComparison.OrdinalIgnoreCase) + 4;
                int end = bgImage.LastIndexOf(')');
                if (start > 4 && end > start)
                {
                    string uri = bgImage[start..end].Trim().Trim('\'', '"');
                    if (uri.StartsWith("data:", StringComparison.Ordinal))
                        yield return uri;
                }
            }
        }

        foreach (IStyledNode child in node.Children)
            foreach (string src in CollectImageSrcs(child))
                yield return src;
    }
}
