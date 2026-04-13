namespace Muonroi.BuildingBlock.Test;

public class PublishEndpointAuthExtensionsTests
{
    private static void InvokeApply(PublishContext<string> ctx, MAuthenticateInfoContext auth)
    {
        MethodInfo mi = typeof(PublishEndpointAuthExtensions)
            .GetMethod("ApplyAuthHeaders", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(string));
        mi.Invoke(null, [ctx, auth]);
    }

    [Fact]
    public async Task PublishWithAuthContext_Sends_Message()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();

        await endpoint.PublishWithAuthContext("msg", new MAuthenticateInfoContext(false));

        await endpoint.Received()
            .Publish("msg", Arg.Any<IPipe<PublishContext<string>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishWithAuthContext_Allows_Null_Message()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        await endpoint.PublishWithAuthContext<string>(null!, new MAuthenticateInfoContext(false));
        await endpoint.PublishWithAuthContext(string.Empty, new MAuthenticateInfoContext(false));
        await endpoint.Received()
            .Publish(null!, Arg.Any<IPipe<PublishContext<string>>>(), Arg.Any<CancellationToken>());
        await endpoint.Received().Publish(string.Empty, Arg.Any<IPipe<PublishContext<string>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishWithAuthContext_Null_Context_Throws()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        await Assert.ThrowsAsync<NullReferenceException>(() => endpoint.PublishWithAuthContext("m", null!));
    }

    [Fact]
    public async Task PublishWithContext_Sets_Tenant_Header()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        IPipe<PublishContext<string>>? callback = null;
        endpoint
            .Publish("m", Arg.Any<IPipe<PublishContext<string>>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callback = ci.ArgAt<IPipe<PublishContext<string>>>(1);
                return Task.CompletedTask;
            });

        await endpoint.PublishWithContext("m", new MAuthenticateInfoContext(false), "t1");
        PublishContext<string> ctx = Substitute.For<PublishContext<string>>();
        DictionarySendHeaders headers = new();
        ctx.Headers.Returns(headers);
        await callback!.Send(ctx);
        Assert.True(headers.TryGetHeader(CustomHeader.TenantId, out object? val));
        Assert.Equal("t1", val);
    }

    [Fact]
    public async Task PublishWithContext_Allows_Null_Args()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        await endpoint.PublishWithAuthContext(string.Empty, new MAuthenticateInfoContext(false));
        await endpoint.Received().Publish(string.Empty, Arg.Any<IPipe<PublishContext<string>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishWithContext_Null_Context_Throws()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        await Assert.ThrowsAsync<NullReferenceException>(() => endpoint.PublishWithContext("m", null!, null));
    }

    [Fact]
    public async Task PublishWithAuthContext_Uses_CurrentTenantContext()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-current";

        try
        {
            IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
            IPipe<PublishContext<string>>? callback = null;
            endpoint
                .Publish("m", Arg.Any<IPipe<PublishContext<string>>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    callback = ci.ArgAt<IPipe<PublishContext<string>>>(1);
                    return Task.CompletedTask;
                });

            await endpoint.PublishWithAuthContext("m", new MAuthenticateInfoContext(false));

            PublishContext<string> ctx = Substitute.For<PublishContext<string>>();
            DictionarySendHeaders headers = new();
            ctx.Headers.Returns(headers);
            await callback!.Send(ctx);

            Assert.True(headers.TryGetHeader(CustomHeader.TenantId, out object? val));
            Assert.Equal("tenant-current", val);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task PublishWithAuthContext_FallsBack_To_AuthContextTenant()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = null;

        try
        {
            IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
            IPipe<PublishContext<string>>? callback = null;
            endpoint
                .Publish("m", Arg.Any<IPipe<PublishContext<string>>>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    callback = ci.ArgAt<IPipe<PublishContext<string>>>(1);
                    return Task.CompletedTask;
                });

            await endpoint.PublishWithAuthContext("m", new MAuthenticateInfoContext(false) { TenantId = "tenant-auth" });

            PublishContext<string> ctx = Substitute.For<PublishContext<string>>();
            DictionarySendHeaders headers = new();
            ctx.Headers.Returns(headers);
            await callback!.Send(ctx);

            Assert.True(headers.TryGetHeader(CustomHeader.TenantId, out object? val));
            Assert.Equal("tenant-auth", val);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task PublishWithContext_WhenTenantArgMissing_Uses_AuthContextTenant()
    {
        IPublishEndpoint endpoint = Substitute.For<IPublishEndpoint>();
        IPipe<PublishContext<string>>? callback = null;
        endpoint
            .Publish("m", Arg.Any<IPipe<PublishContext<string>>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callback = ci.ArgAt<IPipe<PublishContext<string>>>(1);
                return Task.CompletedTask;
            });

        await endpoint.PublishWithContext("m", new MAuthenticateInfoContext(false) { TenantId = "tenant-auth" }, null);
        PublishContext<string> ctx = Substitute.For<PublishContext<string>>();
        DictionarySendHeaders headers = new();
        ctx.Headers.Returns(headers);
        await callback!.Send(ctx);

        Assert.True(headers.TryGetHeader(CustomHeader.TenantId, out object? val));
        Assert.Equal("tenant-auth", val);
    }

    [Fact]
    public void ApplyAuthHeaders_Sets_And_Overwrites()
    {
        PublishContext<string> ctx = Substitute.For<PublishContext<string>>();
        DictionarySendHeaders headers = new();
        headers.Set(CustomHeader.CorrelationId, "old");
        ctx.Headers.Returns(headers);
        MAuthenticateInfoContext auth = new(false)
        {
            CorrelationId = "c",
            CurrentUserGuid = "u",
            CurrentUsername = "name",
            AccessToken = "tok"
        };
        InvokeApply(ctx, auth);
        Assert.True(headers.TryGetHeader(CustomHeader.CorrelationId, out object? c));
        Assert.Equal("c", c);
        Assert.True(headers.TryGetHeader(ClaimConstants.UserIdentifier, out object? u));
        Assert.Equal("u", u);
        Assert.True(headers.TryGetHeader(ClaimConstants.Username, out object? n));
        Assert.Equal("name", n);
        Assert.True(headers.TryGetHeader(ClaimConstants.AccessToken, out object? t));
        Assert.Equal("tok", t);
    }

    [Fact]
    public void ApplyAuthHeaders_Null_Values_Do_Not_Overwrite()
    {
        PublishContext<string> ctx = Substitute.For<PublishContext<string>>();
        DictionarySendHeaders headers = new();
        headers.Set(CustomHeader.CorrelationId, "c1");
        ctx.Headers.Returns(headers);
        MAuthenticateInfoContext auth = new(false);
        InvokeApply(ctx, auth);
        Assert.True(headers.TryGetHeader(CustomHeader.CorrelationId, out object? c));
        Assert.Equal("c1", c);
    }
}
