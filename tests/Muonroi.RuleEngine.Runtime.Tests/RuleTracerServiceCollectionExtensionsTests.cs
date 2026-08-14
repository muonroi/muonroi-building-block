namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleTracerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRuleEngineTracing_NullServices_Throws()
    {
        Action action = () => RuleTracerServiceCollectionExtensions.AddRuleEngineTracing(null!);

        action.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void AddRuleEngineTracing_RegistersTracingServices_AndAppliesOptions()
    {
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<IMJsonSerializeService>());

        services.AddRuleEngineTracing(options => options.DefaultTtl = TimeSpan.FromMinutes(45));
        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IRuleTraceStore>().Should().BeOfType<NullRuleTraceStore>();
        provider.GetRequiredService<IRuleDebuggerModeService>().Should().BeOfType<NullRuleDebuggerModeService>();
        provider.GetRequiredService<IRuleExecutionTracer>().Should().BeOfType<RuleExecutionTracer>();
        provider.GetRequiredService<IOptions<RuleTracingOptions>>()
            .Value.DefaultTtl.Should().Be(TimeSpan.FromMinutes(45));
    }
}
