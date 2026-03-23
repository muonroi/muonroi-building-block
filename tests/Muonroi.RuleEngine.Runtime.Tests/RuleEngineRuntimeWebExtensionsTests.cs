using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Tracing;
using Muonroi.RuleEngine.Runtime.Web;
using Muonroi.RuleEngine.Runtime.Web.Contributors;
using Muonroi.RuleEngine.Runtime.Web.Services;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleEngineRuntimeWebExtensionsTests
{
    [Fact]
    public void AddRuleEngineRuntimeWeb_ShouldRegisterRuntimeWebServices()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            ServiceCollection services = new();
            LicenseState state = new()
            {
                IsValid = true,
                Tier = LicenseTier.Licensed
            };
            services.AddSingleton(state);
            services.AddSingleton(new LicenseRuntimeStatus());
            services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
            services.AddSingleton<IOptions<MemoryCacheOptions>>(Options.Create(new MemoryCacheOptions()));

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RuleStore:RootPath"] = root,
                    ["RuleStore:EnableRuntimeCache"] = "false",
                    ["RuleTracing:TraceKeyPrefix"] = "runtime-web-tests",
                    ["RuleTracing:DebuggerKeyPrefix"] = "runtime-web-debugger"
                })
                .Build();

            services.AddRuleEngineRuntimeWeb(configuration);

            ServiceProvider provider = services.BuildServiceProvider();

            provider.GetRequiredService<IRuleDryRunService>().Should().BeOfType<RuleDryRunService>();
            provider.GetRequiredService<IMRuleFlowContractProvider>().Should().BeOfType<MDefaultRuleFlowContractProvider>();
            provider.GetRequiredService<MRuleAuthoringManifestRegistry>().Should().NotBeNull();
            RuleTracingOptions options = provider.GetRequiredService<IOptions<RuleTracingOptions>>().Value;
            options.TraceKeyPrefix.Should().Be("runtime-web-tests");
            options.DebuggerKeyPrefix.Should().Be("runtime-web-debugger");

            services.Should().Contain(x =>
                x.ServiceType == typeof(IUiEngineManifestContributor) &&
                x.ImplementationType == typeof(RuntimeRuleSetManifestContributor));
            services.Should().Contain(x =>
                x.ServiceType == typeof(IHostedService) &&
                x.ImplementationType == typeof(RuleSetHubNotifier));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
