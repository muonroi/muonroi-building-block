namespace Muonroi.RuleEngine.Core.Runtime;

/// <summary>
/// Schedules activations based on priority and group halting.
/// </summary>
public sealed class AgendaScheduler(IEnumerable<Activation> activations)
{
    private readonly List<Activation> _activations = [.. activations];
    private readonly HashSet<string> _haltedGroups = [];

    /// <summary>
    /// Fires all activations in priority order.
    /// </summary>
    public async Task FireAsync(CancellationToken cancellationToken = default)
    {
        foreach (Activation? activation in _activations.OrderByDescending(a => a.Priority))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (activation.Group != null && _haltedGroups.Contains(activation.Group)) continue;

            RuleContext ctx = new(this, activation.Group);
            await activation.Action(ctx);
        }
    }

    private void HaltGroup(string? group)
    {
        if (!string.IsNullOrEmpty(group)) _haltedGroups.Add(group);
    }

    private sealed class RuleContext(AgendaScheduler scheduler, string? group) : IRuleContext
    {
        public void HaltGroup()
        {
            scheduler.HaltGroup(group);
        }
    }
}