using MassTransit;
using MassTransit.Serialization;
using Muonroi.Core.Abstractions.Constants;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.BuildingBlock.Test;

public class TenantContextConsumeFilterTests
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

        public Task RespondAsync<TMessage>(TMessage message, IPipe<SendContext<TMessage>> pipe) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(TMessage message, IPipe<SendContext> pipe) where TMessage : class
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

        public Task RespondAsync(object message, IPipe<SendContext> pipe)
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync(object message, Type messageType, IPipe<SendContext> pipe)
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(object values) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(object values, IPipe<SendContext<TMessage>> pipe) where TMessage : class
        {
            return Task.CompletedTask;
        }

        public Task RespondAsync<TMessage>(object values, IPipe<SendContext> pipe) where TMessage : class
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

        public bool TryGetPayload<T1>([NotNullWhen(true)] out T1 payload) where T1 : class
        {
            payload = null!;
            return false;
        }

        public T1 GetOrAddPayload<T1>(PayloadFactory<T1> payloadFactory) where T1 : class
        {
            return null!;
        }

        public T1 AddOrUpdatePayload<T1>(PayloadFactory<T1> addFactory, UpdatePayloadFactory<T1> updateFactory)
            where T1 : class
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

    [Fact]
    public async Task Send_Assigns_And_Clears_TenantId()
    {
        TenantContext.CurrentTenantId = null;
        DummyContext<string> context = new("msg");
        ((DictionarySendHeaders)context.Headers).Set(CustomHeader.TenantId, "t1");
        bool called = false;
        string? capturedTenant = null;
        IPipe<ConsumeContext<string>> next = Pipe.ExecuteAsync<ConsumeContext<string>>(_ =>
        {
            capturedTenant = TenantContext.CurrentTenantId;
            called = true;
            return Task.CompletedTask;
        });
        TenantContextConsumeFilter<string> filter = new();
        await filter.Send(context, next);
        Assert.True(called);
        Assert.Equal("t1", capturedTenant);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task Send_Without_Tenant_Header_Keeps_Context_Null()
    {
        TenantContext.CurrentTenantId = null;
        DummyContext<string> context = new("payload");
        string? capturedTenant = "set";
        IPipe<ConsumeContext<string>> next = Pipe.ExecuteAsync<ConsumeContext<string>>(_ =>
        {
            capturedTenant = TenantContext.CurrentTenantId;
            return Task.CompletedTask;
        });

        TenantContextConsumeFilter<string> filter = new();
        await filter.Send(context, next);

        Assert.Null(capturedTenant);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task Send_NullNext_Throws()
    {
        DummyContext<string> context = new("m");
        TenantContextConsumeFilter<string> filter = new();
        await Assert.ThrowsAsync<NullReferenceException>(() => filter.Send(context, null!));
    }

    [Fact]
    public void Probe_With_Context_Does_Not_Throw()
    {
        TenantContextConsumeFilter<string> filter = new();
        ProbeContext context = Substitute.For<ProbeContext>();
        Exception ex = Record.Exception(() => filter.Probe(context));
        Assert.Null(ex);
    }

    [Fact]
    public void Probe_With_Null_Does_Not_Throw()
    {
        TenantContextConsumeFilter<string> filter = new();
        Exception ex = Record.Exception(() => filter.Probe(null!));
        Assert.Null(ex);
    }
}
