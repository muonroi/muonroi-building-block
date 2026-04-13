namespace Muonroi.BuildingBlock.Test;

public class RuleEngineTests
{
    private sealed class TestRule<T>(RuleType type, string code = "default") : IRule<T>
    {
        public RuleType Type { get; } = type;

        public string Code { get; } = code;

        public bool Executed { get; private set; }

        public int Order => throw new NotImplementedException();

        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();

        public HookPoint HookPoint => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public void Reset()
        {
            Executed = false;
        }

        public Task ExecuteAsync(T context, CancellationToken cancellationToken = default)
        {
            Executed = true;
            return Task.CompletedTask;
        }

        public Task<RuleResult> EvaluateAsync(T ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class AlwaysOffStrategy : IRuleActivationStrategy<object>
    {
        public bool IsActive(IRule<object> rule, object context)
        {
            return false;
        }
    }

    private sealed class TenantContextModel : ITenantScoped
    {
        public string? TenantId { get; init; }
    }

    [Fact]
    public async Task ExecuteAsync_Throws_For_Cross_Tenant()
    {
        RuleEngine<TenantContextModel> engine =
            new RuleEngine<TenantContextModel>().AddRule(new TestRule<TenantContextModel>(RuleType.Validation));
        TenantContext.CurrentTenantId = "t1";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            engine.ExecuteAsync(new TenantContextModel { TenantId = "t2" }));
    }

    [Fact]
    public async Task ExecuteAsync_Allows_Same_Tenant()
    {
        TestRule<TenantContextModel> rule = new(RuleType.Validation);
        RuleEngine<TenantContextModel> engine = new RuleEngine<TenantContextModel>().AddRule(rule);
        TenantContext.CurrentTenantId = "t1";
        await engine.ExecuteAsync(new TenantContextModel { TenantId = "t1" });
        Assert.True(rule.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_RunsOnlySpecifiedTypes()
    {
        TestRule<object> validation = new(RuleType.Validation, "validation");
        TestRule<object> business = new(RuleType.Business, "business");

        RuleEngine<object> engine = new RuleEngine<object>()
            .AddRule(validation, new RuleDescriptor("VAL", "Validation", string.Empty, RuleType.Validation))
            .AddRule(business, new RuleDescriptor("BUS", "Business", string.Empty, RuleType.Business));

        await engine.ExecuteAsync(new object(), RuleType.Validation);

        Assert.True(validation.Executed);
        Assert.False(business.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_RunsAllWhenNoTypeSpecified()
    {
        TestRule<object> validation = new(RuleType.Validation, "validation");
        TestRule<object> business = new(RuleType.Business, "business");

        RuleEngine<object> engine = new RuleEngine<object>()
            .AddRule(validation, new RuleDescriptor("VAL", "Validation", string.Empty, RuleType.Validation))
            .AddRule(business, new RuleDescriptor("BUS", "Business", string.Empty, RuleType.Business));

        await engine.ExecuteAsync(new object());

        Assert.True(validation.Executed);
        Assert.True(business.Executed);
    }

    private static readonly int[] Expected = [1, 2];

    [Fact]
    public async Task ExecuteAsync_RespectsDependencies()
    {
        List<int> order = [];
        OrderedRule ruleA = new(1, order);
        OrderedRule ruleB = new(2, order);

        RuleEngine<object> engine = new RuleEngine<object>()
            .AddRule(ruleA, new RuleDescriptor("A", "RuleA", string.Empty, RuleType.Business, 0))
            .AddRule(ruleB, new RuleDescriptor("B", "RuleB", string.Empty, RuleType.Business, 1, ["A"]));

        await engine.ExecuteAsync(new object(), RuleType.Business);

        Assert.Equal(Expected, order);
    }

    private sealed class OrderedRule(int id, List<int> order) : IRule<object>
    {
        public RuleType Type => RuleType.Business;

        public string Code => id.ToString();

        public int Order => throw new NotImplementedException();

        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();

        public HookPoint HookPoint => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public Task<RuleResult> EvaluateAsync(object ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync(object context, CancellationToken cancellationToken = default)
        {
            order.Add(id);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_AwaitsAsyncRulesSequentially()
    {
        List<int> order = [];
        AsyncOrderedRule ruleA = new(1, order);
        AsyncOrderedRule ruleB = new(2, order);

        RuleEngine<object> engine = new RuleEngine<object>()
            .AddRule(ruleA, new RuleDescriptor("A", "RuleA", string.Empty, RuleType.Business, 0))
            .AddRule(ruleB, new RuleDescriptor("B", "RuleB", string.Empty, RuleType.Business, 1));

        await engine.ExecuteAsync(new object(), RuleType.Business);

        Assert.Equal(Expected, order);
    }

    private sealed class AsyncOrderedRule(int id, List<int> order) : IRule<object>
    {
        public RuleType Type => RuleType.Business;

        public string Code => id.ToString();

        public int Order => throw new NotImplementedException();

        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();

        public HookPoint HookPoint => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public Task<RuleResult> EvaluateAsync(object ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteAsync(object context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            order.Add(id);
        }
    }

    [Fact]
    public void GetCatalog_ReturnsRegisteredDescriptors()
    {
        TestRule<object> rule = new(RuleType.Validation, "T");
        RuleDescriptor descriptor = new("T", "Test", "desc", RuleType.Validation);
        RuleEngine<object> engine = new RuleEngine<object>().AddRule(rule, descriptor);

        RuleDescriptor catalog = engine.GetCatalog().Single();

        Assert.Equal("T", catalog.Code);
        Assert.Equal("Test", catalog.Name);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; private set; } = value;

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<T, string> listener)
        {
            return NullDisposable.Instance;
        }

        public void Update(T value)
        {
            CurrentValue = value;
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_HonorsOptionsMonitor()
    {
        TestRule<object> validation = new(RuleType.Validation, "validation");
        TestRule<object> business = new(RuleType.Business, "business");

        RuleOptions value = new()
        {
            RuleToggles = []
        };
        value.RuleToggles["business"] = false;
        TestOptionsMonitor<RuleOptions> monitor = new(value);

        RuleEngine<object> engine = new RuleEngine<object>(monitor)
            .AddRule(validation)
            .AddRule(business);

        await engine.ExecuteAsync(new object());

        Assert.True(validation.Executed);
        Assert.False(business.Executed);

        validation.Reset();
        business.Reset();

        monitor.Update(new RuleOptions()); // enable all

        await engine.ExecuteAsync(new object());

        Assert.True(validation.Executed);
        Assert.True(business.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_MergesSelectedRuleCodes()
    {
        TestRule<object> first = new(RuleType.Validation, "first");
        TestRule<object> second = new(RuleType.Validation, "second");
        RuleEngine<object> engine = new RuleEngine<object>()
            .AddRule(first)
            .AddRule(second);

        await engine.ExecuteAsync(new object(), ["second"]);

        Assert.False(first.Executed);
        Assert.True(second.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsInactiveRules()
    {
        TestRule<object> rule = new(RuleType.Validation);
        RuleEngine<object> engine = new RuleEngine<object>(activation: new AlwaysOffStrategy())
            .AddRule(rule);

        await engine.ExecuteAsync(new object());

        Assert.False(rule.Executed);
    }

    [Fact]
    public async Task ExecuteAsync_Honors_Tenant_Toggles()
    {
        TestRule<object> rule = new(RuleType.Validation, "r1");
        RuleOptions value = new()
        {
            TenantRuleToggles = []
        };
        value.TenantRuleToggles["t1"] = new() { ["r1"] = false };
        TestOptionsMonitor<RuleOptions> monitor = new(value);

        RuleEngine<object> engine = new RuleEngine<object>(monitor).AddRule(rule);

        TenantContext.CurrentTenantId = "t1";
        await engine.ExecuteAsync(new object());
        Assert.False(rule.Executed);

        rule.Reset();
        TenantContext.CurrentTenantId = "t2";
        await engine.ExecuteAsync(new object());
        Assert.True(rule.Executed);
        TenantContext.CurrentTenantId = null;
    }
}
