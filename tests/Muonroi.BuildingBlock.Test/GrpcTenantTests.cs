namespace Muonroi.BuildingBlock.Test;

public class GrpcTenantTests
{
    private class TestService(MAuthenticateInfoContext auth) : BaseGrpcService(auth)
    {
        public Metadata BuildMetadata()
        {
            return CreateMetadata();
        }
    }

    [Fact]
    public void CreateMetadata_Adds_TenantId_From_Context()
    {
        MAuthenticateInfoContext auth = new(false);
        TenantContext.CurrentTenantId = "t1";
        TestService svc = new(auth);

        Metadata meta = svc.BuildMetadata();

        string? value = meta.FirstOrDefault(m => m.Key == CustomHeader.TenantId)?.Value;
        Assert.Equal("t1", value);
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public void CreateMetadata_Adds_TenantId_From_AuthContext_When_RuntimeTenantIsMissing()
    {
        MAuthenticateInfoContext auth = new(false)
        {
            TenantId = "t-auth"
        };
        TenantContext.CurrentTenantId = null;
        TestService svc = new(auth);

        Metadata meta = svc.BuildMetadata();

        string? value = meta.FirstOrDefault(m => m.Key == CustomHeader.TenantId)?.Value;
        Assert.Equal("t-auth", value);
    }
}
