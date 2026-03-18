namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Broadcasts proliferation lifecycle events (e.g. via SignalR).
/// </summary>
public interface IProliferationChangeNotifier
{
    Task NotifyProliferationTriggeredAsync(string workflowName, int scenarioCount, CancellationToken ct = default);
    Task NotifyScenarioCompletedAsync(string scenarioId, bool isSuccess, CancellationToken ct = default);
}
