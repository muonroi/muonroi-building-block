namespace Muonroi.Messaging.MassTransit.Tests;

public class AmqpContextConsumeFilterTests
{
    [Fact]
    public void Probe_Does_Not_Throw_With_Null_Context()
    {
        IAmqpContext amqpContext = Substitute.For<IAmqpContext>();
        SystemExecutionContextAccessor accessor = new();
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();
        tenantContextPolicy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(callInfo => callInfo.Arg<ISystemExecutionContext>());

        AmqpContextConsumeFilter<string> filter = new(amqpContext, accessor, tenantContextPolicy);

        Exception? ex = Record.Exception(() => filter.Probe(null!));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Send_Forwards_Message_Maps_Headers_And_Clears_Context()
    {
        Dictionary<string, string?> amqpHeaders = new(StringComparer.OrdinalIgnoreCase);
        int clearCalls = 0;
        int addCalls = 0;

        IAmqpContext amqpContext = Substitute.For<IAmqpContext>();
        amqpContext.GetHeaderByKey(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.Arg<string>();
                return amqpHeaders.TryGetValue(key, out string? value) ? value : null;
            });
        amqpContext.When(x => x.AddHeaders(Arg.Any<IDictionary<string, object>>()))
            .Do(callInfo =>
            {
                addCalls++;
                foreach (KeyValuePair<string, object> pair in callInfo.Arg<IDictionary<string, object>>())
                {
                    amqpHeaders[pair.Key] = pair.Value?.ToString();
                }
            });
        amqpContext.When(x => x.ClearHeaders())
            .Do(_ =>
            {
                clearCalls++;
                amqpHeaders.Clear();
            });

        Headers sourceHeaders = CreateHeaders(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [CustomHeader.CorrelationId] = "corr-123",
            [ClaimConstants.UserIdentifier] = "user-1",
            [ClaimConstants.Username] = "alice",
            [ClaimConstants.AccessToken] = "token-1",
            [CustomHeader.TenantId] = "tenant-1",
            [CustomHeader.SentAt] = DateTimeOffset.UtcNow.ToString("O"),
            [CustomHeader.SourceType] = "http"
        });

        ConsumeContext<string> context = Substitute.For<ConsumeContext<string>>();
        context.Headers.Returns(sourceHeaders);

        SystemExecutionContextAccessor accessor = new();
        ISystemExecutionContext capturedRawContext = SystemExecutionContext.Empty;
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();
        tenantContextPolicy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(callInfo =>
            {
                capturedRawContext = callInfo.Arg<ISystemExecutionContext>();
                return capturedRawContext;
            });

        ISystemExecutionContext? contextDuringNext = null;
        IPipe<ConsumeContext<string>> next = Substitute.For<IPipe<ConsumeContext<string>>>();
        next.Send(context).Returns(_ =>
        {
            contextDuringNext = accessor.Get();
            return Task.CompletedTask;
        });

        AmqpContextConsumeFilter<string> filter = new(amqpContext, accessor, tenantContextPolicy);

        await filter.Send(context, next);

        await next.Received(1).Send(context);
        Assert.Equal(1, addCalls);
        Assert.Equal(2, clearCalls);
        Assert.Empty(amqpHeaders);
        Assert.Equal("corr-123", capturedRawContext.CorrelationId);
        Assert.Equal("tenant-1", capturedRawContext.TenantId);
        Assert.Equal("user-1", capturedRawContext.UserId);
        Assert.Equal("alice", capturedRawContext.Username);
        Assert.Equal("token-1", capturedRawContext.AccessToken);
        Assert.Equal("http", capturedRawContext.SourceType);
        Assert.NotNull(contextDuringNext);
        Assert.Equal("tenant-1", contextDuringNext!.TenantId);
        Assert.Equal("user-1", contextDuringNext.UserId);
        Assert.Equal("http", contextDuringNext.SourceType);
        Assert.Equal("unknown", accessor.Get().SourceType);
    }

    [Fact]
    public async Task Send_When_SourceHeaders_Is_Null_Forwards_Without_Mutating_Amqp_Context()
    {
        int clearCalls = 0;
        int addCalls = 0;

        IAmqpContext amqpContext = Substitute.For<IAmqpContext>();
        amqpContext.When(x => x.AddHeaders(Arg.Any<IDictionary<string, object>>()))
            .Do(_ => addCalls++);
        amqpContext.When(x => x.ClearHeaders())
            .Do(_ => clearCalls++);

        ConsumeContext<string> context = Substitute.For<ConsumeContext<string>>();
        context.Headers.Returns((Headers)null!);

        IPipe<ConsumeContext<string>> next = Substitute.For<IPipe<ConsumeContext<string>>>();
        next.Send(context).Returns(Task.CompletedTask);

        SystemExecutionContextAccessor accessor = new();
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();
        tenantContextPolicy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(callInfo => callInfo.Arg<ISystemExecutionContext>());

        AmqpContextConsumeFilter<string> filter = new(amqpContext, accessor, tenantContextPolicy);

        await filter.Send(context, next);

        await next.Received(1).Send(context);
        Assert.Equal(0, addCalls);
        Assert.Equal(0, clearCalls);
    }

    [Fact]
    public async Task Send_When_Correlation_Header_Missing_Generates_New_Correlation_And_Default_Source()
    {
        Dictionary<string, string?> amqpHeaders = new(StringComparer.OrdinalIgnoreCase);

        IAmqpContext amqpContext = Substitute.For<IAmqpContext>();
        amqpContext.GetHeaderByKey(Arg.Any<string>())
            .Returns(callInfo =>
            {
                string key = callInfo.Arg<string>();
                return amqpHeaders.TryGetValue(key, out string? value) ? value : null;
            });
        amqpContext.When(x => x.AddHeaders(Arg.Any<IDictionary<string, object>>()))
            .Do(callInfo =>
            {
                foreach (KeyValuePair<string, object> pair in callInfo.Arg<IDictionary<string, object>>())
                {
                    amqpHeaders[pair.Key] = pair.Value?.ToString();
                }
            });
        amqpContext.When(x => x.ClearHeaders()).Do(_ => amqpHeaders.Clear());

        Headers sourceHeaders = CreateHeaders(new Dictionary<string, object?>
        {
            [CustomHeader.TenantId] = "tenant-2"
        });

        ConsumeContext<string> context = Substitute.For<ConsumeContext<string>>();
        context.Headers.Returns(sourceHeaders);

        ISystemExecutionContext capturedRawContext = SystemExecutionContext.Empty;
        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();
        tenantContextPolicy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(callInfo =>
            {
                capturedRawContext = callInfo.Arg<ISystemExecutionContext>();
                return capturedRawContext;
            });

        SystemExecutionContextAccessor accessor = new();
        IPipe<ConsumeContext<string>> next = Substitute.For<IPipe<ConsumeContext<string>>>();
        next.Send(context).Returns(Task.CompletedTask);

        AmqpContextConsumeFilter<string> filter = new(amqpContext, accessor, tenantContextPolicy);

        await filter.Send(context, next);

        Assert.True(Guid.TryParseExact(capturedRawContext.CorrelationId, "N", out _));
        Assert.Equal("message-bus", capturedRawContext.SourceType);
    }

    [Fact]
    public async Task Send_When_Next_Throws_Still_Clears_Amqp_Context()
    {
        int clearCalls = 0;

        IAmqpContext amqpContext = Substitute.For<IAmqpContext>();
        amqpContext.GetHeaderByKey(Arg.Any<string>()).Returns((string?)null);
        amqpContext.When(x => x.AddHeaders(Arg.Any<IDictionary<string, object>>()))
            .Do(_ => { });
        amqpContext.When(x => x.ClearHeaders())
            .Do(_ => clearCalls++);

        Headers sourceHeaders = CreateHeaders(new Dictionary<string, object?>
        {
            [CustomHeader.CorrelationId] = "corr-throw"
        });

        ConsumeContext<string> context = Substitute.For<ConsumeContext<string>>();
        context.Headers.Returns(sourceHeaders);

        ITenantContextPolicy tenantContextPolicy = Substitute.For<ITenantContextPolicy>();
        tenantContextPolicy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(callInfo => callInfo.Arg<ISystemExecutionContext>());

        IPipe<ConsumeContext<string>> next = Substitute.For<IPipe<ConsumeContext<string>>>();
        next.Send(context).Returns(_ => throw new MInternalException("boom"));

        SystemExecutionContextAccessor accessor = new();
        AmqpContextConsumeFilter<string> filter = new(amqpContext, accessor, tenantContextPolicy);

        await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Equal(2, clearCalls);
    }

    private static Headers CreateHeaders(IReadOnlyDictionary<string, object?> values)
    {
        Headers headers = Substitute.For<Headers>();
        headers.TryGetHeader(Arg.Any<string>(), out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                string key = (string)callInfo[0]!;
                if (values.TryGetValue(key, out object? value))
                {
                    callInfo[1] = value;
                    return true;
                }

                callInfo[1] = null;
                return false;
            });
        return headers;
    }
}
