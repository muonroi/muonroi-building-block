using Microsoft.Extensions.Hosting;

namespace Muonroi.Governance.License;

internal sealed class CommercialFeatureStartupValidationHostedService(
    LicenseState state,
    LicenseRuntimeStatus runtimeStatus,
    LicenseTier minimumTier,
    string featureName) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        runtimeStatus.InitializeFromProof(state.ActivationProof);
        ProofTierAccessor accessor = new(state, runtimeStatus);
        accessor.RequireMinimumTier(minimumTier, featureName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
