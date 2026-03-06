using System;
using Muonroi.RuleEngine.Runtime.Rules;

namespace Muonroi.BuildingBlock.Test;

public sealed class InMemoryRuleSetStore : IRuleSetStore
{
    private readonly Dictionary<string, string> _store = [];

    public InMemoryRuleSetStore()
    {
        const string loginWorkflow =
            "[{\"WorkflowName\":\"login\",\"Tenant\":\"default\",\"Rules\":[\"ValidateLoginInfo\",\"LoadUser\",\"CheckAccountLock\",\"VerifyPassword\",\"GenerateToken\"]}]";
        _store["login"] = loginWorkflow;
    }

    public Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default)
    {
        _store[workflowName] = json;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string workflowName, int? version = null,
        CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(workflowName, out string? json);
        return Task.FromResult(json);
    }

    public Task SetActiveVersionAsync(string workflowName, int version, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Array.Empty<int>());
    }
}
