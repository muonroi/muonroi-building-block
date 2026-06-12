using Microsoft.AspNetCore.Authorization;

namespace Muonroi.AspNetCore.Tests.Extensions;

public class UiEnginePolicyExtensionsTests
{
    [Fact]
    public void AddUiEngineChangePolicies_RegistersDefaultPolicies()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddUiEngineChangePolicies();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IConfigureOptions<AuthorizationOptions>));
    }

    [Fact]
    public void AddUiEngineChangePolicies_WithConfigure_InvokesCallback()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        bool invoked = false;

        services.AddUiEngineChangePolicies(options =>
        {
            invoked = true;
            options.UseClaimRequirement = true;
        });

        invoked.Should().BeTrue();
    }

    [Fact]
    public void AddUiEngineChangePolicies_NullConfigure_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var act = () => services.AddUiEngineChangePolicies(null);
        act.Should().NotThrow();
    }
}

public class UiEnginePolicyNamesTests
{
    [Fact]
    public void Constants_HaveExpectedValues()
    {
        UiEnginePolicyNames.Propose.Should().Be("ui-engine:changes:propose");
        UiEnginePolicyNames.Apply.Should().Be("ui-engine:changes:apply");
    }
}

public class UiEnginePolicyOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new UiEnginePolicyOptions();

        options.UseClaimRequirement.Should().BeFalse();
        options.ClaimType.Should().Be("permission");
        options.ProposeClaimValue.Should().Be("ui-engine:changes:propose");
        options.ApplyClaimValue.Should().Be("ui-engine:changes:apply");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var options = new UiEnginePolicyOptions
        {
            UseClaimRequirement = true,
            ClaimType = "custom-type",
            ProposeClaimValue = "custom-propose",
            ApplyClaimValue = "custom-apply"
        };

        options.UseClaimRequirement.Should().BeTrue();
        options.ClaimType.Should().Be("custom-type");
        options.ProposeClaimValue.Should().Be("custom-propose");
        options.ApplyClaimValue.Should().Be("custom-apply");
    }
}
