namespace Muonroi.Core.Abstractions.Interfaces;

public interface IUiEngineManifestContributor
{
    int Order { get; }
    string ModuleId { get; }
    string RequiredTier { get; }

    Task ContributeAsync(UiEngineManifestContext context, CancellationToken ct = default);
}
