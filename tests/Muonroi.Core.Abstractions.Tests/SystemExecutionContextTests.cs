namespace Muonroi.Core.Abstractions.Tests;

public class SystemExecutionContextTests
{
    [Fact]
    public void Constructor_Normalizes_Whitespace_And_SourceType()
    {
        SystemExecutionContext context = new(
            tenantId: " tenant-a ",
            userId: " user-1 ",
            username: " admin ",
            correlationId: " corr-1 ",
            accessToken: " token ",
            apiKey: " api ",
            isAuthenticated: true,
            permissions: ["read"],
            sourceType: " HTTP ");

        context.TenantId.Should().Be("tenant-a");
        context.UserId.Should().Be("user-1");
        context.Username.Should().Be("admin");
        context.CorrelationId.Should().Be("corr-1");
        context.AccessToken.Should().Be("token");
        context.ApiKey.Should().Be("api");
        context.SourceType.Should().Be("http");
    }

    [Fact]
    public void Accessor_Get_Returns_Empty_When_Not_Set()
    {
        SystemExecutionContextAccessor accessor = new();

        accessor.Get().Should().BeSameAs(SystemExecutionContext.Empty);
    }

    [Fact]
    public void Scope_Sets_Context_And_Restores_Previous_On_Dispose()
    {
        SystemExecutionContextAccessor accessor = new();
        SystemExecutionContext previous = new(null, "u1", "name", "c1", null, null, true, [], "http");
        SystemExecutionContext current = new("tenant-a", "u2", "other", "c2", null, null, true, ["read"], "grpc");
        accessor.Set(previous);

        using (new SystemExecutionContextScope(accessor, current))
        {
            accessor.Get().Should().BeSameAs(current);
        }

        accessor.Get().Should().BeSameAs(previous);
    }

    [Fact]
    public void With_Creates_New_Context_With_Overridden_Values()
    {
        SystemExecutionContext original = new("tenant-a", "u1", "name", "c1", null, null, true, ["read"], "http");

        SystemExecutionContext updated = original.With(tenantId: "tenant-b", sourceType: "grpc");

        updated.TenantId.Should().Be("tenant-b");
        updated.SourceType.Should().Be("grpc");
        updated.UserId.Should().Be("u1");
    }
}
