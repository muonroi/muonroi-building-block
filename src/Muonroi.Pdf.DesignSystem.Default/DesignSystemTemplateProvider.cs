using System.Reflection;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Pdf.DesignSystem;

/// <summary>
/// Provides default HTML/CSS templates embedded in the Muonroi.Pdf.DesignSystem.Default assembly.
/// </summary>
public static class DesignSystemTemplateProvider
{
    private static readonly Dictionary<string, string> _cache = LoadAll();

    private static Dictionary<string, string> LoadAll()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Assembly assembly = Assembly.GetExecutingAssembly();

        string[] names = { "invoice", "receipt", "report" };
        foreach (string name in names)
        {
            string resourceName = $"Muonroi.Pdf.DesignSystem.Default.Templates.{name}.html";
            using Stream? stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new MInternalException(
                    $"Embedded resource not found: {resourceName}. " +
                    $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
            using var reader = new StreamReader(stream);
            result[name] = reader.ReadToEnd();
        }

        return result;
    }

    /// <summary>
    /// Returns the raw HTML string for the named template.
    /// </summary>
    /// <param name="name">Template name — one of: "invoice", "receipt", "report" (case-insensitive).</param>
    /// <returns>HTML template string with <c>{{TokenName}}</c> placeholders.</returns>
    /// <exception cref="MNotFoundException">Thrown when <paramref name="name"/> is not a known template.</exception>
    public static string GetTemplate(string name)
    {
        string key = MGuard.NotNull(name).ToLowerInvariant();

        if (_cache.TryGetValue(key, out string? html))
            return html;

        throw new MNotFoundException("Design system template (valid: invoice, receipt, report)", name);
    }
}
