namespace Muonroi.Core.Abstractions.Tests;

public class AuthOptionsTests
{
    [Fact]
    public void AuthOptions_DefaultClaimMap_UsesClaimConstants()
    {
        AuthOptions options = new();

        options.ClaimMap.Should().NotBeNull();
        options.ClaimMap.TokenValidityKey.Should().Be(ClaimConstants.TokenValidityKey);
        options.ClaimMap.UserIdentifier.Should().Be(ClaimConstants.UserIdentifier);
        options.ClaimMap.Username.Should().Be(ClaimConstants.Username);
        options.ClaimMap.FullName.Should().Be(ClaimConstants.FullName);
        options.ClaimMap.Language.Should().Be(ClaimConstants.Language);
        options.ClaimMap.AccessToken.Should().Be(ClaimConstants.AccessToken);
        options.ClaimMap.Permission.Should().Be(ClaimConstants.Permission);
        options.ClaimMap.ApiKey.Should().Be(ClaimConstants.ApiKey);
        options.ClaimMap.TenantId.Should().Be(ClaimConstants.TenantId);
        options.ClaimMap.RelatedIdentifiers.Should().Be(ClaimConstants.RelatedIdentifiers);
    }

    [Fact]
    public void AuthClaimMap_CanBeCustomized()
    {
        AuthClaimMap map = new()
        {
            TokenValidityKey = "custom_token",
            UserIdentifier = "sub",
            Username = "preferred_username"
        };

        map.TokenValidityKey.Should().Be("custom_token");
        map.UserIdentifier.Should().Be("sub");
        map.Username.Should().Be("preferred_username");
    }
}

public class ClaimConstantsTests
{
    [Theory]
    [InlineData(nameof(ClaimConstants.TokenValidityKey), "token_validity_key")]
    [InlineData(nameof(ClaimConstants.UserIdentifier), "user_identifier")]
    [InlineData(nameof(ClaimConstants.Username), "user_name")]
    [InlineData(nameof(ClaimConstants.FullName), "full_name")]
    [InlineData(nameof(ClaimConstants.Language), "language")]
    [InlineData(nameof(ClaimConstants.AccessToken), "access_token")]
    [InlineData(nameof(ClaimConstants.Permission), "permission")]
    [InlineData(nameof(ClaimConstants.ApiKey), "ApiKey")]
    [InlineData(nameof(ClaimConstants.TenantId), "tenant_id")]
    public void ClaimConstants_HaveExpectedValues(string fieldName, string expectedValue)
    {
        string? value = typeof(ClaimConstants)
            .GetField(fieldName, BindingFlags.Public | BindingFlags.Static)?
            .GetValue(null) as string;

        value.Should().Be(expectedValue);
    }
}

public class CustomHeaderTests
{
    [Fact]
    public void CustomHeader_Values_AreCorrect()
    {
        CustomHeader.CorrelationId.Should().Be("x-correlation-id");
        CustomHeader.ApiKey.Should().Be("x-api-key");
        CustomHeader.TenantId.Should().Be("x-tenant-id");
        CustomHeader.SentAt.Should().Be("x-sent-at");
        CustomHeader.SourceType.Should().Be("x-source-type");
    }
}

public class ApiErrorTests
{
    [Fact]
    public void ApiError_Record_PropertiesAreCorrect()
    {
        ApiError error = new("ERR001", "Something went wrong");

        error.Code.Should().Be("ERR001");
        error.Message.Should().Be("Something went wrong");
        error.Details.Should().BeNull();
    }

    [Fact]
    public void ApiError_WithDetails_ContainsDetails()
    {
        Dictionary<string, object> details = new() { ["field"] = "name" };
        ApiError error = new("ERR002", "Validation failed", details);

        error.Details.Should().NotBeNull();
        error.Details!["field"].Should().Be("name");
    }

    [Fact]
    public void ApiError_EqualityWorks_ForRecords()
    {
        ApiError a = new("E1", "msg");
        ApiError b = new("E1", "msg");

        a.Should().Be(b);
    }
}
