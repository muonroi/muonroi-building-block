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
    /// Regression: nested fields from a JSON-deserialized <c>Dictionary&lt;string, object?&gt;</c>
    /// context (the dry-run / dynamic flow-graph path) arrive as <see cref="System.Text.Json.JsonElement"/>.
    /// Nested member access (<c>vgm.isVgm</c>, <c>vgm.wgt</c>) must resolve to native CLR values so a
    /// nested boolean condition evaluates correctly. Before the <c>UnwrapJsonElement</c> fix in
    /// <see cref="FeelRuleAdapter{TContext}"/>, the nested JsonElement could not be navigated and
    /// <c>vgm.isVgm</c> resolved to null → <c>vgm.isVgm = true</c> was false even when isVgm was true.
    /// </summary>
    [Fact]
    public async Task FeelRuleAdapter_ShouldResolveNestedFieldsFromJsonElementContext()
    {
        ServiceCollection services = new();
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(builder => builder.AddMuonroiLogging());
        await using ServiceProvider provider = services.BuildServiceProvider();
        IMLog<FeelRuleAdapter<Dictionary<string, object?>>> log =
            provider.GetRequiredService<IMLog<FeelRuleAdapter<Dictionary<string, object?>>>>();

        // Mirrors RuleDryRunService: object? values deserialize as JsonElement, nested object as JsonElement(Object).
        Dictionary<string, object?> ctx = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(
            "{\"vgm\":{\"isVgm\":true,\"wgt\":29990},\"maxGross\":30000}")!;

        FeelRuleAdapter<Dictionary<string, object?>> passAdapter = new(
            "vgm-check",
            "vgm.isVgm = true and vgm.wgt <= maxGross",
            null,
            new ReflectionContextProjector<Dictionary<string, object?>>(),
            log);

        RuleResult pass = await passAdapter.EvaluateAsync(ctx, new FactBag(), CancellationToken.None);
        pass.IsSuccess.Should().BeTrue(
            because: "nested boolean vgm.isVgm and nested number vgm.wgt must resolve from a JsonElement-backed Dictionary context");

        // Negative guard: proves vgm.wgt resolves to the REAL number, not null. Before the fix
        // vgm.wgt was null and "null <= 100" was true (false positive masking the nested bug);
        // after the fix 29990 <= 100 is correctly false.
        FeelRuleAdapter<Dictionary<string, object?>> failAdapter = new(
            "vgm-check-tight",
            "vgm.wgt <= 100",
            null,
            new ReflectionContextProjector<Dictionary<string, object?>>(),
            log);

        RuleResult fail = await failAdapter.EvaluateAsync(ctx, new FactBag(), CancellationToken.None);
        fail.IsSuccess.Should().BeFalse(
            because: "vgm.wgt (29990) must resolve to the real number so 29990 <= 100 is false (not a null-comparison false positive)");
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
