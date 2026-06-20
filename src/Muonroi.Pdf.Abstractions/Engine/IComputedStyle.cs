namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>Read-only accessor for CSS computed property values on a styled node.</summary>
public interface IComputedStyle
{
    /// <summary>
    /// Returns the computed value of the given CSS property, or <see langword="null"/>
    /// if the property was not set or does not apply to this node.
    /// </summary>
    /// <param name="property">CSS property name in lowercase kebab-case (e.g. <c>"font-size"</c>).</param>
    /// <returns>The computed value string, or <see langword="null"/> if absent.</returns>
    string? GetValue(string property);

    /// <summary>
    /// Determines whether a computed value exists for the given CSS property on this node.
    /// </summary>
    /// <param name="property">CSS property name in lowercase kebab-case (e.g. <c>"display"</c>).</param>
    /// <returns><see langword="true"/> if the property has a computed value; otherwise <see langword="false"/>.</returns>
    bool HasProperty(string property);
}
