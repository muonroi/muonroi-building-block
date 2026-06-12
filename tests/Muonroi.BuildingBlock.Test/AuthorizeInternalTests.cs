namespace Muonroi.BuildingBlock.Test;

public class AuthorizeInternalTests
{
    private static object? InvokeGetClaimValue<T>(List<Claim> claims, string type)
    {
        Type t = typeof(AuthorizeInternal);
        MethodInfo mi =
            t.GetMethod("GetClaimValue", BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeof(T));
        return mi.Invoke(null, [claims, type]);
    }

    [Fact]
    public void GetClaimValue_Returns_Value_When_Exists()
    {
        List<Claim> claims = [new("a", "42")];
        object? result = InvokeGetClaimValue<int>(claims, "a");
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetClaimValue_Returns_Default_When_Missing()
    {
        List<Claim> claims = [new("a", "1")];
        object? result = InvokeGetClaimValue<int>(claims, "b");
        Assert.Null(result);
    }

    [Fact]
    public void GetClaimValue_Returns_Default_When_Value_Empty()
    {
        List<Claim> claims = [new("a", "")];
        object? result = InvokeGetClaimValue<string>(claims, "a");
        Assert.Null(result);
    }

    [Fact]
    public void GetClaimValue_Empty_Key_Returns_Default()
    {
        List<Claim> claims = [new("a", "v")];
        object? result = InvokeGetClaimValue<string>(claims, string.Empty);
        Assert.Null(result);
    }
}
