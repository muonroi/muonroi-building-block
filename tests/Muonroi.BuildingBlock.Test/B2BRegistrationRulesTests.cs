using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class B2BRegistrationRulesTests
{
    private record B2BRegistrationContext(string TaxCode, string DeclaredName, string DeclaredIndustry);

    private record CompanyInfo(string TaxCode, string Name, string IndustryCode);

    private interface ITaxAuthorityClient
    {
        Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default);
        Task<CompanyInfo?> GetCompanyInfoAsync(string taxCode, CancellationToken cancellationToken = default);
    }

    private interface IFraudCheckClient
    {
        Task<bool> IsBlacklistedAsync(string taxCode, CancellationToken cancellationToken = default);
    }

    private interface IIndustryClassifier
    {
        Task<bool> IsRestrictedAsync(string industryCode, CancellationToken cancellationToken = default);
    }

    private sealed class StubTaxAuthorityClient : ITaxAuthorityClient
    {
        public bool Exists { get; set; }
        public CompanyInfo? Info { get; set; }

        public Task<bool> TaxCodeExistsAsync(string taxCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Exists);
        }

        public Task<CompanyInfo?> GetCompanyInfoAsync(string taxCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Info);
        }
    }

    private sealed class StubFraudCheckClient : IFraudCheckClient
    {
        public bool Blacklisted { get; set; }

        public Task<bool> IsBlacklistedAsync(string taxCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Blacklisted);
        }
    }

    private sealed class StubIndustryClassifier : IIndustryClassifier
    {
        public bool Restricted { get; set; }

        public Task<bool> IsRestrictedAsync(string industryCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Restricted);
        }
    }

    private sealed class TaxCodeExistsRule(ITaxAuthorityClient client) : IRule<B2BRegistrationContext>
    {
        public const string RuleCode = "TaxCodeExists";
        public string Name => RuleCode;
        public string Code => RuleCode;
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => [];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(B2BRegistrationContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            bool exists = await client.TaxCodeExistsAsync(context.TaxCode, cancellationToken);
            facts["tax_code_exists"] = exists;
            return exists ? RuleResult.Passed() : RuleResult.Failure("Tax code not found");
        }

        public Task ExecuteAsync(B2BRegistrationContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CompanyInfoMatchRule(ITaxAuthorityClient client) : IRule<B2BRegistrationContext>
    {
        public const string RuleCode = "CompanyInfoMatch";
        public string Name => RuleCode;
        public string Code => RuleCode;
        public int Order => 2;
        public IReadOnlyList<string> DependsOn => [TaxCodeExistsRule.RuleCode];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(B2BRegistrationContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            CompanyInfo? info = await client.GetCompanyInfoAsync(context.TaxCode, cancellationToken);
            if (info is null || !string.Equals(info.Name, context.DeclaredName, StringComparison.OrdinalIgnoreCase))
                return RuleResult.Failure("Declared data does not match official records");
            facts["company_info"] = info;
            return RuleResult.Passed();
        }

        public Task ExecuteAsync(B2BRegistrationContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BlacklistRule(IFraudCheckClient client) : IRule<B2BRegistrationContext>
    {
        public const string RuleCode = "Blacklist";
        public string Name => RuleCode;
        public string Code => RuleCode;
        public int Order => 3;
        public IReadOnlyList<string> DependsOn => [CompanyInfoMatchRule.RuleCode];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(B2BRegistrationContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            if (!facts.TryGet("company_info", out CompanyInfo? info) || info is null)
                return RuleResult.Failure("Missing company info");

            bool blacklisted = await client.IsBlacklistedAsync(info.IndustryCode, cancellationToken);
            return blacklisted ? RuleResult.Failure("Industry is restricted") : RuleResult.Passed();
        }

        public Task ExecuteAsync(B2BRegistrationContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class IndustryRestrictionRule(IIndustryClassifier client) : IRule<B2BRegistrationContext>
    {
        public const string RuleCode = "IndustryRestriction";
        public string Name => RuleCode;
        public string Code => RuleCode;
        public int Order => 4;
        public IReadOnlyList<string> DependsOn => [CompanyInfoMatchRule.RuleCode];
        public IEnumerable<Type> Dependencies => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(B2BRegistrationContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            if (!facts.TryGet("company_info", out CompanyInfo? info) || info is null)
                return RuleResult.Failure("Missing company info");

            bool restricted = await client.IsRestrictedAsync(info.IndustryCode, cancellationToken);
            return restricted ? RuleResult.Failure("Industry is restricted") : RuleResult.Passed();
        }

        public Task ExecuteAsync(B2BRegistrationContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_Success_WhenAllRulesPass()
    {
        StubTaxAuthorityClient tax = new()
        {
            Exists = true,
            Info = new CompanyInfo("123", "ABC", "IT")
        };
        StubFraudCheckClient fraud = new()
        {
            Blacklisted = false
        };
        StubIndustryClassifier industry = new()
        {
            Restricted = false
        };

        IRule<B2BRegistrationContext>[] rules =
        [
            new TaxCodeExistsRule(tax),
            new CompanyInfoMatchRule(tax),
            new BlacklistRule(fraud),
            new IndustryRestrictionRule(industry)
        ];

        RuleOrchestrator<B2BRegistrationContext> orchestrator = new(
            rules,
            [],
            NullLogger<RuleOrchestrator<B2BRegistrationContext>>.Instance
        );

        B2BRegistrationContext ctx = new("123", "ABC", "IT");
        FactBag facts = await orchestrator.ExecuteAsync(ctx);

        Assert.True((bool?)facts["tax_code_exists"]);
        Assert.True(facts.TryGet("company_info", out CompanyInfo? info) && info is not null && info.Name == "ABC");
    }

    [Fact]
    public async Task ExecuteAsync_Fails_WhenIndustryRestricted()
    {
        StubTaxAuthorityClient tax = new()
        {
            Exists = true,
            Info = new CompanyInfo("123", "ABC", "R1")
        };
        StubFraudCheckClient fraud = new()
        {
            Blacklisted = false
        };
        StubIndustryClassifier industry = new()
        {
            Restricted = true
        };

        IRule<B2BRegistrationContext>[] rules =
        [
            new TaxCodeExistsRule(tax),
            new CompanyInfoMatchRule(tax),
            new BlacklistRule(fraud),
            new IndustryRestrictionRule(industry)
        ];

        RuleOrchestrator<B2BRegistrationContext> orchestrator = new(
            rules,
            [],
            NullLogger<RuleOrchestrator<B2BRegistrationContext>>.Instance
        );

        B2BRegistrationContext ctx = new("123", "ABC", "R1");

        await Assert.ThrowsAsync<MInternalException>(() => orchestrator.ExecuteAsync(ctx));
    }
}
