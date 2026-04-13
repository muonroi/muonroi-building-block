using Muonroi.AuthZ.HotReload;
using Muonroi.RuleEngine.Runtime.Rules;

namespace Muonroi.AuthZ.Tests;

public class DefaultAuthRuleChangeHandlerTests
{
    [Fact]
    public async Task OnAuthRuleChangedAsync_WithRuntimeCache_CallsInvalidate()
    {
        // Arrange
        RecordingRuntimeCache cache = new();
        DefaultAuthRuleChangeHandler handler = new(cache);
        Guid ruleSetId = Guid.NewGuid();

        // Act
        await handler.OnAuthRuleChangedAsync(ruleSetId);

        // Assert
        Assert.Single(cache.InvalidationCalls);
        (string tenantId, string workflowName) = cache.InvalidationCalls[0];
        Assert.Equal(string.Empty, tenantId);
        Assert.Equal("auth/", workflowName);
    }

    [Fact]
    public async Task OnAuthRuleChangedAsync_WithoutRuntimeCache_DoesNotThrow()
    {
        // Arrange
        DefaultAuthRuleChangeHandler handler = new(runtimeCache: null);
        Guid ruleSetId = Guid.NewGuid();

        // Act & Assert — should not throw
        await handler.OnAuthRuleChangedAsync(ruleSetId);
    }

    [Fact]
    public async Task OnAuthRuleChangedAsync_WithRuntimeCache_CallsInvalidateOncePerInvocation()
    {
        // Arrange
        RecordingRuntimeCache cache = new();
        DefaultAuthRuleChangeHandler handler = new(cache);

        // Act — invoke twice
        await handler.OnAuthRuleChangedAsync(Guid.NewGuid());
        await handler.OnAuthRuleChangedAsync(Guid.NewGuid());

        // Assert — both invocations trigger invalidation
        Assert.Equal(2, cache.InvalidationCalls.Count);
    }

    private sealed class RecordingRuntimeCache : IRuleSetRuntimeCache
    {
        public List<(string TenantId, string WorkflowName)> InvalidationCalls { get; } = [];

        public Task<string?> GetOrCreateAsync(
            string tenantId,
            string workflowName,
            Func<Task<string?>> factory,
            CancellationToken cancellationToken = default)
        {
            return factory();
        }

        public Task InvalidateAsync(string tenantId, string workflowName, CancellationToken cancellationToken = default)
        {
            InvalidationCalls.Add((tenantId, workflowName));
            return Task.CompletedTask;
        }
    }
}
