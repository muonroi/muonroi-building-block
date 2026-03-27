using FluentAssertions;
using Moq;
using Muonroi.Data.Abstractions.UnitOfWork;
using Xunit;

namespace Muonroi.Data.Abstractions.Tests;

public class MultiDbUnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_Should_Aggregate_Results_From_All_Contexts()
    {
        Mock<IMDataContext> ctx1 = new();
        Mock<IMDataContext> ctx2 = new();
        ctx1.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        ctx2.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(5);

        using MultiDbUnitOfWork uow = new(ctx1.Object, ctx2.Object);

        int result = await uow.SaveChangesAsync();

        result.Should().Be(8);
        ctx1.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx2.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_With_No_Contexts_Should_Return_Zero()
    {
        using MultiDbUnitOfWork uow = new();

        int result = await uow.SaveChangesAsync();

        result.Should().Be(0);
    }

    [Fact]
    public void Dispose_Should_Dispose_All_Contexts()
    {
        Mock<IMDataContext> ctx1 = new();
        Mock<IMDataContext> ctx2 = new();

        MultiDbUnitOfWork uow = new(ctx1.Object, ctx2.Object);
        uow.Dispose();

        ctx1.Verify(x => x.Dispose(), Times.Once);
        ctx2.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_Should_Call_Contexts_In_Order()
    {
        List<int> order = [];
        Mock<IMDataContext> ctx1 = new();
        Mock<IMDataContext> ctx2 = new();
        ctx1.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(1))
            .ReturnsAsync(1);
        ctx2.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add(2))
            .ReturnsAsync(2);

        using MultiDbUnitOfWork uow = new(ctx1.Object, ctx2.Object);
        await uow.SaveChangesAsync();

        order.Should().Equal(1, 2);
    }
}
