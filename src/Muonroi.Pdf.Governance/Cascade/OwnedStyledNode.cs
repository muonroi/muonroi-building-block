namespace Muonroi.Pdf.Governance.Cascade;

/// <summary>
/// <see cref="IStyledNode"/> implementation that resolves <see cref="Style"/> via
/// <see cref="CascadeResolver"/>, threading the parent's resolved map for inheritance,
/// and caches the resolved style per node.
///
/// <para>
/// This replaces the <c>AngleSharpStyledNode</c> internals. No call to
/// <c>IWindow.GetComputedStyle</c> or <c>ComputeCurrentStyle</c> is made anywhere.
/// </para>
///
/// <para>
/// Resolution strategy: <em>eager top-down</em>. The root node resolves immediately on
/// first <see cref="Style"/> access; each child OwnedStyledNode is constructed with this
/// node's resolved map as its <c>parentResolved</c> argument, so the chain resolves
/// root→leaf one node at a time, each node running the 7-step resolver exactly once.
/// The result is cached in a nullable backing field.
/// </para>
/// </summary>
internal sealed class OwnedStyledNode : IStyledNode
{
    // -----------------------------------------------------------------------
    // Core state
    // -----------------------------------------------------------------------
    private readonly INode _node;
    private readonly CascadeResolver _resolver;
    private readonly IReadOnlyDictionary<string, string>? _parentResolved;

    // -----------------------------------------------------------------------
    // Per-node style cache (resolver runs at most once per node)
    // -----------------------------------------------------------------------
    private IComputedStyle? _cachedStyle;

    /// <summary>
    /// The raw resolved map for this node — used as the parent map when constructing children.
    /// Null until <see cref="Style"/> is first accessed on an element node.
    /// For text nodes this is always null (text nodes have no cascade).
    /// </summary>
    private Dictionary<string, string>? _resolvedMap;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates an OwnedStyledNode wrapping <paramref name="node"/>.
    /// </summary>
    /// <param name="node">The DOM node (element or text).</param>
    /// <param name="resolver">The shared cascade resolver for the document.</param>
    /// <param name="parentResolved">
    /// The already-resolved property map of the parent element (for inheritance, Step 6).
    /// Pass <see langword="null"/> for the root element.
    /// </param>
    internal OwnedStyledNode(
        INode node,
        CascadeResolver resolver,
        IReadOnlyDictionary<string, string>? parentResolved)
    {
        _node = node;
        _resolver = resolver;
        _parentResolved = parentResolved;
    }

    // -----------------------------------------------------------------------
    // IStyledNode implementation
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public string LocalName =>
        _node is IElement element ? element.LocalName : "#text";

    /// <inheritdoc/>
    public string? TextContent => _node.TextContent;

    /// <inheritdoc/>
    public IComputedStyle Style
    {
        get
        {
            if (_cachedStyle is not null)
                return _cachedStyle;

            if (_node is IElement el)
            {
                // Resolve via the owned cascade (never calls GetComputedStyle).
                _resolvedMap = _resolver.Resolve(el, _parentResolved);
                _cachedStyle = new OwnedComputedStyle(_resolvedMap);
            }
            else
            {
                // Text nodes carry no cascade; return a stable empty style.
                _cachedStyle = OwnedComputedStyle.Empty;
            }

            return _cachedStyle;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStyledNode> Children
    {
        get
        {
            // Ensure this node's resolved map is available before constructing children.
            // Accessing Style triggers resolution and populates _resolvedMap.
            _ = Style;

            var result = new List<IStyledNode>();
            foreach (INode child in _node.ChildNodes)
            {
                if (child is IElement || child.NodeType == NodeType.Text)
                {
                    result.Add(new OwnedStyledNode(child, _resolver, _resolvedMap));
                }
            }
            return result;
        }
    }

    /// <inheritdoc/>
    public string? GetAttribute(string name) =>
        (_node as IElement)?.GetAttribute(name);

    /// <inheritdoc/>
    public bool IsElement => _node is IElement;

    /// <inheritdoc/>
    public bool IsText => _node.NodeType == NodeType.Text;
}
