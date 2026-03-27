using System.Net.Http.Json;
using Muonroi.Governance.Authorization;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Tests;

public sealed class MPolicyDecisionServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WhenDisabled_ShouldReturnLocalDisabledFallback()
    {
        MPolicyDecisionService service = new(
            new MPolicyDecisionConfigs { Enabled = false },
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IMLog<MPolicyDecisionService>>());

        MPolicyDecisionResult result = await service.EvaluateAsync(CreateRequest());

        result.IsAllowed.Should().BeFalse();
        result.IsAuthoritative.Should().BeFalse();
        result.UsedFallback.Should().BeTrue();
        result.DecisionSource.Should().Be("local.disabled");
    }

    [Fact]
    public async Task EvaluateAsync_WhenEndpointMissing_ShouldFallbackLocally()
    {
        MPolicyDecisionService service = new(
            new MPolicyDecisionConfigs
            {
                Enabled = true,
                FailureMode = MPolicyDecisionFailureMode.FallbackToLocal
            },
            Substitute.For<IHttpClientFactory>(),
            Substitute.For<IMLog<MPolicyDecisionService>>());

        MPolicyDecisionResult result = await service.EvaluateAsync(CreateRequest());

        result.IsAuthoritative.Should().BeFalse();
        result.UsedFallback.Should().BeTrue();
        result.Error.Should().Contain("endpoint is missing");
        result.DecisionSource.Should().Be("local.fallback");
    }

    [Fact]
    public async Task EvaluateAsync_WhenHttpFailsAndFailureModeDeny_ShouldReturnAuthoritativeDeny()
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(new ThrowingHandler())
            {
                BaseAddress = new Uri("https://pdp.example")
            });

        MPolicyDecisionService service = new(
            new MPolicyDecisionConfigs
            {
                Enabled = true,
                Endpoint = "https://pdp.example",
                FailureMode = MPolicyDecisionFailureMode.Deny
            },
            httpClientFactory,
            Substitute.For<IMLog<MPolicyDecisionService>>());

        MPolicyDecisionResult result = await service.EvaluateAsync(CreateRequest());

        result.IsAllowed.Should().BeFalse();
        result.IsAuthoritative.Should().BeTrue();
        result.UsedFallback.Should().BeFalse();
        result.DecisionSource.Should().Be("pdp.failure");
        result.Error.Should().Contain("network down");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOpaReturnsAllowObject_ShouldAllow()
    {
        RecordingHandler handler = new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"result":{"allow":true}}""")
        }));
        IHttpClientFactory httpClientFactory = CreateFactory(handler);

        MPolicyDecisionService service = new(
            new MPolicyDecisionConfigs
            {
                Enabled = true,
                Endpoint = "https://pdp.example",
                Provider = MPolicyDecisionProvider.Opa
            },
            httpClientFactory,
            Substitute.For<IMLog<MPolicyDecisionService>>());

        MPolicyDecisionResult result = await service.EvaluateAsync(CreateRequest());

        result.IsAllowed.Should().BeTrue();
        result.IsAuthoritative.Should().BeTrue();
        result.DecisionSource.Should().Be("pdp.opa");
        handler.RequestUri!.AbsolutePath.Should().Be("/v1/data/authz/allow");
    }

    [Fact]
    public async Task EvaluateAsync_WhenOpenFgaReturnsAllowed_ShouldUseOpenFgaPayloadAndSource()
    {
        RecordingHandler handler = new(async request =>
        {
            string json = await request.Content!.ReadAsStringAsync();
            json.Should().Contain("\"tuple_key\"");
            json.Should().Contain("\"user\":\"user-1\"");
            json.Should().Contain("\"relation\":\"read\"");
            json.Should().Contain("\"object\":\"invoice:42\"");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { allowed = false })
            };
        });
        IHttpClientFactory httpClientFactory = CreateFactory(handler);

        MPolicyDecisionService service = new(
            new MPolicyDecisionConfigs
            {
                Enabled = true,
                Endpoint = "https://pdp.example",
                Provider = MPolicyDecisionProvider.OpenFga
            },
            httpClientFactory,
            Substitute.For<IMLog<MPolicyDecisionService>>());

        MPolicyDecisionResult result = await service.EvaluateAsync(CreateRequest());

        result.IsAllowed.Should().BeFalse();
        result.IsAuthoritative.Should().BeTrue();
        result.DecisionSource.Should().Be("pdp.openfga");
        handler.RequestUri!.AbsolutePath.Should().Be("/check");
    }

    [Fact]
    public async Task EvaluateAsync_WhenResponseShapeUnsupported_ShouldFallback()
    {
        RecordingHandler handler = new(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"unexpected":true}""")
        }));

        MPolicyDecisionService service = new(
            new MPolicyDecisionConfigs
            {
                Enabled = true,
                Endpoint = "https://pdp.example",
                FailureMode = MPolicyDecisionFailureMode.FallbackToLocal
            },
            CreateFactory(handler),
            Substitute.For<IMLog<MPolicyDecisionService>>());

        MPolicyDecisionResult result = await service.EvaluateAsync(CreateRequest());

        result.IsAuthoritative.Should().BeFalse();
        result.UsedFallback.Should().BeTrue();
        result.Error.Should().Contain("Unsupported PDP response shape");
    }

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>())
            .Returns(new HttpClient(handler)
            {
                BaseAddress = new Uri("https://pdp.example")
            });
        return httpClientFactory;
    }

    private static MPolicyDecisionRequest CreateRequest()
    {
        return new MPolicyDecisionRequest
        {
            UserId = "user-1",
            TenantId = "tenant-a",
            CorrelationId = "corr-1",
            Action = "read",
            Resource = "invoice:42"
        };
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("network down");
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return responder(request);
        }
    }
}
