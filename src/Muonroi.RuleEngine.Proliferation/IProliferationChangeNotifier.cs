namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Broadcasts proliferation lifecycle events (e.g. via SignalR).
/// </summary>
public interface IProliferationChangeNotifier
{
    /// <summary>Announces that proliferation has been triggered for a workflow.</summary>
    Task NotifyProliferationTriggeredAsync(string workflowName, int scenarioCount, CancellationToken ct = default);
    /// <summary>Announces that a scenario finished executing.</summary>
    Task NotifyScenarioCompletedAsync(string scenarioId, bool isSuccess, CancellationToken ct = default);
}
