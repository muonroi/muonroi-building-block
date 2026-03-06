namespace Muonroi.BuildingBlock.Test;

public class UserContextTests
{
    [Fact]
    public void UserGuid_Get_Set_Works()
    {
        try
        {
            UserContext.CurrentUserGuid = null;
            Assert.Null(UserContext.CurrentUserGuid);
            UserContext.CurrentUserGuid = "guid";
            Assert.Equal("guid", UserContext.CurrentUserGuid);
        }
        finally
        {
            UserContext.CurrentUserGuid = null;
        }
    }

    [Fact]
    public void Username_Get_Set_Works()
    {
        try
        {
            UserContext.CurrentUsername = null;
            Assert.Null(UserContext.CurrentUsername);
            UserContext.CurrentUsername = "name";
            Assert.Equal("name", UserContext.CurrentUsername);
        }
        finally
        {
            UserContext.CurrentUsername = null;
        }
    }
}
