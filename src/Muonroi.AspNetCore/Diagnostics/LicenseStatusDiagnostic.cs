namespace Muonroi.AspNetCore.Diagnostics;

/// <summary>
/// Warning diagnostic that checks the validity and expiry of the registered license.
/// Skips gracefully when <see cref="ILicenseGuard"/> is not registered (standalone mode).
/// </summary>
public sealed class LicenseStatusDiagnostic : IMEcosystemDiagnostic
{
    /// <inheritdoc />
    public string Name => "LicenseStatusDiagnostic";

    /// <inheritdoc />
    public DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

    /// <inheritdoc />
    public Task<DiagnosticResult> CheckAsync(IServiceProvider sp, CancellationToken ct)
    {
        ILicenseGuard? guard = sp.GetService<ILicenseGuard>();
        if (guard is null)
        {
            return Task.FromResult(DiagnosticResult.Pass("ILicenseGuard not registered — running in standalone (free) mode."));
        }

        try
        {
            LicenseState state = guard.Current;

            if (!state.IsValid)
            {
                return Task.FromResult(DiagnosticResult.Fail(
                    "License is not valid. The application will run in degraded mode.",
                    fixSuggestion: "Check your license key configuration and ensure the license server is reachable. " +
                                   "Run services.AddGovernance() with a valid license key.",
                    details: new Dictionary<string, object>
                    {
                        ["tier"] = guard.Tier.ToString(),
                        ["isFreeMode"] = guard.IsFreeMode
                    }));
            }

            return Task.FromResult(DiagnosticResult.Pass(
                $"License is valid. Tier: {guard.Tier}. Free mode: {guard.IsFreeMode}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(DiagnosticResult.Fail(
                $"License check threw an exception: {ex.Message}",
                fixSuggestion: "Ensure the license guard is correctly initialized. Check license key format and expiry.",
                details: new Dictionary<string, object>
                {
                    ["exceptionType"] = ex.GetType().Name,
                    ["exceptionMessage"] = ex.Message
                }));
        }
    }
}
