namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Warning diagnostic that validates capability flags in <see cref="IMEcosystemRegistry"/> match
/// actual DI registrations.
/// Example: <see cref="MCapability.MultiTenant"/> flagged but <see cref="ITenantContext"/> not registered.
/// </summary>
public sealed class EcosystemRegistryDiagnostic : IMEcosystemDiagnostic
{
    /// <inheritdoc />
    public string Name => "EcosystemRegistryDiagnostic";

    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

    /// <inheritdoc />
    public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        IMEcosystemRegistry? registry = sp.GetService<IMEcosystemRegistry>();
        if (registry is null)
        {
            return Task.FromResult(DiagnosticResult.Pass("IMEcosystemRegistry not registered — skipping capability validation."));
        }

        var mismatches = new List<string>();

        // MultiTenant flag set but ITenantContext missing
        if (registry.Has(MCapability.MultiTenant))
        {
            ITenantContext? tenantCtx = sp.GetService<ITenantContext>();
            if (tenantCtx is null)
            {
                mismatches.Add(
                    "MCapability.MultiTenant is flagged but ITenantContext is not registered.  →  " +
                    "Fix: Call services.AddTenantContext() before AddRuleEngine() or remove the MultiTenant capability flag.");
            }
        }

        // Auth flag set but ILicenseGuard missing
        if (registry.Has(MCapability.Auth))
        {
            Type? licenseGuardType = FindTypeByName("Muonroi.Governance.License.ILicenseGuard");
            if (licenseGuardType is not null)
            {
                object? guard = sp.GetService(licenseGuardType);
                if (guard is null)
                {
                    mismatches.Add(
                        "MCapability.Auth is flagged but ILicenseGuard is not registered.  →  " +
                        "Fix: Call services.AddGovernance() or remove the Auth capability flag.");
                }
            }
        }

        DiagnosticResult result = mismatches.Count == 0
            ? DiagnosticResult.Pass($"All {registry.Active} capability flags match DI registrations.")
            : DiagnosticResult.Fail(
                $"{mismatches.Count} capability flag mismatch(es) detected.",
                fixSuggestion: string.Join(Environment.NewLine, mismatches),
                details: new Dictionary<string, object>
                {
                    ["activeCapabilities"] = registry.Active.ToString(),
                    ["mismatches"] = mismatches
                });

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
