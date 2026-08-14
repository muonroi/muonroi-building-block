namespace Muonroi.Governance.Tests;

public class LicenseRuntimeEnforcementTests
{
    public sealed record TestMessage(string Value);

    private static readonly LicenseState MessageBusLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.MessageBus]
    };

    private static readonly LicenseState DistributedCacheLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.DistributedCache]
    };

    private static readonly LicenseState MessageBusSpoofedInvalid = new()
    {
        IsValid = false,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.MessageBus]
    };

    private static readonly LicenseState DistributedCacheSpoofedInvalid = new()
    {
        IsValid = false,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.DistributedCache]
    };

    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public byte[]? Get(string key)
        {
            return _store.TryGetValue(key, out byte[]? value) ? value : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _store.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _store[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScopeLicenseGuard(bool allowDistributedCache) : ILicenseGuard
    {
        public LicenseState Current => LicenseState.CreateFree();
        public LicenseTier Tier => LicenseTier.Free;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.DistributedCache, StringComparison.OrdinalIgnoreCase) ||
                   allowDistributedCache;
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
            {
                throw new MInternalException("distributed-cache feature not licensed");
            }
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken()
        {
            return string.Empty;
        }

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return encryptedData;
        }
    }

    private sealed class DenyMessageBusGuard : ILicenseGuard
    {
        public LicenseState Current => MessageBusLicensed;
        public LicenseTier Tier => LicenseTier.Licensed;
        public bool IsFreeMode => false;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.MessageBus, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
            {
                throw new MInternalException("message-bus feature blocked by guard");
            }
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken()
        {
            return string.Empty;
        }

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return encryptedData;
        }
    }

    [Fact]
    public async Task MultiLevelCacheService_FreeMode_ExternalDistributedCache_Throws()
    {
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        IDistributedCache distributed = new FakeDistributedCache();
        MultiLevelCacheService service = new(memory, distributed, LicenseState.CreateFree());

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => service.GetAsync<string>("k"));
        Assert.Contains("distributed-cache", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultiLevelCacheService_Licensed_ExternalDistributedCache_Allows()
    {
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        IDistributedCache distributed = new FakeDistributedCache();
        MultiLevelCacheService service = new(memory, distributed, DistributedCacheLicensed);

        await service.SetAsync("k", "v");
        string? value = await service.GetAsync<string>("k");

        Assert.Equal("v", value);
    }

    [Fact]
    public async Task MultiLevelCacheService_SpoofedInvalidLicense_ExternalDistributedCache_Throws()
    {
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        IDistributedCache distributed = new FakeDistributedCache();
        MultiLevelCacheService service = new(memory, distributed, DistributedCacheSpoofedInvalid);

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => service.GetAsync<string>("k"));
        Assert.Contains("distributed-cache", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultiLevelCacheService_ScopeLicenseGuard_TakesPrecedence()
    {
        ServiceCollection services = [];
        services.AddScoped<ILicenseGuard>(_ => new TestScopeLicenseGuard(allowDistributedCache: false));
        using ServiceProvider provider = services.BuildServiceProvider();

        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        IDistributedCache distributed = new FakeDistributedCache();
        MultiLevelCacheService service = new(
            memory,
            distributed,
            DistributedCacheLicensed,
            scopeFactory: provider.GetRequiredService<IServiceScopeFactory>());

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => service.GetAsync<string>("k"));
        Assert.Contains("distributed-cache", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EcsPublishLoggingFilter_FreeMode_Throws()
    {
        EcsPublishLoggingFilter<TestMessage> filter = new();
        PublishContext<TestMessage> context = Substitute.For<PublishContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<PublishContext<TestMessage>> next = Substitute.For<IPipe<PublishContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<PublishContext<TestMessage>>());
    }

    [Fact]
    public async Task EcsPublishLoggingFilter_Licensed_Allows()
    {
        EcsPublishLoggingFilter<TestMessage> filter = new(MessageBusLicensed);
        PublishContext<TestMessage> context = Substitute.For<PublishContext<TestMessage>>();
        context.DestinationAddress.Returns(new Uri("rabbitmq://localhost/exchange/test"));
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<PublishContext<TestMessage>> next = Substitute.For<IPipe<PublishContext<TestMessage>>>();
        next.Send(context).Returns(Task.CompletedTask);

        await filter.Send(context, next);
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task EcsConsumeLoggingFilter_FreeMode_Throws()
    {
        EcsConsumeLoggingFilter<TestMessage> filter = new();
        ConsumeContext<TestMessage> context = Substitute.For<ConsumeContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<Headers>());
        IPipe<ConsumeContext<TestMessage>> next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<ConsumeContext<TestMessage>>());
    }

    [Fact]
    public async Task EcsConsumeLoggingFilter_Licensed_Allows()
    {
        EcsConsumeLoggingFilter<TestMessage> filter = new(MessageBusLicensed);
        ConsumeContext<TestMessage> context = Substitute.For<ConsumeContext<TestMessage>>();
        context.DestinationAddress.Returns(new Uri("rabbitmq://localhost/queue/test"));
        Headers headers = Substitute.For<Headers>();
        headers
            .TryGetHeader(CustomHeader.TenantId, out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                callInfo[1] = "tenant-a";
                return true;
            });
        context.Headers.Returns(headers);
        IPipe<ConsumeContext<TestMessage>> next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        next.Send(context).Returns(Task.CompletedTask);

        await filter.Send(context, next);
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task EcsSendLoggingFilter_FreeMode_Throws()
    {
        EcsSendLoggingFilter<TestMessage> filter = new();
        SendContext<TestMessage> context = Substitute.For<SendContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<SendContext<TestMessage>> next = Substitute.For<IPipe<SendContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<SendContext<TestMessage>>());
    }

    [Fact]
    public async Task EcsSendLoggingFilter_Licensed_Allows()
    {
        EcsSendLoggingFilter<TestMessage> filter = new(MessageBusLicensed);
        SendContext<TestMessage> context = Substitute.For<SendContext<TestMessage>>();
        context.DestinationAddress.Returns(new Uri("rabbitmq://localhost/queue/test"));
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<SendContext<TestMessage>> next = Substitute.For<IPipe<SendContext<TestMessage>>>();
        next.Send(context).Returns(Task.CompletedTask);

        await filter.Send(context, next);
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task EcsPublishLoggingFilter_SpoofedInvalidLicense_Throws()
    {
        EcsPublishLoggingFilter<TestMessage> filter = new(MessageBusSpoofedInvalid);
        PublishContext<TestMessage> context = Substitute.For<PublishContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<PublishContext<TestMessage>> next = Substitute.For<IPipe<PublishContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<PublishContext<TestMessage>>());
    }

    [Fact]
    public async Task EcsPublishLoggingFilter_LicenseGuardDenies_TakesPrecedence()
    {
        EcsPublishLoggingFilter<TestMessage> filter = new(new DenyMessageBusGuard(), MessageBusLicensed);
        PublishContext<TestMessage> context = Substitute.For<PublishContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<PublishContext<TestMessage>> next = Substitute.For<IPipe<PublishContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus feature blocked by guard", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<PublishContext<TestMessage>>());
    }

    [Fact]
    public async Task EcsSendLoggingFilter_LicenseGuardDenies_TakesPrecedence()
    {
        EcsSendLoggingFilter<TestMessage> filter = new(new DenyMessageBusGuard(), MessageBusLicensed);
        SendContext<TestMessage> context = Substitute.For<SendContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        IPipe<SendContext<TestMessage>> next = Substitute.For<IPipe<SendContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus feature blocked by guard", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<SendContext<TestMessage>>());
    }

    [Fact]
    public async Task EcsConsumeLoggingFilter_LicenseGuardDenies_TakesPrecedence()
    {
        EcsConsumeLoggingFilter<TestMessage> filter = new(new DenyMessageBusGuard(), MessageBusLicensed);
        ConsumeContext<TestMessage> context = Substitute.For<ConsumeContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<Headers>());
        IPipe<ConsumeContext<TestMessage>> next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();

        MInternalException ex = await Assert.ThrowsAsync<MInternalException>(() => filter.Send(context, next));
        Assert.Contains("message-bus feature blocked by guard", ex.Message, StringComparison.OrdinalIgnoreCase);
        await next.DidNotReceive().Send(Arg.Any<ConsumeContext<TestMessage>>());
    }
}


