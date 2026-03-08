namespace Muonroi.Governance.Abstractions.Tests;

using Muonroi.Governance.License;
using Xunit;

public class LicenseAbstractionsTests
{
    [Fact]
    public void LicenseTier_Enum_ShouldHaveExpectedValues()
    {
        // Assert
        Assert.Equal(0, (int)LicenseTier.Free);
        Assert.Equal(1, (int)LicenseTier.Licensed);
        Assert.Equal(2, (int)LicenseTier.Enterprise);
    }

    [Fact]
    public void LicenseState_Default_ShouldBeFree()
    {
        // Arrange
        var state = new LicenseState();

        // Assert
        Assert.Equal(LicenseTier.Free, state.Tier);
        Assert.False(state.IsValid);
    }

    [Fact]
    public void LicenseState_CreateFree_ShouldBeValidAndFree()
    {
        // Act
        var state = LicenseState.CreateFree();

        // Assert
        Assert.Equal(LicenseTier.Free, state.Tier);
        Assert.True(state.IsValid);
        Assert.Equal("FREE", state.Payload?.LicenseId);
    }
}
