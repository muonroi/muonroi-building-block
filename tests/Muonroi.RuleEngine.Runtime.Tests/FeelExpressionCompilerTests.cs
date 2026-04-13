using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.Runtime.Adapters;
using Muonroi.RuleEngine.Runtime.Compilation.Feel;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Covers FEEL expression compilation and adapter integration.
/// </summary>
public sealed class FeelExpressionCompilerTests
{
    /// <summary>
    /// Ensures common FEEL comparisons are compiled and executed correctly.
    /// </summary>
    [Fact]
    public void Compile_ShouldEvaluateCommonBooleanExpressions()
    {
        Func<IDictionary<string, object>, bool> compiled = FeelExpressionCompiler.Compile("amount > 100 and status in ('vip', 'gold')");
        Dictionary<string, object> variables = new(StringComparer.OrdinalIgnoreCase)
        {
            ["amount"] = 150,
            ["status"] = "vip"
        };

        compiled(variables).Should().BeTrue();
    }

    /// <summary>
    /// Ensures range expressions are supported by the compiled subset.
    /// </summary>
    [Fact]
    public void Compile_ShouldEvaluateRangeExpressions()
    {
        Func<IDictionary<string, object>, bool> compiled = FeelExpressionCompiler.Compile("score in [1..10]");

        compiled(new Dictionary<string, object> { ["score"] = 7 }).Should().BeTrue();
        compiled(new Dictionary<string, object> { ["score"] = 11 }).Should().BeFalse();
    }

    /// <summary>
    /// Ensures identical expressions reuse the same cached delegate instance.
    /// </summary>
    [Fact]
    public void Compile_ShouldReuseCachedDelegate()
    {
        Func<IDictionary<string, object>, bool> first = FeelExpressionCompiler.Compile("amount > 0");
        Func<IDictionary<string, object>, bool> second = FeelExpressionCompiler.Compile("amount > 0");

        ReferenceEquals(first, second).Should().BeTrue();
    }

    /// <summary>
    /// Ensures unsupported syntax falls back to the interpreter instead of surfacing an exception.
    /// </summary>
    [Fact]
    public void Compile_ShouldFallbackToFeelEvaluatorForUnsupportedFunctions()
    {
        Func<IDictionary<string, object>, bool> compiled = FeelExpressionCompiler.Compile("name startsWith 'A'");

        compiled(new Dictionary<string, object> { ["name"] = "Adam" }).Should().BeTrue();
    }

    /// <summary>
    /// Ensures missing variables do not throw during compiled evaluation.
    /// </summary>
    [Fact]
    public void Compile_ShouldTreatMissingVariablesAsFalse()
    {
        Func<IDictionary<string, object>, bool> compiled = FeelExpressionCompiler.Compile("missing and true");

        compiled(new Dictionary<string, object>()).Should().BeFalse();
    }

    /// <summary>
    /// Ensures the FEEL adapter caches the compiled delegate after the first evaluation.
    /// </summary>
    [Fact]
    public async Task FeelRuleAdapter_ShouldCacheCompiledDelegateAfterFirstEvaluation()
    {
        ServiceCollection services = new();
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(builder => builder.AddMuonroiLogging());
        await using ServiceProvider provider = services.BuildServiceProvider();
        IMLog<FeelRuleAdapter<FeelContext>> log = provider.GetRequiredService<IMLog<FeelRuleAdapter<FeelContext>>>();
        FeelRuleAdapter<FeelContext> adapter = new(
            "FEEL_CACHE",
            "amount > 0",
            null,
            new FeelContextProjector(),
            log);

        FactBag facts = new();
        await adapter.EvaluateAsync(new FeelContext { Amount = 5 }, facts, CancellationToken.None);
        Delegate? firstDelegate = typeof(FeelRuleAdapter<FeelContext>)
            .GetField("_compiledDelegate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(adapter) as Delegate;
        await adapter.EvaluateAsync(new FeelContext { Amount = 8 }, facts, CancellationToken.None);
        Delegate? secondDelegate = typeof(FeelRuleAdapter<FeelContext>)
            .GetField("_compiledDelegate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(adapter) as Delegate;

        firstDelegate.Should().NotBeNull();
        ReferenceEquals(firstDelegate, secondDelegate).Should().BeTrue();
    }

    /// <summary>
    /// Test context used by adapter integration tests.
    /// </summary>
    public sealed class FeelContext
    {
        /// <summary>
        /// Gets or sets the amount value.
        /// </summary>
        public decimal Amount { get; set; }
    }

    private sealed class FeelContextProjector : IContextProjector<FeelContext>
    {
        public IReadOnlyDictionary<string, object?> Project(FeelContext context)
            => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = context.Amount
            };
    }
}
