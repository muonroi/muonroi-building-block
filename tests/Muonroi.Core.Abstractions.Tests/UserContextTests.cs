namespace Muonroi.Core.Abstractions.Tests;

public class UserContextTests
{
    [Fact]
    public void CurrentUserGuid_Get_Set_Works()
    {
        try
        {
            UserContext.CurrentUserGuid = null;
            UserContext.CurrentUserGuid.Should().BeNull();

            UserContext.CurrentUserGuid = "guid";
            UserContext.CurrentUserGuid.Should().Be("guid");
        }
        finally
        {
            UserContext.CurrentUserGuid = null;
        }
    }

    [Fact]
    public void CurrentUsername_Get_Set_Works()
    {
        try
        {
            UserContext.CurrentUsername = null;
            UserContext.CurrentUsername.Should().BeNull();

            UserContext.CurrentUsername = "name";
            UserContext.CurrentUsername.Should().Be("name");
        }
        finally
        {
            UserContext.CurrentUsername = null;
        }
    }
}
