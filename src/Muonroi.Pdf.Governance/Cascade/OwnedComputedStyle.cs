namespace Muonroi.Pdf.Governance.Cascade;

/// <summary>
/// <see cref="IComputedStyle"/> implementation backed by a pre-resolved
/// <see cref="Dictionary{TKey,TValue}"/> produced by <see cref="CascadeResolver"/>.
/// Mirrors the shape of <c>AngleSharpComputedStyle</c> with no dependency on
/// <c>ICssStyleDeclaration</c> or <c>IWindow.GetComputedStyle</c>.
/// </summary>
internal sealed class OwnedComputedStyle : IComputedStyle
{
    private static readonly OwnedComputedStyle _empty =
        new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    /// <summary>An empty computed style — every <c>GetValue</c> returns <see langword="null"/>.</summary>
    public static IComputedStyle Empty => _empty;

    private readonly Dictionary<string, string> _resolved;

    internal OwnedComputedStyle(Dictionary<string, string> resolved) => _resolved = resolved;

    /// <inheritdoc/>
    public string? GetValue(string property) =>
        _resolved.TryGetValue(property, out string? v) ? v : null;

    /// <inheritdoc/>
    public bool HasProperty(string property) =>
        _resolved.TryGetValue(property, out string? v) && !string.IsNullOrEmpty(v);
}
