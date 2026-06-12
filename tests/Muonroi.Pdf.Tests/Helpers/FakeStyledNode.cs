namespace Muonroi.Pdf.Tests.Helpers;

internal sealed class FakeStyledNode : IStyledNode
{
    private readonly Dictionary<string, string> _styles;
    private readonly Dictionary<string, string> _attributes;

    public string LocalName { get; set; } = "div";
    public string? TextContent { get; set; }
    public bool IsElement { get; set; } = true;
    public bool IsText { get; set; }
    public List<IStyledNode> ChildList { get; } = new();

    public FakeStyledNode(string localName = "div",
        Dictionary<string, string>? styles = null,
        Dictionary<string, string>? attributes = null)
    {
        LocalName = localName;
        _styles = styles ?? new();
        _attributes = attributes ?? new();
    }

    public IComputedStyle Style => new FakeComputedStyle(_styles);
    public IReadOnlyList<IStyledNode> Children => ChildList;
    public string? GetAttribute(string name) => _attributes.GetValueOrDefault(name);
}

internal sealed class FakeComputedStyle : IComputedStyle
{
    private readonly Dictionary<string, string> _values;

    public FakeComputedStyle(Dictionary<string, string> values)
        => _values = values;

    public string? GetValue(string property)
        => _values.TryGetValue(property, out string? v) ? v : null;

    public bool HasProperty(string property)
        => _values.ContainsKey(property);
}
