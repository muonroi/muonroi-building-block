using Muonroi.Logging.Abstractions;
using Muonroi.Governance.Enterprise.Policy;
using Muonroi.Governance.Policy;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Core.Abstractions.Interfaces;
using NSubstitute;

namespace Muonroi.Governance.Enterprise.Tests.Policy;

public class PolicyEnforcerTests
{
    private readonly IMDateTimeService _dateTimeService;
    private readonly IMLog<PolicyEnforcer> _logger;

    public PolicyEnforcerTests()
    {
        _dateTimeService = Substitute.For<IMDateTimeService>();
        _logger = Substitute.For<IMLog<PolicyEnforcer>>();
    }

    [Fact]
    public void CheckApiRateLimit_WithNoPolicy_ShouldReturnTrue()
    {
        // Arrange
        var enforcer = new PolicyEnforcer(null, _dateTimeService, _logger);

        // Act
        var result = enforcer.CheckApiRateLimit();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CheckApiRateLimit_UnderLimit_ShouldReturnTrue()
    {
        // Arrange
        var policy = new LicensePolicy
        {
            Enforcement = new PolicyEnforcementRules { MaxApiRequestsPerMinute = 10 }
        };
        _dateTimeService.UtcNow().Returns(new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc));
        var enforcer = new PolicyEnforcer(policy, _dateTimeService, _logger);

        // Act
        var result = enforcer.CheckApiRateLimit();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CheckApiRateLimit_OverLimit_ShouldReturnFalse()
    {
        // Arrange
        var policy = new LicensePolicy
        {
            Enforcement = new PolicyEnforcementRules { MaxApiRequestsPerMinute = 1 }
        };
        _dateTimeService.UtcNow().Returns(new DateTime(2026, 3, 23, 10, 0, 0, DateTimeKind.Utc));
        var enforcer = new PolicyEnforcer(policy, _dateTimeService, _logger);

        // Act
        enforcer.CheckApiRateLimit(); // First call - OK
        var result = enforcer.CheckApiRateLimit(); // Second call - Fail

        // Assert
        Assert.False(result);
        _logger.Received().Warn(Arg.Is<string>(s => s.Contains("API rate limit exceeded")), Arg.Any<object[]>());
    }

    [Fact]
    public void CheckFeatureQuota_OverQuota_ShouldReturnFalse()
    {
        // Arrange
        var policy = new LicensePolicy
        {
            FeatureQuotas = new Dictionary<string, FeatureQuota>
            {
                ["TestFeature"] = new FeatureQuota { MaxUsagePerDay = 100 }
            }
        };
        var enforcer = new PolicyEnforcer(policy, _dateTimeService, _logger);

        // Act
        var result = enforcer.CheckFeatureQuota("TestFeature", 100);

        // Assert
        Assert.False(result);
        _logger.Received().Warn(Arg.Is<string>(s => s.Contains("Feature quota exceeded")), Arg.Any<object[]>());
    }
}

