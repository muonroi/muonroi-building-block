using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Internal.Font;

internal sealed record EmbeddedFontInfo(
    string Family,
    FontWeight Weight,
    FontStyle Style,
    ReadOnlyMemory<byte> SubsetBytes,
    IReadOnlySet<int> UsedCodepoints);
