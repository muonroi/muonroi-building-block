namespace Muonroi.BuildingBlock.Test;

public class FakeMediatorTests
{
    private class DummyCommand : IRequest
    {
    }

    [Fact]
    public async Task Send_Object_Returns_Null()
    {
        FakeMediator mediator = new();

        object? result = await mediator.Send(new object());

        Assert.Null(result);
    }

    [Fact]
    public async Task Send_Generic_Completes_Successfully()
    {
        FakeMediator mediator = new();

        Task task = mediator.Send(new DummyCommand());
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }
}
