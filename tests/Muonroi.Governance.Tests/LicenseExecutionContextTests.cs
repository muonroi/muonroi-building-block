namespace Muonroi.Governance.Tests;

public class LicenseExecutionContextTests
{
    [Fact]
    public void IsInLicenseCheck_Default_IsFalse()
    {
        Assert.False(LicenseExecutionContext.IsInLicenseCheck);
    }

    [Fact]
    public void BeginScope_SetsFlagToTrue()
    {
        using (LicenseExecutionContext.BeginScope())
        {
            Assert.True(LicenseExecutionContext.IsInLicenseCheck);
        }
    }

    [Fact]
    public void DisposeScope_RestoresOriginalValue()
    {
        LicenseExecutionContext.IsInLicenseCheck = false;

        using (LicenseExecutionContext.BeginScope())
        {
            Assert.True(LicenseExecutionContext.IsInLicenseCheck);
        }

        Assert.False(LicenseExecutionContext.IsInLicenseCheck);
    }

    [Fact]
    public void NestedScopes_MaintainCorrectValues()
    {
        LicenseExecutionContext.IsInLicenseCheck = false;

        using (LicenseExecutionContext.BeginScope())
        {
            Assert.True(LicenseExecutionContext.IsInLicenseCheck);

            using (LicenseExecutionContext.BeginScope())
            {
                Assert.True(LicenseExecutionContext.IsInLicenseCheck);
            }

            Assert.True(LicenseExecutionContext.IsInLicenseCheck);
        }

        Assert.False(LicenseExecutionContext.IsInLicenseCheck);
    }

    [Fact]
    public async Task AsyncFlow_MaintainsIndependentContext()
    {
        LicenseExecutionContext.IsInLicenseCheck = false;

        Task<bool> task1 = Task.Run(() =>
        {
            using (LicenseExecutionContext.BeginScope())
            {
                Thread.Sleep(50);
                return LicenseExecutionContext.IsInLicenseCheck;
            }
        });

        Task<bool> task2 = Task.Run(() =>
        {
            Thread.Sleep(25);
            return LicenseExecutionContext.IsInLicenseCheck;
        });

        bool[] results = await Task.WhenAll(task1, task2);

        Assert.True(results[0]);
        Assert.False(results[1]);
    }
}
