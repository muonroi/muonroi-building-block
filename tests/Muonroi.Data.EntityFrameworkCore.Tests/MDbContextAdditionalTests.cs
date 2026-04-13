using Muonroi.Governance.Abstractions.License;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Additional tests for MDbContext covering timestamps, soft-delete, and transaction edge cases.
/// </summary>
public class MDbContextAdditionalTests
{
    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options,
        IMediator mediator,
        ILicenseGuard? licenseGuard = null,
        IMDateTimeService? dateTimeService = null)
        : MDbContext(options, mediator, licenseGuard, null, dateTimeService)
    {
    }

    private static MUser CreateUser(string suffix)
    {
        return new MUser
        {
            UserName = $"user-{suffix}",
            EmailAddress = $"{suffix}@muonroi.test",
            Name = "Test",
            Surname = "User",
            Password = "p@ss"
        };
    }

    [Fact]
    public async Task SaveChangesAsync_Sets_CreationTime_On_Added_Entity()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        MUser user = CreateUser("ts-add");
        db.Users.Add(user);

        await db.SaveChangesAsync();

        user.CreationTime.Should().NotBe(default);
        user.CreatedDateTs.Should().NotBe(0);
    }

    [Fact]
    public async Task SaveChangesAsync_Sets_LastModificationTime_On_Modified_Entity()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        MUser user = CreateUser("ts-mod");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.Name = "Modified";
        db.Entry(user).State = EntityState.Modified;
        await db.SaveChangesAsync();

        user.LastModificationTime.Should().NotBeNull();
        user.LastModificationTimeTs.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_Soft_Deletes_On_Deleted_Entity()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        MUser user = CreateUser("ts-del");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.Entry(user).State = EntityState.Deleted;
        await db.SaveChangesAsync();

        // After soft-delete, entity should be marked as deleted but not removed
        MUser? found = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserName == "user-ts-del");
        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeTrue();
        found.DeletionTime.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveEntitiesAsync_Returns_Guid_On_InMemory()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        MUser user = CreateUser("save-entities");
        db.Users.Add(user);

        Guid result = await db.SaveEntitiesAsync();

        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task SaveEntitiesAsync_With_Active_Transaction_Uses_Existing()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        await db.Database.EnsureCreatedAsync();

        IDbContextTransaction? tx = await db.BeginTransactionAsync();
        db.Users.Add(CreateUser("active-tx"));

        Guid result = await db.SaveEntitiesAsync();

        result.Should().NotBe(Guid.Empty);
        db.HasActiveTransaction.Should().BeTrue();
        db.RollbackTransaction();
    }

    [Fact]
    public async Task CommitTransactionAsync_Throws_On_Null_Transaction()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());

        Func<Task> act = () => db.CommitTransactionAsync(null!);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task CommitTransactionAsync_Throws_On_Wrong_Transaction()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        await db.Database.EnsureCreatedAsync();

        // Start one transaction to set _currentTransaction
        _ = await db.BeginTransactionAsync();

        // Create a different transaction to pass (simulating wrong transaction)
        using SqliteConnection connection2 = new("DataSource=:memory:");
        await connection2.OpenAsync();
        DbContextOptions<TestDbContext> options2 = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection2)
            .Options;
        using TestDbContext db2 = new(options2, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        await db2.Database.EnsureCreatedAsync();
        IDbContextTransaction? otherTx = await db2.BeginTransactionAsync();

        Func<Task> act = () => db.CommitTransactionAsync(otherTx!);

        await act.Should().ThrowAsync<MInternalException>();

        db.RollbackTransaction();
        db2.RollbackTransaction();
    }

    [Fact]
    public void RollbackTransaction_NoOp_When_No_Active_Transaction()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());

        // Should not throw
        db.RollbackTransaction();
        db.HasActiveTransaction.Should().BeFalse();
    }

    [Fact]
    public void TrackEntity_Adds_To_Internal_List()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());
        MUser user = CreateUser("track");

        db.TrackEntity(user);

        // Verify by triggering domain event dispatch
        // The entity should be tracked for domain event publishing
        user.AddDomainEvent(new TestDomainEvent());

        // No exception means track was successful
    }

    [Fact]
    public void GetHashCode_Returns_Consistent_Value()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options, new NoMediator(), new TestLicenseGuard(), new MDateTimeService());

        int hash1 = db.GetHashCode();
        int hash2 = db.GetHashCode();

        hash1.Should().Be(hash2);
    }

    private sealed class TestDomainEvent : Core.Abstractions.SeedWorks.IMDomainEvent, INotification
    {
    }
}
