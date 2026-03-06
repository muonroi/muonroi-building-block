namespace Muonroi.BuildingBlock.Test;

public class MUserEntityTests
{
    [Fact]
    public void SignInTokenExpireTimeUtc_Get_Returns_Set_Value_Or_Null()
    {
        MUser user = new();
        Assert.Null(user.SignInTokenExpireTimeUtc);
        DateTime now = DateTime.UtcNow;
        user.SignInTokenExpireTimeUtc = now;
        Assert.Equal(now, user.SignInTokenExpireTimeUtc);
    }

    [Fact]
    public void SetNewPasswordResetCode_Generates_New_Code()
    {
        MUser user = new();
        Assert.Null(user.PasswordResetCode);
        user.SetNewPasswordResetCode();
        string first = user.PasswordResetCode!;
        Assert.False(string.IsNullOrEmpty(first));
        user.SetNewPasswordResetCode();
        Assert.NotEqual(first, user.PasswordResetCode);
    }
}
