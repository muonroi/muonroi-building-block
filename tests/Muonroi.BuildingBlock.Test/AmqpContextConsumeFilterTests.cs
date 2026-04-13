namespace Muonroi.BuildingBlock.Test;

public class AmqpContextConsumeFilterTests
{
    private class DummyContext<T>(T message) : ConsumeContext<T> where T : class
    {
        public T Message { get; } = message;
        public CancellationToken CancellationToken => CancellationToken.None;
        public Headers Headers { get; } = new DictionarySendHeaders();
        public ReceiveContext ReceiveContext => throw new NotImplementedException();
        public SerializerContext SerializerContext => throw new NotImplementedException();
        public Task ConsumeCompleted => Task.CompletedTask;
        public IEnumerable<string> SupportedMessageTypes => [];

        public bool HasMessageType(Type messageType)
        {
            return false;
        }

        public bool TryGetMessage<TMessage>(out ConsumeContext<TMessage> consumeContext) where TMessage : class
        {
            consumeContext = null!;
            return false;
        }

        public void AddConsumeTask(Task task)
        {
        }

        public Task RespondAsync<TMessage>(TMessage message) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(TMessage message, IPipe<SendContext<TMessage>> sendPipe)
            where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(TMessage message, IPipe<SendContext> sendPipe) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync(object message)
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync(object message, Type messageType)
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync(object message, IPipe<SendContext> sendPipe)
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync(object message, Type messageType, IPipe<SendContext> sendPipe)
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(object values) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(object values, IPipe<SendContext<TMessage>> sendPipe) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(object values, IPipe<SendContext> sendPipe) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public void Respond<TMessage>(TMessage message) where TMessage : class
        {
        }

        public Task NotifyConsumed<TMessage>(ConsumeContext<TMessage> context, TimeSpan duration, string consumerType)
            where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task NotifyFaulted<TMessage>(ConsumeContext<TMessage> context, TimeSpan duration, string consumerType,
            Exception exception) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task NotifyConsumed(TimeSpan duration, string consumerType)
        {
            return Task.CompletedTask;
        }

        public Task NotifyFaulted(TimeSpan duration, string consumerType, Exception exception)
        {
            return Task.CompletedTask;
        }

        public bool HasPayloadType(Type payloadType)
        {
            return false;
        }

        public bool TryGetPayload<TPayload>([NotNullWhen(true)] out TPayload payload) where TPayload : class
        {
            payload = null!;
            return false;
        }

        public TPayload GetOrAddPayload<TPayload>(PayloadFactory<TPayload> payloadFactory) where TPayload : class
        {
            return null!;
        }

        public TPayload AddOrUpdatePayload<TPayload>(PayloadFactory<TPayload> addFactory,
            UpdatePayloadFactory<TPayload> updateFactory) where TPayload : class
        {
            return null!;
        }

        public Task Publish<T1>(T1 message, CancellationToken cancellationToken = default) where T1 : class
        {
            return Task.CompletedTask;
        }

        public Task Publish<T1>(T1 message, IPipe<PublishContext<T1>> publishPipe,
            CancellationToken cancellationToken = default) where T1 : class
        {
            return Task.CompletedTask;
        }

        public Task Publish<T1>(T1 message, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default) where T1 : class
        {
            return Task.CompletedTask;
        }

        public Task Publish(object message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish(object message, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish(object message, Type messageType, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<T1>(object values, CancellationToken cancellationToken = default) where T1 : class
        {
            return Task.CompletedTask;
        }

        public Task Publish<T1>(object values, IPipe<PublishContext<T1>> publishPipe,
            CancellationToken cancellationToken = default) where T1 : class
        {
            return Task.CompletedTask;
        }

        public Task Publish<T1>(object values, IPipe<PublishContext> publishPipe,
            CancellationToken cancellationToken = default) where T1 : class
        {
            return Task.CompletedTask;
        }

        public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
        {
            throw new NotImplementedException();
        }

        public Task<ISendEndpoint> GetSendEndpoint(Uri address)
        {
            return (Task<ISendEndpoint>)Task.CompletedTask;
        }

        public ConnectHandle ConnectSendObserver(ISendObserver observer)
        {
            throw new NotImplementedException();
        }

        Guid? MessageContext.MessageId => null;
        Guid? MessageContext.RequestId => null;
        Guid? MessageContext.CorrelationId => null;
        Guid? MessageContext.ConversationId => null;
        Guid? MessageContext.InitiatorId => null;
        DateTime? MessageContext.ExpirationTime => null;
        Uri? MessageContext.SourceAddress => null;
        Uri? MessageContext.DestinationAddress => null;
        Uri? MessageContext.ResponseAddress => null;
        Uri? MessageContext.FaultAddress => null;
        DateTime? MessageContext.SentTime => null;
        Headers MessageContext.Headers => Headers;
        HostInfo MessageContext.Host => throw new NotImplementedException();
    }

    private class FakeProbeContext : ProbeContext
    {
        public CancellationToken CancellationToken => CancellationToken.None;

        public void Add(string key, string value)
        {
        }

        public void Add(string key, object value)
        {
        }

        public ProbeContext CreateScope(string key)
        {
            return new FakeProbeContext();
        }

        public void Set(object values)
        {
        }

        public void Set(IEnumerable<KeyValuePair<string, object>> values)
        {
        }
    }

    [Fact]
    public void Constructor_Allows_Null_Dependency()
    {
        AmqpContextConsumeFilter<string> filter = new(null!);
        Assert.NotNull(filter);
    }

    [Fact]
    public void Probe_Does_Not_Throw_With_Context()
    {
        AmqpContextConsumeFilter<string> filter = new(new AmqpContext());
        FakeProbeContext context = new();
        filter.Probe(context);
        Assert.NotNull(context);
    }

    [Fact]
    public void Probe_Does_Not_Throw_With_Null()
    {
        AmqpContextConsumeFilter<string> filter = new(new AmqpContext());
        Exception ex = Record.Exception(() => filter.Probe(null!));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Send_Forwards_To_Next_And_Clears_Headers()
    {
        AmqpContext amqp = new();
        AmqpContextConsumeFilter<string> filter = new(amqp);
        DummyContext<string> context = new("msg");
        ((DictionarySendHeaders)context.Headers).Set(CustomHeader.CorrelationId, "c");
        bool called = false;
        IPipe<ConsumeContext<string>> next = Pipe.ExecuteAsync<ConsumeContext<string>>(_ =>
        {
            called = true;
            Assert.Equal("c", amqp.GetHeaderByKey(CustomHeader.CorrelationId));
            return Task.CompletedTask;
        });
        await filter.Send(context, next);
        Assert.True(called);
        Assert.Null(amqp.GetHeaderByKey(CustomHeader.CorrelationId));
    }

    [Fact]
    public async Task Send_NullNext_Throws()
    {
        AmqpContextConsumeFilter<string> filter = new(new AmqpContext());
        DummyContext<string> context = new("m");
        await Assert.ThrowsAsync<NullReferenceException>(() => filter.Send(context, null!));
    }
}
