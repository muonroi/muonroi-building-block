

namespace Muonroi.BuildingBlock.Test;

public class CreatePermissionRequestModelTests
{
    [Fact]
    public void Name_Returns_Value_Or_Null()
    {
        CreatePermissionRequestModel model = new() { Name = "perm" };
        Assert.Equal("perm", model.Name);
        model.Name = null!;
        Assert.Null(model.Name);
    }

    [Fact]
    public void IsGranted_Returns_Default_Value()
    {
        CreatePermissionRequestModel model = new() { Name = "perm" };
        Assert.True(model.IsGranted);
        model.IsGranted = false;
        Assert.False(model.IsGranted);
    }

    [Fact]
    public void Discriminator_Returns_Value_Or_Empty()
    {
        CreatePermissionRequestModel model = new() { Name = "perm" };
        Assert.Equal(string.Empty, model.Discriminator);
        model.Discriminator = "disc";
        Assert.Equal("disc", model.Discriminator);
    }
}
