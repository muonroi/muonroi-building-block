using Muonroi.AspNetCore.Diagnostics;

namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Critical diagnostic that attempts to resolve core Muonroi services from the DI container.
/// Catches missing registrations before the first HTTP request.
/// </summary>
public sealed class DependencyGraphDiagnostic : IMEcosystemDiagnostic
{
    /// <inheritdoc />
    public string Name => "DependencyGraphDiagnostic";

    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Critical;

    /// <summary>
    /// Service types to probe. These are core Muonroi services that should resolve
    /// successfully when the package is registered. The list uses strings to avoid
    /// hard assembly references — resolution is attempted via <see cref="IServiceProvider"/>.
    /// </summary>
    private static readonly IReadOnlyList<(string TypeName, string FixHint)> ProbedServiceTypeNames =
    [
        ("Muonroi.Core.Abstractions.Interfaces.IMJsonSerializeService",
            "Call services.AddApplication() or ensure Muonroi.Core is registered."),

        ("Muonroi.Core.Abstractions.Interfaces.IMDateTimeService",
            "Call services.AddApplication() to register core Muonroi services.")
    ];

    /// <inheritdoc />
    public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        var failures = new List<string>();

        foreach (var (typeName, fixHint) in ProbedServiceTypeNames)
        {
            Type? serviceType = Type.GetType(typeName) ?? FindTypeByName(sp, typeName);
            if (serviceType is null)
            {
                // Type not loaded into the AppDomain — package not referenced; skip gracefully
                continue;
            }

            try
            {
                object? resolved = sp.GetService(serviceType);
                if (resolved is null)
                {
                    failures.Add($"Failed to resolve: {typeName}  →  Fix: {fixHint}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Exception resolving {typeName}: {ex.Message}  →  Fix: {fixHint}");
            }
        }

        DiagnosticResult result = failures.Count == 0
            ? DiagnosticResult.Pass("All core Muonroi services resolved successfully.")
            : DiagnosticResult.Fail(
                $"DI graph has {failures.Count} unresolved service(s).",
                fixSuggestion: string.Join(Environment.NewLine, failures),
                details: new Dictionary<string, object> { ["failures"] = failures });

        return Task.FromResult(result);
    }

    private static Type? FindTypeByName(IServiceProvider sp, string fullyQualifiedName)
    {
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? found = asm.GetType(fullyQualifiedName);
            if (found is not null)
                return found;
        }
        return null;
    }
}
