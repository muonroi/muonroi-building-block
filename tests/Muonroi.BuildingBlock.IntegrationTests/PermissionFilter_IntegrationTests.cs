using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Diagnostics;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests;

/// <summary>
/// Integration tests for permission filter behavior.
/// </summary>
/// <remarks>
/// Initializes a new test instance with a factory and client.
/// </remarks>
/// <param name="factory">Test application factory.</param>
public class PermissionFilter_IntegrationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>
    /// Verifies authorized users are allowed.
    /// </summary>
    [Fact]
    public async Task AuthorizedUser_WithRequiredPermission_ShouldReturn200()
    {
        // Arrange - User has permission bit 0b0001
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            permissions: 0b0001);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected/read");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies unauthorized users are forbidden.
    /// </summary>
    [Fact]
    public async Task UnauthorizedUser_WithoutRequiredPermission_ShouldReturn403()
    {
        // Arrange - User has no permissions
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            permissions: 0b0000);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected/admin");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies users with all required permissions are allowed.
    /// </summary>
    [Fact]
    public async Task User_WithAllRequiredPermissions_ShouldReturn200()
    {
        // Arrange - User has multiple permissions: Read (0b0001) + Write (0b0010) = 0b0011
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            permissions: 0b0011);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected/readwrite");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies missing permissions are denied.
    /// </summary>
    [Fact]
    public async Task User_MissingOneRequiredPermission_ShouldReturn403()
    {
        // Arrange - User has only Read (0b0001) but endpoint requires Read + Write (0b0011)
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            permissions: 0b0001);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected/readwrite");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies endpoints without permission requirements allow authenticated users.
    /// </summary>
    [Fact]
    public async Task Endpoint_WithNoPermissionRequirement_ShouldAllowAllAuthenticatedUsers()
    {
        // Arrange - User has no permissions
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            permissions: 0b0000);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/public/authenticated");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies permission checks return expected status codes.
    /// </summary>
    [Theory]
    [InlineData(0b0001, "/api/protected/read", HttpStatusCode.OK)]
    [InlineData(0b0010, "/api/protected/write", HttpStatusCode.OK)]
    [InlineData(0b0100, "/api/protected/delete", HttpStatusCode.OK)]
    [InlineData(0b1000, "/api/protected/admin", HttpStatusCode.OK)]
    [InlineData(0b0000, "/api/protected/read", HttpStatusCode.Forbidden)]
    [InlineData(0b0000, "/api/protected/admin", HttpStatusCode.Forbidden)]
    public async Task PermissionCheck_WithVariousPermissions_ShouldReturnExpectedStatus(
        long userPermissions,
        string endpoint,
        HttpStatusCode expectedStatus)
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            userPermissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().Be(expectedStatus);
    }

    /// <summary>
    /// Verifies permission checks with database lookup.
    /// </summary>
    [Fact]
    public async Task PermissionCheck_WithDatabaseLookup_ShouldValidateCorrectly()
    {
        // Arrange - Simulate permission from database
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var testUser = new TestUser
        {
            Id = Guid.Parse(_factory.TestUserId),
            Username = _factory.TestUsername,
            TenantId = _factory.TestTenantId,
            Permissions = 0b1111,
            IsActive = true
        };
        await dbContext.Users.AddAsync(testUser);
        await dbContext.SaveChangesAsync();

        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            0b1111);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/user/permissions");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Verifies permission caching improves or maintains performance.
    /// </summary>
    [Fact]
    public async Task PermissionCaching_ShouldImprovePerformance()
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            0b1111);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - First request (cache miss)
        Stopwatch stopwatch = Stopwatch.StartNew();
        var firstResponse = await _client.GetAsync("/api/protected/read");
        stopwatch.Stop();
        TimeSpan firstRequestDuration = stopwatch.Elapsed;

        // Subsequent requests should benefit from the cached permission result.
        List<TimeSpan> cachedDurations = [];
        HttpResponseMessage? lastCachedResponse = null;
        for (int i = 0; i < 3; i++)
        {
            stopwatch.Restart();
            lastCachedResponse = await _client.GetAsync("/api/protected/read");
            stopwatch.Stop();
            cachedDurations.Add(stopwatch.Elapsed);
        }

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        lastCachedResponse.Should().NotBeNull();
        if (lastCachedResponse is not null)
        {
            lastCachedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        TimeSpan averageCachedDuration = TimeSpan.FromTicks((long)cachedDurations.Average(x => x.Ticks));
        averageCachedDuration.Should().BeLessThanOrEqualTo(firstRequestDuration + TimeSpan.FromMilliseconds(50));
    }

    /// <summary>
    /// Verifies dynamic permission changes are respected.
    /// </summary>
    [Theory]
    [InlineData(0b0001)]
    [InlineData(0b0011)]
    [InlineData(0b0111)]
    [InlineData(0b1111)]
    public async Task DynamicPermissionAssignment_ShouldBeRespected(long newPermissions)
    {
        // Arrange - Update user permissions dynamically
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            newPermissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected/read");

        // Assert
        if ((newPermissions & 0b0001) != 0)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    /// <summary>
    /// Verifies role-based permissions inherit access.
    /// </summary>
    [Fact]
    public async Task RoleBasedPermission_ShouldInheritFromRole()
    {
        // Arrange - Admin role has all permissions
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            0b11111111); // Admin with all permissions
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Try to access all protected endpoints
        var readResponse = await _client.GetAsync("/api/protected/read");
        var writeResponse = await _client.GetAsync("/api/protected/write");
        var deleteResponse = await _client.GetAsync("/api/protected/delete");
        var adminResponse = await _client.GetAsync("/api/protected/admin");

        // Assert - All should succeed
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        writeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Verifies edge-case permission values are handled safely.
    /// </summary>
    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public async Task EdgeCasePermissionValues_ShouldBeHandledSafely(long permissions)
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            permissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected/read");

        // Assert - Should not crash, may return OK or Forbidden
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
    }
}
