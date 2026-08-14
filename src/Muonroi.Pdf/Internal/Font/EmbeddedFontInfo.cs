namespace Muonroi.Pdf.Internal.Font;

using FontStyle = Muonroi.Pdf.Abstractions.FontStyle;

internal sealed record EmbeddedFontInfo(
    string Family,
    FontWeight Weight,
    FontStyle Style,
    ReadOnlyMemory<byte> SubsetBytes,
    IReadOnlySet<int> UsedCodepoints,
    IReadOnlyDictionary<ushort, ushort> OldToNewGid,
    IReadOnlyList<ushort> SortedGids,
    IReadOnlyDictionary<int, ushort> CpToNewGid)
{
    /// <summary>
    /// Backward-compat constructor for callers that don't have GID mapping data
    /// (e.g. test helpers that create EmbeddedFontInfo directly without going through FontPipeline).
    /// OldToNewGid, SortedGids, and CpToNewGid will be empty.
    /// </summary>
    public EmbeddedFontInfo(
        string family,
        FontWeight weight,
        FontStyle style,
        ReadOnlyMemory<byte> subsetBytes,
        IReadOnlySet<int> usedCodepoints)
        : this(family, weight, style, subsetBytes, usedCodepoints,
               new Dictionary<ushort, ushort>(), Array.Empty<ushort>(),
               new Dictionary<int, ushort>())
    {
    }
}
