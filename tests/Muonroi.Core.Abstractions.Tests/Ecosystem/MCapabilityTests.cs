namespace Muonroi.Core.Abstractions.Tests.Ecosystem;

public class MCapabilityTests
{
    [Fact]
    public void None_HasValue_Zero()
    {
        ((int)MCapability.None).Should().Be(0);
    }

    [Fact]
    public void Logging_HasValue_One()
    {
        ((int)MCapability.Logging).Should().Be(1);
    }

    [Fact]
    public void RuleEngine_HasValue_Two()
    {
        ((int)MCapability.RuleEngine).Should().Be(2);
    }

    [Fact]
    public void MultiTenant_HasValue_Four()
    {
        ((int)MCapability.MultiTenant).Should().Be(4);
    }

    [Fact]
    public void Auth_HasValue_Eight()
    {
        ((int)MCapability.Auth).Should().Be(8);
    }

    [Fact]
    public void Logging_Or_Auth_EqualsNine()
    {
        ((int)(MCapability.Logging | MCapability.Auth)).Should().Be(9);
    }

    [Fact]
    public void LoggingOrAuth_And_Logging_EqualsLogging()
    {
        var combined = MCapability.Logging | MCapability.Auth;
        (combined & MCapability.Logging).Should().Be(MCapability.Logging);
    }

    [Fact]
    public void AllFlagsArePowersOfTwo()
    {
        var values = new[] { MCapability.Logging, MCapability.RuleEngine, MCapability.MultiTenant, MCapability.Auth };
        foreach (var v in values)
        {
            var i = (int)v;
            (i & (i - 1)).Should().Be(0, because: $"{v} should be a power of two");
        }
    }
}
