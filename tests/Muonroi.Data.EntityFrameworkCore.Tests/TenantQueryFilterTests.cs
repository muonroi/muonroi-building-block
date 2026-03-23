using Microsoft.EntityFrameworkCore.Metadata;
using Muonroi.Core.Abstractions.SeedWorks;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for tenant query filter and creator filter application in MDbContext.OnModelCreating.
/// </summary>
public class TenantQueryFilterTests
{
    private sealed class FilterTestDbContext(DbContextOptions<FilterTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void OnModelCreating_Applies_CreatorFilter_To_MEntity_Types()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using FilterTestDbContext db = new(options);
        IModel model = db.Model;

        // MUser derives from MEntity and has CreatorUserId, so should have a query filter
        IEntityType? userType = model.FindEntityType(typeof(MUser));
        userType.Should().NotBeNull();

        System.Linq.Expressions.LambdaExpression? filter = userType!.GetQueryFilter();
        filter.Should().NotBeNull("MUser derives from MEntity and should have a creator filter");
    }

    [Fact]
    public async Task CreatorFilter_Allows_All_When_CurrentUserGuid_Null()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        TenantContext.CurrentTenantId = null;
        UserContext.CurrentUserGuid = null;

        using (FilterTestDbContext seedDb = new(options))
        {
            seedDb.Users.Add(new MUser
            {
                UserName = "userA",
                EmailAddress = "a@test.com",
                Name = "A",
                Surname = "User",
                Password = "p",
                CreatorUserId = Guid.NewGuid()
            });
            seedDb.Users.Add(new MUser
            {
                UserName = "userB",
                EmailAddress = "b@test.com",
                Name = "B",
                Surname = "User",
                Password = "p",
                CreatorUserId = Guid.NewGuid()
            });
            await seedDb.SaveChangesAsync();
        }

        // When CurrentUserGuid is null, all entities should be visible
        UserContext.CurrentUserGuid = null;
        using (FilterTestDbContext queryDb = new(options))
        {
            List<MUser> users = await queryDb.Users.ToListAsync();
            users.Should().HaveCount(2);
        }
    }

    [Fact]
    public void OnModelCreating_Applies_UtcDateTime_Converters()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using FilterTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        IProperty? creationTime = userType!.FindProperty("CreationTime");
        creationTime.Should().NotBeNull();
        creationTime!.GetValueConverter().Should().NotBeNull();
    }
}
