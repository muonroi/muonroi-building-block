namespace Muonroi.BuildingBlock.Test;

public class LoginResponseModelTests
{
    [Fact]
    public void FullName_Returns_Concatenated_Name_And_Surname()
    {
        LoginResponseModel model = new()
        {
            Name = "John",
            Surname = "Doe"
        };
        Assert.Equal("John Doe", model.FullName);
    }

    [Fact]
    public void FullName_Returns_Space_When_Null_Or_Not_Set()
    {
        LoginResponseModel model = new();
        Assert.Equal(" ", model.FullName);
        model.Name = null!;
        model.Surname = null!;
        Assert.Equal(" ", model.FullName);
    }
}
