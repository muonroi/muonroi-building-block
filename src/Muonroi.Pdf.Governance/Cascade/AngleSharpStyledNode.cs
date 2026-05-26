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
                ICssStyleDeclaration? computed = _window.GetComputedStyle(el);
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
