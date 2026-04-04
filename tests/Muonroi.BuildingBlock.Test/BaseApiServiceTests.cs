using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class BaseApiServiceTests
{
    private class TestService(MAuthenticateInfoContext ctx) : BaseApiService(ctx)
    {
        public MAuthenticateInfoContext? Ctx => AuthContext;

        public T Build<T>(string url) where T : class
        {
            return CreateClient<T>(url);
        }
    }

    public interface ITestApi
    {
    }

    [Fact]
    public void Constructor_Assigns_Context()
    {
        MAuthenticateInfoContext ctx = new(false);
        TestService svc = new(ctx);
        Assert.Same(ctx, svc.Ctx);
    }

    [Fact]
    public void Constructor_Allows_Null_Context()
    {
        TestService svc = new(null!);
        Assert.Null(svc.Ctx);
    }

    [Fact]
    public void CreateClient_Returns_Client()
    {
        TestService svc = new(new MAuthenticateInfoContext(false));
        ITestApi api = svc.Build<ITestApi>("http://localhost");
        Assert.NotNull(api);
    }

    [Fact]
    public void CreateClient_Null_BaseUrl_Throws()
    {
        TestService svc = new(new MAuthenticateInfoContext(false));
        Assert.Throws<MArgumentException>(() => svc.Build<ITestApi>(null!));
    }

    [Fact]
    public void CreateClient_Null_Context_Throws()
    {
        TestService svc = new(null!);
        Assert.Throws<MArgumentException>(() => svc.Build<ITestApi>("http://a"));
    }
}
