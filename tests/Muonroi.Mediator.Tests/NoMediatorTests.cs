namespace Muonroi.Mediator.Tests;

public class NoMediatorTests
{
    private class DummyRequest : IRequest<string>
    {
    }

    private class DummyNotification : INotification
    {
    }

    private class StreamRequest : IStreamRequest<int>
    {
    }

    private class NonGenericRequest : IRequest
    {
    }

    [Fact]
    public async Task CreateStream_Generic_Returns_Empty()
    {
        NoMediator mediator = new();
        List<int> result = [];

        await foreach (int item in mediator.CreateStream(new StreamRequest()))
        {
            result.Add(item);
        }

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateStream_Generic_Null_Returns_Empty()
    {
        NoMediator mediator = new();
        List<int> result = [];

        await foreach (int item in mediator.CreateStream<int>(null!))
        {
            result.Add(item);
        }

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateStream_Object_Returns_Empty()
    {
        NoMediator mediator = new();
        List<object?> result = [];

        await foreach (object? item in mediator.CreateStream(new object()))
        {
            result.Add(item);
        }

        Assert.Empty(result);
    }

    [Fact]
    public async Task CreateStream_Object_Null_Returns_Empty()
    {
        NoMediator mediator = new();
        List<object?> result = [];

        await foreach (object? item in mediator.CreateStream((object)null!))
        {
            result.Add(item);
        }

        Assert.Empty(result);
    }

    [Fact]
    public async Task Publish_Generic_Completes()
    {
        NoMediator mediator = new();

        Task task = mediator.Publish(new DummyNotification());
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Publish_Generic_Null_Completes()
    {
        NoMediator mediator = new();

        Task task = mediator.Publish<DummyNotification>(null!);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Publish_Object_Completes()
    {
        NoMediator mediator = new();

        Task task = mediator.Publish((object)new DummyNotification());
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Publish_Object_Null_Completes()
    {
        NoMediator mediator = new();

        Task task = mediator.Publish((object)null!);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Send_Generic_Returns_Default()
    {
        NoMediator mediator = new();

        string result = await mediator.Send(new DummyRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task Send_Generic_Null_Returns_Default()
    {
        NoMediator mediator = new();

        string result = await mediator.Send<string>(null!);

        Assert.Null(result);
    }

    [Fact]
    public async Task Send_NonGeneric_Completes()
    {
        NoMediator mediator = new();

        Task task = mediator.Send(new NonGenericRequest());
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Send_NonGeneric_Request_Instance_Completes()
    {
        NoMediator mediator = new();

        Task task = mediator.Send((IRequest)new NonGenericRequest());
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Send_Object_Returns_Null()
    {
        NoMediator mediator = new();

        object? result = await mediator.Send((object)new NonGenericRequest());

        Assert.Null(result);
    }

    [Fact]
    public async Task Send_Object_Null_Returns_Null()
    {
        NoMediator mediator = new();

        object? result = await mediator.Send((object)null!);

        Assert.Null(result);
    }
}
