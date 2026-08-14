namespace Muonroi.RuleEngine.Abstractions.Tests;

public class OrchestratorResultTests
{
    [Fact]
    public void Success_sets_is_success_true()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Success(ExecutionMode.BestEffort, facts, results);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Success_sets_execution_mode()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Success(ExecutionMode.CompensateOnFailure, facts, results);

        // Assert
        Assert.Equal(ExecutionMode.CompensateOnFailure, result.ExecutionMode);
    }

    [Fact]
    public void Success_sets_facts_reference()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Success(ExecutionMode.AllOrNothing, facts, results);

        // Assert
        Assert.Same(facts, result.Facts);
    }

    [Fact]
    public void Success_sets_rule_results_reference()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Success(ExecutionMode.AllOrNothing, facts, results);

        // Assert
        Assert.Same(results, result.RuleResults);
    }

    [Fact]
    public void Success_sets_errors_empty()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Success(ExecutionMode.AllOrNothing, facts, results);

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Success_sets_compensation_errors_empty()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Success(ExecutionMode.AllOrNothing, facts, results);

        // Assert
        Assert.Empty(result.CompensationErrors);
    }

    [Fact]
    public void Failure_sets_is_success_false()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            new List<string> { "error" },
            new List<string>());

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Failure_sets_execution_mode()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.BestEffort,
            facts,
            results,
            new List<string>(),
            new List<string>());

        // Assert
        Assert.Equal(ExecutionMode.BestEffort, result.ExecutionMode);
    }

    [Fact]
    public void Failure_sets_facts_reference()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            new List<string>(),
            new List<string>());

        // Assert
        Assert.Same(facts, result.Facts);
    }

    [Fact]
    public void Failure_sets_rule_results_reference()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            new List<string>(),
            new List<string>());

        // Assert
        Assert.Same(results, result.RuleResults);
    }

    [Fact]
    public void Failure_sets_errors_reference()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var errors = new List<string> { "error" };

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            errors,
            new List<string>());

        // Assert
        Assert.Same(errors, result.Errors);
    }

    [Fact]
    public void Failure_sets_compensation_errors_reference()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var compensationErrors = new List<string> { "compensation" };

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            new List<string>(),
            compensationErrors);

        // Assert
        Assert.Same(compensationErrors, result.CompensationErrors);
    }

    [Fact]
    public void Failure_allows_empty_errors()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var errors = new List<string>();

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            errors,
            new List<string>());

        // Assert
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failure_allows_empty_compensation_errors()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var compensationErrors = new List<string>();

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.CompensateOnFailure,
            facts,
            results,
            new List<string>(),
            compensationErrors);

        // Assert
        Assert.Empty(result.CompensationErrors);
    }

    [Fact]
    public void Failure_preserves_null_and_empty_error_entries()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        IReadOnlyList<string> errors = new List<string> { null!, string.Empty };

        // Act
        var result = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            errors,
            new List<string>());

        // Assert
        Assert.Equal(2, result.Errors.Count);
        Assert.True(result.Errors[0] is null);
        Assert.Equal(string.Empty, result.Errors[1]);
    }

    [Fact]
    public void Record_equality_matches_same_values_and_references()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var errors = new List<string>();
        var compensationErrors = new List<string>();

        // Act
        var left = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            errors,
            compensationErrors);
        var right = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            errors,
            compensationErrors);

        // Assert
        Assert.Equal(left, right);
    }

    [Fact]
    public void Record_inequality_when_facts_reference_differs()
    {
        // Arrange
        var left = OrchestratorResult.Success(
            ExecutionMode.AllOrNothing,
            new FactBag(),
            CreateRuleResults());
        var right = OrchestratorResult.Success(
            ExecutionMode.AllOrNothing,
            new FactBag(),
            CreateRuleResults());

        // Act
        var equal = left == right;

        // Assert
        Assert.False(equal);
    }

    [Fact]
    public void Record_inequality_when_is_success_differs()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();

        // Act
        var success = OrchestratorResult.Success(ExecutionMode.AllOrNothing, facts, results);
        var failure = OrchestratorResult.Failure(
            ExecutionMode.AllOrNothing,
            facts,
            results,
            new List<string> { "error" },
            new List<string>());

        // Assert
        Assert.NotEqual(success, failure);
    }

    [Fact]
    public void With_expression_can_change_single_property()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var original = OrchestratorResult.Success(ExecutionMode.AllOrNothing, facts, results);

        // Act
        var updated = original with { ExecutionMode = ExecutionMode.BestEffort };

        // Assert
        Assert.Equal(ExecutionMode.BestEffort, updated.ExecutionMode);
        Assert.True(updated.IsSuccess);
    }

    [Fact]
    public void Success_allows_default_execution_mode()
    {
        // Arrange
        var facts = new FactBag();
        var results = CreateRuleResults();
        var mode = default(ExecutionMode);

        // Act
        var result = OrchestratorResult.Success(mode, facts, results);

        // Assert
        Assert.Equal(ExecutionMode.AllOrNothing, result.ExecutionMode);
    }

    [Fact]
    public void ICompensatableRule_inherits_irule()
    {
        // Arrange
        var interfaces = typeof(ICompensatableRule<string>).GetInterfaces();

        // Act
        var hasInterface = interfaces.Contains(typeof(IRule<string>));

        // Assert
        Assert.True(hasInterface);
    }

    [Fact]
    public void ICompensatableRule_has_expected_compensate_signature()
    {
        // Arrange
        var method = typeof(ICompensatableRule<string>).GetMethod("CompensateAsync");

        // Act
        var parameters = method!.GetParameters();

        // Assert
        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Equal(3, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal(typeof(FactBag), parameters[1].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[2].ParameterType);
    }

    [Fact]
    public void ICompensatableRule_cancellation_token_is_optional()
    {
        // Arrange
        var method = typeof(ICompensatableRule<string>).GetMethod("CompensateAsync");

        // Act
        var cancellationParam = method!.GetParameters()[2];

        // Assert
        Assert.True(cancellationParam.IsOptional);
        Assert.True(cancellationParam.HasDefaultValue);
        // Note: DefaultValue returns null for value types with = default
        Assert.Null(cancellationParam.DefaultValue);
    }

    [Fact]
    public void ICompensatableRule_is_contravariant()
    {
        // Arrange
        ICompensatableRule<object> broad = new SampleRule();

        // Act
        ICompensatableRule<string> narrow = broad;

        // Assert
        Assert.NotNull(narrow);
    }

    [Fact]
    public async Task Compensate_async_can_be_invoked()
    {
        // Arrange
        var rule = new SampleRule();
        var facts = new FactBag();

        // Act
        await rule.CompensateAsync("ctx", facts);

        // Assert
        Assert.True(rule.Compensated);
    }

    private static IReadOnlyDictionary<string, RuleResult> CreateRuleResults()
    {
        return new Dictionary<string, RuleResult>
        {
            ["R1"] = RuleResult.Passed()
        };
    }

    private sealed class SampleRule : ICompensatableRule<object>
    {
        public bool Compensated { get; private set; }

        public string Code => "SAMPLE";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public string Name => "Sample";
        public IEnumerable<Type> Dependencies => Array.Empty<Type>();

        public Task<RuleResult> EvaluateAsync(object ctx, FactBag facts, CancellationToken ct)
        {
            return Task.FromResult(RuleResult.Success());
        }

        public Task ExecuteAsync(object context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CompensateAsync(object context, FactBag facts, CancellationToken cancellationToken = default)
        {
            Compensated = true;
            facts.Set("compensated", true);
            return Task.CompletedTask;
        }
    }
}
