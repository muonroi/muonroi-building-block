namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Critical diagnostic that validates required Muonroi configuration sections are present and non-empty.
/// Only checks sections for packages that appear to be registered.
/// </summary>
public sealed class ConfigurationDiagnostic : IMEcosystemDiagnostic
{
    /// <inheritdoc />
    public string Name => "ConfigurationDiagnostic";

    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Critical;

    /// <summary>
    /// Maps a configuration section key to the service type that requires it.
    /// The service type string is used to skip the check when the package is not registered.
    /// </summary>
    private static readonly IReadOnlyList<(string Section, string RequiredByTypeName, string FixHint)> RequiredSections =
    [
        ("Muonroi:License", "Muonroi.Governance.License.ILicenseGuard",
            "Add a 'Muonroi:License' section to appsettings.json with your license key."),

        ("Muonroi:Tenancy", "Muonroi.Tenancy.Abstractions.ITenantContext",
            "Add a 'Muonroi:Tenancy' section to appsettings.json to configure multi-tenancy."),
    ];

    /// <inheritdoc />
    public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        IConfiguration? config = sp.GetService<IConfiguration>();
        if (config is null)
        {
            return Task.FromResult(DiagnosticResult.Pass("IConfiguration not registered — skipping configuration validation."));
        }

        var missing = new List<string>();

        foreach (var (section, requiredByTypeName, fixHint) in RequiredSections)
        {
            // Only validate if the owning service type is actually registered
            Type? ownerType = FindTypeByName(requiredByTypeName);
            if (ownerType is null)
                continue;

            object? service = sp.GetService(ownerType);
            if (service is null)
                continue; // Package not registered — skip this section check

            IConfigurationSection configSection = config.GetSection(section);
            bool hasValue = configSection.Exists() && configSection.GetChildren().Any();

            if (!hasValue)
            {
                missing.Add($"Missing or empty config section '{section}'  →  Fix: {fixHint}");
            }
        }

        DiagnosticResult result = missing.Count == 0
            ? DiagnosticResult.Pass("All required configuration sections are present.")
            : DiagnosticResult.Fail(
                $"{missing.Count} required configuration section(s) are missing or empty.",
                fixSuggestion: string.Join(Environment.NewLine, missing),
                details: new Dictionary<string, object> { ["missingSections"] = missing });

        return Task.FromResult(result);
    }

    private static Type? FindTypeByName(string fullyQualifiedName)
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
