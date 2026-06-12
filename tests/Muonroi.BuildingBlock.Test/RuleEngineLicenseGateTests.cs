using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class RuleEngineLicenseGateTests
{
    private sealed class DummyRule : IRule<object>
    {
        public string Code => "Dummy";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;
        public string Name => "Dummy";
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(object ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Passed());
        }

        public Task ExecuteAsync(object context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenRuleEngineNotLicensed_ShouldThrow()
    {
        RuleEngine<object> engine = new(licenseGuard: new DenyRuleEngineGuard());
        engine.AddRule(new DummyRule());

        await Assert.ThrowsAsync<MInternalException>(() => engine.ExecuteAsync(new object()));
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
