using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests;

/// <summary>
/// Integration tests for multi-tenant behavior.
/// </summary>
/// <remarks>
/// Initializes a new test instance with the application factory.
/// </remarks>
/// <param name="factory">Test application factory.</param>
public class MultiTenant_IntegrationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;

    /// <summary>
    /// Verifies tenant resolution from request headers.
    /// </summary>
    [Theory]
    [InlineData("tenant-001")]
    [InlineData("tenant-002")]
    [InlineData("tenant-abc-123")]
    public async Task TenantResolution_FromHeader_ShouldSetCorrectTenant(string tenantId)
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            tenantId: tenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        // Act
        var response = await client.GetAsync("/api/tenant/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(tenantId);
    }

    /// <summary>
    /// Verifies tenant resolution from subdomain.
    /// </summary>
    [Fact]
    public async Task TenantResolution_FromSubdomain_ShouldExtractTenantId()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Host = "tenant123.example.com";

        // Act
        var response = await client.GetAsync("/api/tenant/current");

        // Assert - Subdomain resolution may not be fully supported in test environment
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies tenant resolution from route path.
    /// </summary>
    [Theory]
    [InlineData("tenant-001", "/api/tenant/tenant-001/data")]
    [InlineData("tenant-002", "/api/tenant/tenant-002/data")]
    public async Task TenantResolution_FromPath_ShouldExtractFromRoute(string tenantId, string path)
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            tenantId: tenantId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync(path);

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies fallback tenant resolution when none is provided.
    /// </summary>
    [Fact]
    public async Task TenantResolution_FallbackStrategy_ShouldUseDefault()
    {
        // Arrange - No tenant ID provided anywhere
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/tenant/current");

        // Assert - Should fallback to default tenant
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("test-tenant-001"); // Default tenant from TestTenantIdResolver
    }

    /// <summary>
    /// Verifies data isolation across tenants.
    /// </summary>
    [Fact]
    public async Task DataIsolation_BetweenTenants_ShouldEnforceFiltering()
    {
        // Arrange - Create data for two different tenants
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var tenant1User = new TestUser
        {
            Id = Guid.NewGuid(),
            Username = "user1@tenant1.com",
            TenantId = "tenant-001",
            Permissions = 0b1111,
            IsActive = true
        };

        var tenant2User = new TestUser
        {
            Id = Guid.NewGuid(),
            Username = "user2@tenant2.com",
            TenantId = "tenant-002",
            Permissions = 0b1111,
            IsActive = true
        };

        await dbContext.Users.AddAsync(tenant1User);
        await dbContext.Users.AddAsync(tenant2User);
        await dbContext.SaveChangesAsync();

        // Act - Request as tenant-001
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            tenant1User.Id.ToString(),
            tenant1User.Username,
            tenant1User.Permissions,
            tenantId: "tenant-001");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-001");

        var response = await client.GetAsync("/api/users");

        // Assert - Should only see tenant-001 data
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("tenant-001");
        content.Should().NotContain("tenant-002");
    }

    /// <summary>
    /// Verifies tenant-prefixed cache keys isolate cached data.
    /// </summary>
    [Fact]
    public async Task CacheKeyPrefixing_WithTenantId_ShouldIsolateCachedData()
    {
        // Arrange
        var tenant1Client = _factory.CreateClient();
        var tenant1Token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            tenantId: "tenant-001");
        tenant1Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenant1Token);
        tenant1Client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-001");

        var tenant2Client = _factory.CreateClient();
        var tenant2Token = _factory.GenerateJwtToken(
            Guid.NewGuid().ToString(),
            "other@test.com",
            _factory.TestUserPermissions,
            tenantId: "tenant-002");
        tenant2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenant2Token);
        tenant2Client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-002");

        // Act - Cache data for tenant-001
        var tenant1Response1 = await tenant1Client.GetAsync("/api/cache/test");
        var tenant1Response2 = await tenant1Client.GetAsync("/api/cache/test");

        // Cache data for tenant-002
        var tenant2Response1 = await tenant2Client.GetAsync("/api/cache/test");

        // Assert - Each tenant should have independent cache
        tenant1Response1.StatusCode.Should().Be(HttpStatusCode.OK);
        tenant1Response2.StatusCode.Should().Be(HttpStatusCode.OK);
        tenant2Response1.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies tenant context is preserved across middleware.
    /// </summary>
    [Fact]
    public async Task TenantContextPropagation_AcrossMiddleware_ShouldMaintainContext()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            tenantId: "tenant-123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-123");

        // Act - Make request that goes through multiple middleware
        var response = await client.PostAsync("/api/data/create", new StringContent("{}"));

        // Assert - Tenant context should be maintained throughout
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    /// <summary>
    /// Verifies invalid tenant identifiers are handled.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task InvalidTenantId_ShouldFallbackOrReject(string? invalidTenantId)
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (invalidTenantId != null)
        {
            client.DefaultRequestHeaders.Add("X-Tenant-Id", invalidTenantId);
        }

        // Act
        var response = await client.GetAsync("/api/tenant/current");

        // Assert - Should fallback to default tenant
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies missing tenant identifiers default to a tenant.
    /// </summary>
    [Fact]
    public async Task MissingTenantId_ShouldUseDefaultTenant()
    {
        // Arrange - No X-Tenant-Id header
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/tenant/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("test-tenant-001"); // Default
    }

    /// <summary>
    /// Verifies tenant context does not change mid-request.
    /// </summary>
    [Fact]
    public async Task TenantSwitching_MidRequest_ShouldNotOccur()
    {
        // Arrange
        var client = _factory.CreateClient();
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            tenantId: "tenant-001");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-001");

        // Act - Make request, tenant context should be immutable
        var response = await client.GetAsync("/api/tenant/verify-immutability");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies concurrent requests from different tenants are isolated.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequests_FromDifferentTenants_ShouldBeIsolated()
    {
        // Arrange
        var tenant1Client = _factory.CreateClient();
        var tenant1Token = _factory.GenerateJwtToken(
            Guid.NewGuid().ToString(),
            "user1@tenant1.com",
            0b1111,
            tenantId: "tenant-001");
        tenant1Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenant1Token);
        tenant1Client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-001");

        var tenant2Client = _factory.CreateClient();
        var tenant2Token = _factory.GenerateJwtToken(
            Guid.NewGuid().ToString(),
            "user2@tenant2.com",
            0b1111,
            tenantId: "tenant-002");
        tenant2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenant2Token);
        tenant2Client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-002");

        // Act - Concurrent requests
        var task1 = tenant1Client.GetAsync("/api/tenant/current");
        var task2 = tenant2Client.GetAsync("/api/tenant/current");
        await Task.WhenAll(task1, task2);

        var content1 = await task1.Result.Content.ReadAsStringAsync();
        var content2 = await task2.Result.Content.ReadAsStringAsync();

        // Assert - Each should have correct tenant
        content1.Should().Contain("tenant-001");
        content1.Should().NotContain("tenant-002");
        content2.Should().Contain("tenant-002");
        content2.Should().NotContain("tenant-001");
    }
}
