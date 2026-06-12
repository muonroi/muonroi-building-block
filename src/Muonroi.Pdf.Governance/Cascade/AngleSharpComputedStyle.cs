using AngleSharp.Css.Dom;

namespace Muonroi.Pdf.Governance.Cascade;

internal sealed class AngleSharpComputedStyle : IComputedStyle
{
    private static readonly AngleSharpComputedStyle _empty = new(null);

    public static IComputedStyle Empty => _empty;

    private readonly ICssStyleDeclaration? _style;

    internal AngleSharpComputedStyle(ICssStyleDeclaration? style) => _style = style;

    public string? GetValue(string property) => _style?.GetPropertyValue(property);

    public bool HasProperty(string property) => !string.IsNullOrEmpty(_style?.GetPropertyValue(property));
}
