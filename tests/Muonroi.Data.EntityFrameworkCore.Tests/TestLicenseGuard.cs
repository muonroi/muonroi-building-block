namespace Muonroi.Data.EntityFrameworkCore.Tests;

internal sealed class TestLicenseGuard : ILicenseGuard
{
    private static readonly LicenseState State = LicenseState.CreateFree();

    public LicenseState Current => State;

    public LicenseTier Tier => State.Tier;

    public bool IsFreeMode => true;

    public void EnsureValid(
        string actionType,
        string? actionName = null,
        string? payloadHash = null,
        string? correlationId = null)
    {
    }

    public bool HasFeature(string featureName)
    {
        return true;
    }

    public void EnsureFeature(string featureName)
    {
    }

    public void RecordAction(LicenseActionContext context)
    {
    }

    public string GetChainToken()
    {
        return "TEST_CHAIN";
    }

    public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
    {
        return decryptor("test-key", encryptedData);
    }
}
