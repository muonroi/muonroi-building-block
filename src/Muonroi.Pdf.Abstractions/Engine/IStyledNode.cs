namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>AngleSharp-free DOM node carrying computed CSS styles. Layout engine traverses only this interface.</summary>
public interface IStyledNode
{
    /// <summary>
    /// Tag name of an element node in lowercase (e.g. <c>"div"</c>, <c>"p"</c>),
    /// or an empty string for text nodes.
    /// </summary>
    string LocalName { get; }

    /// <summary>
    /// Text content of the node. For text nodes this is the literal character data;
    /// for element nodes it is the concatenated text of all descendant text nodes.
    /// Returns <see langword="null"/> when there is no text content.
    /// </summary>
    string? TextContent { get; }

    /// <summary>Computed CSS style values for this node, resolved after cascade.</summary>
    IComputedStyle Style { get; }

    /// <summary>Direct child nodes of this node, in document order.</summary>
    IReadOnlyList<IStyledNode> Children { get; }

    /// <summary>
    /// Returns the value of the named HTML attribute, or <see langword="null"/> if the
    /// attribute is not present on this node.
    /// </summary>
    /// <param name="name">Attribute name in lowercase (e.g. <c>"src"</c>, <c>"class"</c>).</param>
    /// <returns>The attribute value string, or <see langword="null"/> if absent.</returns>
    string? GetAttribute(string name);

    /// <summary><see langword="true"/> if this node represents an HTML element; <see langword="false"/> for text and other node types.</summary>
    bool IsElement { get; }

    /// <summary><see langword="true"/> if this node represents a text node; <see langword="false"/> for elements and other node types.</summary>
    bool IsText { get; }
}
