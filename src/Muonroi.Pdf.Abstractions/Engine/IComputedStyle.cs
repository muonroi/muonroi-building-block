namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>Read-only accessor for CSS computed property values on a styled node.</summary>
public interface IComputedStyle
{
    string? GetValue(string property);
    bool HasProperty(string property);
}
