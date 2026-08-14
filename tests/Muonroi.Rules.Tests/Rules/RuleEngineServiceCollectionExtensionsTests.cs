namespace Muonroi.Rules.Tests.Rules;

public sealed class RuleEngineServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRuleEngineStore_WithoutLicenseState_Throws()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Action act = () => services.AddRuleEngineStore(configuration);

        act.Should().Throw<LicenseException>()
            .WithMessage("*LicenseState is not registered*");
    }

    [Fact]
    public async Task AddRuleEngineStore_WithLicensedState_RegistersFileStoreAndInMemoryNotifier()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rules-di-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            ServiceCollection services = new();
            services.AddSingleton(CreateLicensedRuleEngineState());
            services.AddSingleton(Substitute.For<IMJsonSerializeService>());
            services.AddSingleton<IOptions<MemoryCacheOptions>>(Options.Create(new MemoryCacheOptions()));

            IHostEnvironment environment = Substitute.For<IHostEnvironment>();
            environment.ContentRootPath.Returns(root);
            services.AddSingleton(environment);

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RuleStore:RootPath"] = "custom-rules",
                    ["RuleStore:UseContentRoot"] = "true"
                })
                .Build();

            services.AddRuleEngineStore(configuration);

            ServiceProvider provider = services.BuildServiceProvider();
            provider.GetRequiredService<IRuleSetChangeNotifier>().Should().BeOfType<InMemoryRuleSetChangeNotifier>();

            IRuleSetStore store = provider.GetRequiredService<IRuleSetStore>();
            await store.SaveAsync("workflow-a", """{"WorkflowName":"workflow-a","Rules":[{"RuleName":"R1","Expression":"true","RuleExpressionType":0}]}""");

            string savedPath = Path.Combine(root, "custom-rules", "default", "workflow-a", "v1.json");
            File.Exists(savedPath).Should().BeTrue();
            provider.GetRequiredService<RulesEngineService>().Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static LicenseState CreateLicensedRuleEngineState()
    {
        return new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.RuleEngine]
        };
    }
}
