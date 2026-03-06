namespace Muonroi.BuildingBlock.Test;

public class MUserLoginAttemptAndTokenTests
{
    [Fact]
    public void LoginAttempt_Result_Get_Returns_Set_Value_Or_Default()
    {
        MUserLoginAttempt attempt = new();
        Assert.Equal((MLoginResultType)0, attempt.Result);
        attempt.Result = MLoginResultType.LockedOut;
        Assert.Equal(MLoginResultType.LockedOut, attempt.Result);
    }

    [Fact]
    public void UserToken_UserId_Get_Returns_Set_Value()
    {
        MUserToken token = new();
        Assert.Equal(0, token.UserId);
        token.UserId = 10;
        Assert.Equal(10, token.UserId);
    }

    [Fact]
    public void UserToken_LoginProvider_Get_Returns_Set_Value_Or_Empty()
    {
        MUserToken token = new();
        Assert.Equal(string.Empty, token.LoginProvider);
        token.LoginProvider = "prov";
        Assert.Equal("prov", token.LoginProvider);
    }

    [Fact]
    public void UserToken_Name_Get_Returns_Set_Value_Or_Empty()
    {
        MUserToken token = new();
        Assert.Equal(string.Empty, token.Name);
        token.Name = "name";
        Assert.Equal("name", token.Name);
    }

    [Fact]
    public void UserToken_Value_Get_Returns_Set_Value_Or_Empty()
    {
        MUserToken token = new();
        Assert.Equal(string.Empty, token.Value);
        token.Value = "v";
        Assert.Equal("v", token.Value);
    }

    [Fact]
    public void UserToken_ExpireDate_Get_Returns_Set_Value_Or_Null()
    {
        MUserToken token = new();
        Assert.Null(token.ExpireDate);
        DateTime dt = DateTime.UtcNow.AddDays(1);
        token.ExpireDate = dt;
        Assert.Equal(dt, token.ExpireDate);
    }

    [Fact]
    public void UserToken_Default_Ctor_Sets_Defaults()
    {
        MUserToken token = new();
        Assert.Equal(0, token.UserId);
        Assert.Equal(string.Empty, token.LoginProvider);
        Assert.Equal(string.Empty, token.Name);
        Assert.Equal(string.Empty, token.Value);
        Assert.Null(token.ExpireDate);
    }

    [Fact]
    public void UserToken_Ctor_With_Params_Sets_Values()
    {
        DateTime dt = DateTime.UtcNow;
        MUserToken token = new(1, "p", "n", "v", dt);
        Assert.Equal(1, token.UserId);
        Assert.Equal("p", token.LoginProvider);
        Assert.Equal("n", token.Name);
        Assert.Equal("v", token.Value);
        Assert.Equal(dt, token.ExpireDate);
    }

    [Fact]
    public void UserToken_Ctor_Allows_Null_Strings()
    {
        MUserToken token = new(2, null!, null!, null!, null);
        Assert.Equal(2, token.UserId);
        Assert.Null(token.LoginProvider);
        Assert.Null(token.Name);
        Assert.Null(token.Value);
        Assert.Null(token.ExpireDate);
    }
}
