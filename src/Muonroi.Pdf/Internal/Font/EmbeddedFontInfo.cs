using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Internal.Font;

internal sealed record EmbeddedFontInfo(
    string Family,
    FontWeight Weight,
    FontStyle Style,
    ReadOnlyMemory<byte> SubsetBytes,
    IReadOnlySet<int> UsedCodepoints,
    IReadOnlyDictionary<ushort, ushort> OldToNewGid,
    IReadOnlyList<ushort> SortedGids)
{
    /// <summary>
    /// Backward-compat constructor for callers that don't have GID mapping data
    /// (e.g. test helpers that create EmbeddedFontInfo directly without going through FontPipeline).
    /// OldToNewGid and SortedGids will be empty.
    /// </summary>
    public EmbeddedFontInfo(
        string family,
        FontWeight weight,
        FontStyle style,
        ReadOnlyMemory<byte> subsetBytes,
        IReadOnlySet<int> usedCodepoints)
        : this(family, weight, style, subsetBytes, usedCodepoints,
               new Dictionary<ushort, ushort>(), Array.Empty<ushort>())
    {
    }
}
