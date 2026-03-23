using Microsoft.EntityFrameworkCore.Metadata;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class CustomColumnOrderConventionTests
{
    private sealed class ConventionTestDbContext(DbContextOptions<ConventionTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void Customize_Applies_To_MEntity_Types()
    {
        DbContextOptions<ConventionTestDbContext> options = new DbContextOptionsBuilder<ConventionTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using ConventionTestDbContext db = new(options);

        // Use design-time model to access column order metadata
        IDesignTimeModel designTimeModel = db.GetService<IDesignTimeModel>();
        IModel model = designTimeModel.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        userType.Should().NotBeNull();

        IProperty? idProp = userType!.FindProperty(nameof(MEntity.Id));
        idProp.Should().NotBeNull();
        idProp!.GetColumnOrder().Should().Be(0);

        IProperty? entityIdProp = userType.FindProperty(nameof(MEntity.EntityId));
        entityIdProp.Should().NotBeNull();
        entityIdProp!.GetColumnOrder().Should().Be(1);
    }

    [Fact]
    public void Customize_Sets_Derived_Properties_After_Id_And_EntityId()
    {
        DbContextOptions<ConventionTestDbContext> options = new DbContextOptionsBuilder<ConventionTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using ConventionTestDbContext db = new(options);

        IDesignTimeModel designTimeModel = db.GetService<IDesignTimeModel>();
        IModel model = designTimeModel.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        userType.Should().NotBeNull();

        // Check that UserName has column order > 1 (after Id and EntityId)
        IProperty? userNameProp = userType!.FindProperty("UserName");
        userNameProp.Should().NotBeNull();
        int? order = userNameProp!.GetColumnOrder();
        order.Should().NotBeNull();
        order.Should().BeGreaterThan(1);
    }
}
