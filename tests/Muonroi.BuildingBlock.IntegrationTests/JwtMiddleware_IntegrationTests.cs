using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests;

public class JwtMiddleware_IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public JwtMiddleware_IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidJwtToken_ShouldBeAccepted()
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredToken_ShouldBeRejected()
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            expiration: TimeSpan.FromSeconds(-10)); // Already expired
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissingToken_ShouldReturn401()
    {
        // Arrange - No token set

        // Act
        var response = await _client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MalformedToken_ShouldReturn401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "this.is.not.a.valid.jwt.token");

        // Act
        var response = await _client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenWithInvalidSignature_ShouldBeRejected()
    {
        // Arrange - Generate token with different secret
        var differentFactory = new CustomWebApplicationFactory
        {
            JwtSecretKey = "different-secret-key-32-characters-minimum-length"
        };
        var token = differentFactory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("UserIdentifier")]
    [InlineData("Username")]
    [InlineData("Permission")]
    public async Task TokenWithValidClaims_ShouldExtractCorrectly(string claimType)
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/claims/{claimType}");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TokenWithMissingRequiredClaim_ShouldBeHandled()
    {
        // Arrange - Generate minimal token without permission claim
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            0); // No permissions
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/permissions/check");

        // Assert
        // Should not crash, but may return 403 Forbidden
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(3600)]
    public async Task TokenWithValidLifetime_ShouldBeAccepted(int lifetimeSeconds)
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            expiration: TimeSpan.FromSeconds(lifetimeSeconds));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MultipleConcurrentRequests_WithDifferentTokens_ShouldBeHandledCorrectly()
    {
        // Arrange
        var user1Token = _factory.GenerateJwtToken("user1-id", "user1@test.com", 0b111);
        var user2Token = _factory.GenerateJwtToken("user2-id", "user2@test.com", 0b1111);

        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user1Token);
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);

        // Act
        var task1 = client1.GetAsync("/api/health");
        var task2 = client2.GetAsync("/api/health");
        await Task.WhenAll(task1, task2);

        // Assert
        task1.Result.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        task2.Result.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("test-tenant-001")]
    [InlineData("test-tenant-002")]
    [InlineData("")]
    public async Task TokenWithTenantClaim_ShouldPropagateTenantContext(string tenantId)
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            tenantId: tenantId);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!string.IsNullOrEmpty(tenantId))
        {
            _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        }

        // Act
        var response = await _client.GetAsync("/api/tenant/current");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenRefresh_Scenario_OldTokenExpired_NewTokenValid()
    {
        // Arrange - Simulate token refresh
        var oldToken = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            expiration: TimeSpan.FromSeconds(-5)); // Expired

        var newToken = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            expiration: TimeSpan.FromHours(1)); // Valid

        // Act - Old token should fail
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        var oldResponse = await _client.GetAsync("/api/protected");

        // New token should succeed
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        var newResponse = await _client.GetAsync("/api/protected");

        // Assert
        oldResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        newResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Bearer")]
    [InlineData("InvalidScheme validtoken")]
    public async Task InvalidAuthorizationHeader_ShouldReturn401(string authHeader)
    {
        // Arrange
        _client.DefaultRequestHeaders.Clear();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            _client.DefaultRequestHeaders.Add("Authorization", authHeader);
        }

        // Act
        var response = await _client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenWithSpecialCharactersInClaims_ShouldBeHandled()
    {
        // Arrange
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            "user+test@example.com", // Special char in username
            _factory.TestUserPermissions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/user/profile");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenValidation_ShouldEnforceClockSkewZero()
    {
        // Arrange - Token expires in 1 second
        var token = _factory.GenerateJwtToken(
            _factory.TestUserId,
            _factory.TestUsername,
            _factory.TestUserPermissions,
            expiration: TimeSpan.FromSeconds(1));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Immediate request should succeed
        var immediateResponse = await _client.GetAsync("/api/health");

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Second request should fail
        var delayedResponse = await _client.GetAsync("/api/health");

        // Assert
        immediateResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        delayedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
