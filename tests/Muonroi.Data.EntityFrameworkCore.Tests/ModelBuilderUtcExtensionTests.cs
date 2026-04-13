using Microsoft.EntityFrameworkCore.Metadata;
using Muonroi.Core.Abstractions.SeedWorks;

namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class ModelBuilderUtcExtensionTests
{
    private sealed class UtcTestDbContext(DbContextOptions<UtcTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public void UseUtcDateTime_Applies_Converter_To_DateTime_Properties()
    {
        DbContextOptions<UtcTestDbContext> options = new DbContextOptionsBuilder<UtcTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using UtcTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        userType.Should().NotBeNull();

        IProperty? creationTimeProp = userType!.FindProperty(nameof(MEntity.CreationTime));
        creationTimeProp.Should().NotBeNull();
        creationTimeProp!.GetValueConverter().Should().NotBeNull();
    }

    [Fact]
    public void UseUtcDateTime_Applies_Converter_To_Nullable_DateTime_Properties()
    {
        DbContextOptions<UtcTestDbContext> options = new DbContextOptionsBuilder<UtcTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using UtcTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        IProperty? lastModProp = userType!.FindProperty(nameof(MEntity.LastModificationTime));
        lastModProp.Should().NotBeNull();
        lastModProp!.GetValueConverter().Should().NotBeNull();
    }
}
