namespace Muonroi.BuildingBlock.Test;

public class MediatorExceptionTests
{
    private class EmptyServiceFactory
    {
        public object? Invoke(Type serviceType)
        {
            return null;
        }

        public static implicit operator ServiceFactory(EmptyServiceFactory _)
        {
            return new ServiceFactory(_.Invoke);
        }
    }

    [Fact]
    public async Task Send_Null_Request_Throws()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<string>(null!));
    }

    private class DummyRequest : IRequest<string>
    {
    }

    [Fact]
    public async Task Send_Handler_Not_Found_Throws()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Send(new DummyRequest()));
    }

    [Fact]
    public async Task Publish_Null_Event_Throws()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish<TestNotification>(null!));
    }

    private class TestNotification : INotification
    {
    }

    [Fact]
    public async Task Publish_Object_NotINotification_Throws()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        await Assert.ThrowsAsync<InvalidOperationException>(() => mediator.Publish(new object()));
    }

    private class StreamRequest : IStreamRequest<int>
    {
    }

    [Fact]
    public async Task CreateStream_No_Handler_Yields_Empty()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        List<int> result = [];
        await foreach (int i in mediator.CreateStream(new StreamRequest()))
            result.Add(i);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateStream_Null_Request_Throws()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        await Assert.ThrowsAsync<NullReferenceException>(async () =>
        {
            IAsyncEnumerable<int> stream = mediator.CreateStream<int>(null!);
            await foreach (int _ in stream)
            {
                // Intentionally left empty to consume the stream for test purposes.
            }
        });
    }

    [Fact]
    public async Task SendDynamic_Null_Request_Throws()
    {
        Mediator mediator = new(new EmptyServiceFactory());
        await Assert.ThrowsAsync<NullReferenceException>(() => mediator.Send((object)null!));
    }
}
