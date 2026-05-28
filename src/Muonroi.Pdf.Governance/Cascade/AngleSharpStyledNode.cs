using AngleSharp.Css.Dom;

namespace Muonroi.Pdf.Governance.Cascade;

internal sealed class AngleSharpStyledNode : IStyledNode
{
    private readonly INode _node;
    private readonly IWindow? _window;

    internal AngleSharpStyledNode(INode node, IWindow? window)
    {
        _node = node;
        _window = window;
    }

    public string LocalName => _node is IElement element ? element.LocalName : "#text";

    public string? TextContent => _node.TextContent;

    public IComputedStyle Style
    {
        get
        {
            if (_node is IElement el && _window is not null)
            {
                ICssStyleDeclaration? computed;
                try
                {
                    computed = _window.GetComputedStyle(el);
                }
                catch (ArgumentException)
                {
                    // AngleSharp requires a render device to resolve relative units (em, rem, %)
                    // when running in a headless context without a browser viewport. The full
                    // computed cascade is unavailable, but the element's inline style attribute
                    // (el.GetStyle()) is always accessible and never throws — it exposes only the
                    // declarations in style="..." without cascade/inheritance. This recovers inline
                    // border/padding/dimension declarations (e.g. table-bodered1 per-cell inline
                    // borders) that would otherwise be silently lost via the Empty path (G17).
                    // Class-rule properties (width:%, float, border from class selectors) still
                    // require the class-rule fallback in BoxTreeBuilder (see G15 fix).
                    ICssStyleDeclaration? inlineStyle = el.GetStyle();
                    if (inlineStyle != null)
                        return new AngleSharpComputedStyle(inlineStyle);
                    return AngleSharpComputedStyle.Empty;
                }

                if (computed is not null)
                    return new AngleSharpComputedStyle(computed);
            }
            return AngleSharpComputedStyle.Empty;
        }
    }

    public IReadOnlyList<IStyledNode> Children
    {
        get
        {
            var result = new List<IStyledNode>();
            foreach (INode child in _node.ChildNodes)
            {
                if (child is IElement || child.NodeType == NodeType.Text)
                    result.Add(new AngleSharpStyledNode(child, _window));
            }
            return result;
        }
    }

    public string? GetAttribute(string name) =>
        (_node as IElement)?.GetAttribute(name);

    public bool IsElement => _node is IElement;

    public bool IsText => _node.NodeType == NodeType.Text;
}
