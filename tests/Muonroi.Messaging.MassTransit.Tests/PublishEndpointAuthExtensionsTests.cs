using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Messaging.MassTransit.Tests;

public class PublishEndpointAuthExtensionsTests
{
    public sealed record TestMessage(string Value);

    [Fact]
    public async Task PublishWithAuthContext_Publishes_And_Sets_Headers_From_Resolved_Context()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        ISystemExecutionContextAccessor accessor = Substitute.For<ISystemExecutionContextAccessor>();
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();

        SystemExecutionContext rawContext = new(
            tenantId: "tenant-raw",
            userId: "user-raw",
            username: "raw-user",
            correlationId: "corr-raw",
            accessToken: "token-raw",
            apiKey: null,
            isAuthenticated: true,
            permissions: [],
            sourceType: "http");
        SystemExecutionContext resolvedContext = rawContext.With(
            tenantId: "tenant-resolved",
            userId: "user-resolved",
            username: "resolved-user",
            correlationId: "corr-resolved",
            accessToken: "token-resolved",
            sourceType: "worker");

        accessor.Get().Returns(rawContext);
        tenantContextPolicy.ResolveAndValidate(rawContext).Returns(resolvedContext);

        IPipe<PublishContext<TestMessage>>? capturedPipe = null;
        TestMessage message = new("hello");
        endpoint.Publish(message, Arg.Any<IPipe<PublishContext<TestMessage>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedPipe = callInfo.ArgAt<IPipe<PublishContext<TestMessage>>>(1);
                return Task.CompletedTask;
            });

        await endpoint.PublishWithAuthContext(message, accessor, tenantContextPolicy);

        await endpoint.Received(1).Publish(message, Arg.Any<IPipe<PublishContext<TestMessage>>>(), Arg.Any<CancellationToken>());
        accessor.Received(1).Get();
        tenantContextPolicy.Received(1).ResolveAndValidate(rawContext);

        Assert.NotNull(capturedPipe);
        PublishContext<TestMessage> publishContext = Substitute.For<PublishContext<TestMessage>>();
        SendHeaders headers = CreateWritableHeaders(out Dictionary<string, object?> writtenHeaders);
        publishContext.Headers.Returns(headers);

        await capturedPipe!.Send(publishContext);

        Assert.Equal("corr-resolved", writtenHeaders[CustomHeader.CorrelationId]);
        Assert.Equal("worker", writtenHeaders[CustomHeader.SourceType]);
        Assert.Equal("tenant-resolved", writtenHeaders[CustomHeader.TenantId]);
        Assert.Equal("user-resolved", writtenHeaders[ClaimConstants.UserIdentifier]);
        Assert.Equal("resolved-user", writtenHeaders[ClaimConstants.Username]);
        Assert.Equal("token-resolved", writtenHeaders[ClaimConstants.AccessToken]);
        Assert.True(writtenHeaders.TryGetValue(CustomHeader.SentAt, out object? sentAtObj));
        Assert.True(DateTimeOffset.TryParse(sentAtObj?.ToString(), out _));
    }

    [Fact]
    public async Task PublishWithContext_When_Optional_Fields_Empty_Only_Sets_Required_Headers()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();

        SystemExecutionContext context = new(
            tenantId: null,
            userId: null,
            username: null,
            correlationId: "corr-1",
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "api");
        tenantContextPolicy.ResolveAndValidate(context).Returns(context);

        IPipe<PublishContext<TestMessage>>? capturedPipe = null;
        TestMessage message = new("hello");
        endpoint.Publish(message, Arg.Any<IPipe<PublishContext<TestMessage>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedPipe = callInfo.ArgAt<IPipe<PublishContext<TestMessage>>>(1);
                return Task.CompletedTask;
            });

        await endpoint.PublishWithContext(message, context, tenantContextPolicy);

        Assert.NotNull(capturedPipe);
        PublishContext<TestMessage> publishContext = Substitute.For<PublishContext<TestMessage>>();
        SendHeaders headers = CreateWritableHeaders(out Dictionary<string, object?> writtenHeaders);
        publishContext.Headers.Returns(headers);

        await capturedPipe!.Send(publishContext);

        Assert.Equal("corr-1", writtenHeaders[CustomHeader.CorrelationId]);
        Assert.Equal("api", writtenHeaders[CustomHeader.SourceType]);
        Assert.True(writtenHeaders.ContainsKey(CustomHeader.SentAt));
        Assert.False(writtenHeaders.ContainsKey(CustomHeader.TenantId));
        Assert.False(writtenHeaders.ContainsKey(ClaimConstants.UserIdentifier));
        Assert.False(writtenHeaders.ContainsKey(ClaimConstants.Username));
        Assert.False(writtenHeaders.ContainsKey(ClaimConstants.AccessToken));
    }

    [Fact]
    public async Task PublishWithAuthContext_Null_ContextAccessor_Throws()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();

        MArgumentException ex = await Assert.ThrowsAsync<MArgumentException>(
            () => endpoint.PublishWithAuthContext(new TestMessage("x"), null!, tenantContextPolicy));

        Assert.Equal("contextAccessor", ex.ParamName);
    }

    [Fact]
    public async Task PublishWithContext_Null_Endpoint_Throws()
    {
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();
        SystemExecutionContext context = SystemExecutionContext.Empty;

        MArgumentException ex = await Assert.ThrowsAsync<MArgumentException>(
            () => PublishEndpointAuthExtensions.PublishWithContext(
                null!,
                new TestMessage("x"),
                context,
                tenantContextPolicy));

        Assert.Equal("endpoint", ex.ParamName);
    }

    [Fact]
    public async Task PublishWithContext_Null_Context_Throws()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();

        MArgumentException ex = await Assert.ThrowsAsync<MArgumentException>(
            () => endpoint.PublishWithContext(new TestMessage("x"), null!, tenantContextPolicy));

        Assert.Equal("context", ex.ParamName);
    }

    [Fact]
    public async Task PublishWithContext_Null_TenantPolicy_Throws()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        SystemExecutionContext context = SystemExecutionContext.Empty;

        MArgumentException ex = await Assert.ThrowsAsync<MArgumentException>(
            () => endpoint.PublishWithContext(new TestMessage("x"), context, null!));

        Assert.Equal("tenantContextPolicy", ex.ParamName);
    }

    private static SendHeaders CreateWritableHeaders(out Dictionary<string, object?> writtenHeaders)
    {
        Dictionary<string, object?> dictionary = new(StringComparer.OrdinalIgnoreCase);
        SendHeaders headers = Substitute.For<SendHeaders>();
        headers.When(x => x.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(callInfo => dictionary[(string)callInfo[0]!] = callInfo[1]);
        headers.When(x => x.Set(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<bool>()))
            .Do(callInfo => dictionary[(string)callInfo[0]!] = callInfo[1]);
        writtenHeaders = dictionary;
        return headers;
    }
}
