namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class SagaServiceCollectionExtensionsTests
{
    private class TestSagaDbContext(DbContextOptions options, IMediator mediator)
        : MSagaDbContext(options, mediator)
    {
    }

    [Fact]
    public void AddMuonroiSagaDbContext_Registers_Context()
    {
        ServiceCollection services = new();
        services.AddScoped<IMediator>(_ => new NoMediator());

        services.AddMuonroiSagaDbContext<TestSagaDbContext>(opts =>
            opts.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        TestSagaDbContext? ctx = scope.ServiceProvider.GetService<TestSagaDbContext>();

        ctx.Should().NotBeNull();
    }

    [Fact]
    public void AddMuonroiSagaDbContext_Returns_Same_ServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddMuonroiSagaDbContext<TestSagaDbContext>(opts =>
            opts.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

        result.Should().BeSameAs(services);
    }
}
