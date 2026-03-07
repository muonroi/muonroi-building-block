using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Saga;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.Messaging.Abstractions.Contracts;
using NSubstitute;
using Xunit;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MSagaDbContextTests
{
    public class TestSaga : IMuonroiSaga
    {
        public Guid CorrelationId { get; set; }
        public string? TenantId { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? LastModificationTime { get; set; }
    }

    public class TestSagaDbContext(
        DbContextOptions options,
        IMediator mediator,
        ISystemExecutionContextAccessor? executionContextAccessor = null)
        : MSagaDbContext(options, mediator, executionContextAccessor: executionContextAccessor)
    {
        public DbSet<TestSaga> TestSagas { get; set; }
    }

    [Fact]
    public async Task SaveChangesAsync_AutoInjects_TenantId_And_Timestamps()
    {
        // Arrange
        DbContextOptions options = new DbContextOptionsBuilder<TestSagaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IMediator mediator = Substitute.For<IMediator>();
        ISystemExecutionContextAccessor accessor = Substitute.For<ISystemExecutionContextAccessor>();
        ISystemExecutionContext executionContext = Substitute.For<ISystemExecutionContext>();

        string expectedTenant = "tenant-saga-1";
        executionContext.TenantId.Returns(expectedTenant);
        accessor.Get().Returns(executionContext);

        using TestSagaDbContext db = new(options, mediator, accessor);

        TestSaga saga = new() { CorrelationId = Guid.NewGuid() };

        // Act
        db.TestSagas.Add(saga);
        await db.SaveChangesAsync();

        // Assert
        Assert.Equal(expectedTenant, saga.TenantId);
        Assert.NotEqual(default, saga.CreationTime);
        Assert.NotNull(saga.LastModificationTime);
    }
}
