namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>AngleSharp-free DOM node carrying computed CSS styles. Layout engine traverses only this interface.</summary>
public interface IStyledNode
{
    string LocalName { get; }
    string? TextContent { get; }
    IComputedStyle Style { get; }
    IReadOnlyList<IStyledNode> Children { get; }
    string? GetAttribute(string name);
    bool IsElement { get; }
    bool IsText { get; }
}
