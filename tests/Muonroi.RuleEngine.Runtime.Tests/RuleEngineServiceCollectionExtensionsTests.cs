using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.EntityFrameworkCore;
using Muonroi.RuleEngine.Runtime.Rules;
using NSubstitute;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleEngineServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddRuleEngineStore_RegistersFileBackedServicesAndUsesContentRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-runtime-di-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            ServiceCollection services = new();
            IHostEnvironment environment = Substitute.For<IHostEnvironment>();
            environment.ContentRootPath.Returns(root);
            services.AddSingleton(environment);
            services.AddSingleton<IOptions<MemoryCacheOptions>>(Options.Create(new MemoryCacheOptions()));

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RuleStore:RootPath"] = "runtime-rules",
                    ["RuleStore:UseContentRoot"] = "true",
                    ["RuleStore:EnableRuntimeCache"] = "true",
                    ["RuleControlPlane:RequireApproval"] = "true",
                    ["RuleControlPlane:NotifyOnStateChange"] = "false"
                })
                .Build();

            services.AddRuleEngineStore(configuration);

            ServiceProvider provider = services.BuildServiceProvider();
            provider.GetRequiredService<IRuleSetStore>().Should().BeOfType<FileRuleSetStore>();
            provider.GetRequiredService<IRuleSetAuditStore>().Should().BeOfType<FileRuleSetAuditStore>();
            provider.GetRequiredService<IRuleSetChangeNotifier>().Should().BeOfType<InMemoryRuleSetChangeNotifier>();
            provider.GetRequiredService<IRuleSetRuntimeCache>().Should().BeOfType<RuleSetRuntimeCache>();
            provider.GetRequiredService<IRuleSetDefinitionValidator>().Should().BeOfType<RuleSetDefinitionValidator>();
            provider.GetService(typeof(Muonroi.RuleEngine.Runtime.Adapters.RuleGraphParser)).Should().NotBeNull();

            RuleControlPlaneOptions controlPlane = provider.GetRequiredService<IOptions<RuleControlPlaneOptions>>().Value;
            controlPlane.RequireApproval.Should().BeTrue();
            controlPlane.NotifyOnStateChange.Should().BeFalse();

            IRuleSetStore store = provider.GetRequiredService<IRuleSetStore>();
            await store.SaveAsync("wf-runtime", "{\"workflowName\":\"wf-runtime\",\"rules\":[\"RULE_A\"]}");

            string savedPath = Path.Combine(root, "runtime-rules", "default", "wf-runtime", "v1.json");
            File.Exists(savedPath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void AddMRuleEngineApprovalWorkflow_EnablesApprovalAndRegistersService()
    {
        ServiceCollection services = new();

        services.AddMRuleEngineApprovalWorkflow();

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<RuleControlPlaneOptions>>().Value.RequireApproval.Should().BeTrue();
        services.Should().Contain(x => x.ServiceType == typeof(IRuleSetApprovalService));
    }

    [Fact]
    public void AddMCanaryRollout_EnablesCanaryAndRegistersService()
    {
        ServiceCollection services = new();

        services.AddMCanaryRollout();

        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<RuleControlPlaneOptions>>().Value.EnableCanary.Should().BeTrue();
        services.Should().Contain(x => x.ServiceType == typeof(ICanaryRolloutService));
    }

    [Fact]
    public void AddMRuleEngineWithPostgres_WithoutLicenseState_Throws()
    {
        ServiceCollection services = new();

        Action act = () => services.AddMRuleEngineWithPostgres("Host=localhost;Database=test;Username=u;Password=p");

        act.Should().Throw<LicenseException>()
            .WithMessage("*LicenseState is not registered*");
    }

    [Fact]
    public void AddMRuleEngineWithPostgres_WithLicensedState_RegistersCoreRuntimeServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.RuleEngine]
        });

        services.AddMRuleEngineWithPostgres(
            "Host=localhost;Database=test;Username=u;Password=p",
            options =>
            {
                options.RequireApproval = true;
                options.EnableCanary = true;
            });

        services.Should().Contain(x => x.ServiceType == typeof(IRuleSetStore));
        services.Should().Contain(x => x.ServiceType == typeof(IRuleSetAuditStore));
        services.Should().Contain(x => x.ServiceType == typeof(IRuleSetApprovalService));
        services.Should().Contain(x => x.ServiceType == typeof(ICanaryRolloutService));
        services.Should().Contain(x => x.ServiceType == typeof(RulesEngineService));
    }
}
