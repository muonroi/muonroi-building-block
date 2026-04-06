using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Warning diagnostic that validates tenant configuration when multi-tenancy is enabled.
/// Skips gracefully when <see cref="ITenantContext"/> is not registered.
/// </summary>
public sealed class TenantConfigDiagnostic : IMEcosystemDiagnostic
{
    /// <inheritdoc />
    public string Name => "TenantConfigDiagnostic";

    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

    /// <inheritdoc />
    public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        // Only run when multi-tenancy is registered
        ITenantContext? tenantCtx = sp.GetService<ITenantContext>();
        if (tenantCtx is null)
        {
            return Task.FromResult(DiagnosticResult.Pass("ITenantContext not registered — skipping tenant configuration validation."));
        }

        var issues = new List<string>();

        // Validate MultiTenantOptions configuration section
        IConfiguration? config = sp.GetService<IConfiguration>();
        if (config is not null)
        {
            IConfigurationSection section = config.GetSection(MultiTenantOptions.SectionName);
            if (!section.Exists())
            {
                issues.Add(
                    $"Configuration section '{MultiTenantOptions.SectionName}' is missing.  →  " +
                    "Fix: Add a 'MultiTenantConfigs' section to appsettings.json with Enabled, Strategy, and related settings.");
            }
            else
            {
                // Validate Enabled=true (default is true, but an explicit false with ITenantContext registered is suspicious)
                IOptions<MultiTenantOptions>? options = sp.GetService<IOptions<MultiTenantOptions>>();
                if (options?.Value is { Enabled: false })
                {
                    issues.Add(
                        "ITenantContext is registered but MultiTenantOptions.Enabled=false.  →  " +
                        "Fix: Either set Enabled=true in MultiTenantConfigs or remove the tenancy DI registration.");
                }
            }
        }

        DiagnosticResult result = issues.Count == 0
            ? DiagnosticResult.Pass("Tenant configuration is valid.")
            : DiagnosticResult.Fail(
                $"{issues.Count} tenant configuration issue(s) found.",
                fixSuggestion: string.Join(Environment.NewLine, issues),
                details: new Dictionary<string, object> { ["issues"] = issues });

        return Task.FromResult(result);
    }
}
