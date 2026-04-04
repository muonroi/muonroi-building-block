using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class RulesEngineServiceSecurityTests
{
    [Fact]
    public async Task SaveRuleSetAsync_WorkflowNameMismatch_ShouldThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "other-workflow",
                                "Rules": [ "R1" ]
                              }
                            ]
                            """;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SaveRuleSetAsync("expected-workflow", json));
    }

    [Fact]
    public async Task SaveRuleSetAsync_EmptyRulesArray_ShouldThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "wf",
                                "Rules": []
                              }
                            ]
                            """;

        await Assert.ThrowsAsync<InvalidDataException>(() => service.SaveRuleSetAsync("wf", json));
    }

    [Fact]
    public async Task ExecuteAsync_RuleEngineFeatureNotLicensed_ShouldThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);
        RulesEngineService service = new(store, null, new DenyRuleEngineGuard());

        await Assert.ThrowsAsync<MInternalException>(() => service.ExecuteAsync("login", new object()));
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflowWithUnknownRule_ShouldThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "wf",
                                "Rules": [ "RuleDoesNotExist" ]
                              }
                            ]
                            """;
        await service.SaveRuleSetAsync("wf", json);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync("wf", new SecurityRuleContext()));
    }

    [Fact]
    public async Task ExecuteAsync_CodeWorkflowWithAmbiguousRuleCode_ShouldThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        FileRuleSetStore store = new(root);
        RulesEngineService service = new(store);

        const string json = """
                            [
                              {
                                "WorkflowName": "wf",
                                "Rules": [ "DuplicateRuleCode" ]
                              }
                            ]
                            """;
        await service.SaveRuleSetAsync("wf", json);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync("wf", new SecurityRuleContext()));
    }

    private sealed class SecurityRuleContext;

    private sealed class DuplicateRuleA : IRule<SecurityRuleContext>
    {
        public string Code => "DuplicateRuleCode";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(SecurityRuleContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(SecurityRuleContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DuplicateRuleB : IRule<SecurityRuleContext>
    {
        public string Code => "DuplicateRuleCode";
        public int Order => 2;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => Code;
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(SecurityRuleContext ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(SecurityRuleContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class DenyRuleEngineGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();

        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.RuleEngine, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
                throw new InvalidOperationException("rule-engine feature is not licensed");
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken()
        {
            return "test";
        }

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("k", encryptedData);
        }
    }
}
