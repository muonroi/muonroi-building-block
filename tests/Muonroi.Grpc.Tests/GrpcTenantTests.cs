namespace Muonroi.Grpc.Tests;

public class GrpcTenantTests
{
    private sealed class TestService(ISystemExecutionContextAccessor accessor)
        : BaseGrpcService(accessor, new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.Grpc]
        })
    {
        public Metadata BuildMetadata()
        {
            return CreateMetadata();
        }
    }

    [Fact]
    public void CreateMetadata_Adds_TenantId_From_ExecutionContext()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(
            tenantId: "tenant-1",
            userId: "user-1",
            username: "tester",
            correlationId: "corr-1",
            accessToken: null,
            apiKey: "api-key",
            isAuthenticated: true,
            permissions: [],
            sourceType: "test"));

        TestService service = new(accessor);

        Metadata metadata = service.BuildMetadata();

        metadata.GetValue(CustomHeader.TenantId).Should().Be("tenant-1");
        metadata.GetValue(CustomHeader.CorrelationId).Should().Be("corr-1");
        metadata.GetValue(CustomHeader.ApiKey).Should().Be("api-key");
    }

    [Fact]
    public void CreateMetadata_Only_Emits_CorrelationId_When_Context_Is_Empty()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(SystemExecutionContext.Empty);

        TestService service = new(accessor);

        Metadata metadata = service.BuildMetadata();

        metadata.Should().ContainSingle();
        metadata.GetValue(CustomHeader.CorrelationId).Should().NotBeNullOrWhiteSpace();
        metadata.GetValue(CustomHeader.TenantId).Should().BeNull();
        metadata.GetValue(CustomHeader.ApiKey).Should().BeNull();
    }
}
