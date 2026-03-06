using Muonroi.Governance.License;

namespace Muonroi.BuildingBlock.Test;

internal sealed class TestLicenseGuard : ILicenseGuard
{
    private static readonly LicenseState State = LicenseState.CreateFree();

    public LicenseState Current => State;
    public LicenseTier Tier => State.Tier;
    public bool IsFreeMode => true;

    public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
        string? correlationId = null)
    {
        // no-op for tests
    }

    public bool HasFeature(string featureName) => true;

    public void EnsureFeature(string featureName)
    {
        // no-op for tests
    }

    public void RecordAction(LicenseActionContext context)
    {
        // no-op for tests
    }

    public string GetChainToken() => "TEST_CHAIN";

    public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
    {
        return decryptor("test-key", encryptedData);
    }
}
