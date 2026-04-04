using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Mediator.Tests;

public class MediatorExceptionTests
{
    private class EmptyServiceFactory
    {
        public object? Invoke(Type serviceType)
        {
            return null;
        }

        public static implicit operator ServiceFactory(EmptyServiceFactory factory)
        {
            return new ServiceFactory(factory.Invoke);
        }
    }

    [Fact]
    public async Task Send_Null_Request_Throws()
    {
        MMediator mediator = new(new EmptyServiceFactory());

        await Assert.ThrowsAsync<MArgumentException>(() => mediator.Send<string>(null!));
    }

    private class DummyRequest : IRequest<string>
    {
    }

    [Fact]
    public async Task Send_Handler_Not_Found_Throws()
    {
        MMediator mediator = new(new EmptyServiceFactory());

        await Assert.ThrowsAsync<MInternalException>(() => mediator.Send(new DummyRequest()));
    }

    private class TestNotification : INotification
    {
    }

    [Fact]
    public async Task Publish_Null_Event_Throws()
    {
        MMediator mediator = new(new EmptyServiceFactory());

        await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish<TestNotification>(null!));
    }

    [Fact]
    public async Task Publish_Object_NotINotification_Throws()
    {
        MMediator mediator = new(new EmptyServiceFactory());

        await Assert.ThrowsAsync<MInternalException>(() => mediator.Publish(new object()));
    }

    private class StreamRequest : IStreamRequest<int>
    {
    }

    [Fact]
    public async Task CreateStream_No_Handler_Yields_Empty()
    {
        MMediator mediator = new(new EmptyServiceFactory());
        List<int> result = [];

        await foreach (int item in mediator.CreateStream(new StreamRequest()))
        {
            result.Add(item);
        }

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateStream_Null_Request_Throws()
    {
        MMediator mediator = new(new EmptyServiceFactory());

        // v2: ArgumentNullException instead of NullReferenceException (direct null guard via ArgumentNullException.ThrowIfNull)
        await Assert.ThrowsAsync<MArgumentException>(async () =>
        {
            IAsyncEnumerable<int> stream = mediator.CreateStream<int>(null!);
            await foreach (int _ in stream)
            {
            }
        });
    }

    [Fact]
    public async Task SendDynamic_Null_Request_Throws()
    {
        MMediator mediator = new(new EmptyServiceFactory());

        // v2: ArgumentNullException instead of NullReferenceException (direct null guard via ArgumentNullException.ThrowIfNull)
        await Assert.ThrowsAsync<MArgumentException>(() => mediator.Send((object)null!));
    }
}
