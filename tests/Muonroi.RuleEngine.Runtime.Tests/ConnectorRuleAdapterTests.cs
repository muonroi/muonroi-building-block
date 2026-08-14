namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class ConnectorRuleAdapterTests
{
    [Fact]
    public async Task EvaluateAsync_WhenConnectorMissing_ReturnsFailure()
    {
        IConnectorRegistry registry = Substitute.For<IConnectorRegistry>();
        registry.Resolve("http").Returns((IServiceTaskConnector?)null);
        IContextProjector<TestContext> projector = new PassThroughProjector();
        IMLog<ConnectorRuleAdapter<TestContext>> log = Substitute.For<IMLog<ConnectorRuleAdapter<TestContext>>>();

        var sut = new ConnectorRuleAdapter<TestContext>(
            "node-1",
            "http",
            connectorConfig: null,
            credentialId: null,
            registry,
            credentialStore: null,
            projector,
            log);

        RuleResult result = await sut.EvaluateAsync(new TestContext(), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_WhenCredentialLookupFails_ReturnsFailure()
    {
        IServiceTaskConnector connector = CreateConnector();
        IConnectorRegistry registry = Substitute.For<IConnectorRegistry>();
        registry.Resolve("http").Returns(connector);

        IConnectorCredentialStore credentials = Substitute.For<IConnectorCredentialStore>();
        credentials.GetAsync("cred-1", "tenant-a", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyDictionary<string, string>>(new InvalidOperationException("boom")));

        var sut = new ConnectorRuleAdapter<TestContext>(
            "node-2",
            "http",
            JsonDocument.Parse("""{"path":"/orders"}""").RootElement,
            "cred-1",
            registry,
            credentials,
            new PassThroughProjector(),
            Substitute.For<IMLog<ConnectorRuleAdapter<TestContext>>>(),
            "tenant-a");

        RuleResult result = await sut.EvaluateAsync(new TestContext(), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("Failed to load credentials", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_WhenConnectorSucceeds_WritesFactsAndMetadata()
    {
        IServiceTaskConnector connector = CreateConnector();
        connector.ExecuteAsync(Arg.Any<ConnectorContext>(), Arg.Any<CancellationToken>())
            .Returns(ConnectorResult.Ok(
                new Dictionary<string, object?> { ["approval"] = "granted" },
                statusCode: 202,
                duration: TimeSpan.FromMilliseconds(15)));

        IConnectorRegistry registry = Substitute.For<IConnectorRegistry>();
        registry.Resolve("http").Returns(connector);

        IConnectorCredentialStore credentials = Substitute.For<IConnectorCredentialStore>();
        credentials.GetAsync("cred-1", "tenant-a", Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["token"] = "secret" });

        FactBag facts = new();
        var sut = new ConnectorRuleAdapter<TestContext>(
            "node-3",
            "http",
            JsonDocument.Parse("""{"path":"/orders"}""").RootElement,
            "cred-1",
            registry,
            credentials,
            new PassThroughProjector(),
            Substitute.For<IMLog<ConnectorRuleAdapter<TestContext>>>(),
            "tenant-a");

        RuleResult result = await sut.EvaluateAsync(new TestContext(), facts, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        facts.Get<string>("approval").Should().Be("granted");
        facts.Get<string>("__node.node-3.approval").Should().Be("granted");
        facts.Get<bool>("__connector.node-3.success").Should().BeTrue();
        facts.Get<int?>("__connector.node-3.statusCode").Should().Be(202);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConnectorThrows_ReturnsFailure()
    {
        IServiceTaskConnector connector = CreateConnector();
        connector.ExecuteAsync(Arg.Any<ConnectorContext>(), Arg.Any<CancellationToken>())
            .Returns<Task<ConnectorResult>>(_ => throw new InvalidOperationException("timeout"));

        IConnectorRegistry registry = Substitute.For<IConnectorRegistry>();
        registry.Resolve("http").Returns(connector);

        var sut = new ConnectorRuleAdapter<TestContext>(
            "node-throw",
            "http",
            connectorConfig: null,
            credentialId: null,
            registry,
            credentialStore: null,
            new PassThroughProjector(),
            Substitute.For<IMLog<ConnectorRuleAdapter<TestContext>>>(),
            "tenant-a");

        RuleResult result = await sut.EvaluateAsync(new TestContext(), new FactBag(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("timeout", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_WhenConnectorReturnsFailure_WritesMetadataAndReturnsFailure()
    {
        IServiceTaskConnector connector = CreateConnector();
        connector.ExecuteAsync(Arg.Any<ConnectorContext>(), Arg.Any<CancellationToken>())
            .Returns(ConnectorResult.Fail("bad gateway", statusCode: 502, duration: TimeSpan.FromMilliseconds(9)));

        IConnectorRegistry registry = Substitute.For<IConnectorRegistry>();
        registry.Resolve("http").Returns(connector);

        FactBag facts = new();
        var sut = new ConnectorRuleAdapter<TestContext>(
            "node-4",
            "http",
            connectorConfig: null,
            credentialId: null,
            registry,
            credentialStore: null,
            new PassThroughProjector(),
            Substitute.For<IMLog<ConnectorRuleAdapter<TestContext>>>(),
            "tenant-a");

        RuleResult result = await sut.EvaluateAsync(new TestContext(), facts, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle(x => x.Contains("bad gateway", StringComparison.OrdinalIgnoreCase));
        facts.Get<bool>("__connector.node-4.success").Should().BeFalse();
        facts.Get<int?>("__connector.node-4.statusCode").Should().Be(502);
        facts.Get<string>("__connector.node-4.error").Should().Be("bad gateway");
    }

    private static IServiceTaskConnector CreateConnector()
    {
        IServiceTaskConnector connector = Substitute.For<IServiceTaskConnector>();
        connector.Metadata.Returns(new ConnectorMetadata
        {
            Type = "http",
            DisplayName = "HTTP",
            Category = "net",
            IconSvg = "<svg />"
        });
        return connector;
    }

    public sealed class TestContext;

    private sealed class PassThroughProjector : IContextProjector<TestContext>
    {
        public IReadOnlyDictionary<string, object?> Project(TestContext context)
        {
            return new Dictionary<string, object?>();
        }
    }
}
