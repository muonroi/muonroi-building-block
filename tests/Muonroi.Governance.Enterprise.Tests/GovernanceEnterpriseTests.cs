using System.Reflection;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;

namespace Muonroi.Governance.Enterprise.Tests;


public class GovernanceEnterpriseTests
{
    [Fact]
    public void HmacFingerprintSigner_ComputeSignature_ShouldBeConsistent()
    {
        // Arrange
        LicensePayload payload = new() { LicenseId = "TEST_LICENSE", Signature = "SIG", ServerNonce = "NONCE" };
        LicenseConfigs configs = new() { ProjectSeed = "SEED", FingerprintSalt = "SALT" };
        HmacFingerprintSigner signer = new(payload, configs);

        LicenseActionContext context = new()
        {
            TenantId = "TenantA",
            ActionType = "ACTION",
            ActionName = "ActionA",
            PayloadHash = "HASH",
            Timestamp = DateTimeOffset.Parse("2026-03-08T00:00:00Z")
        };

        // Act
        string sig1 = signer.ComputeSignature("PREV", context, 1);
        string sig2 = signer.ComputeSignature("PREV", context, 1);

        // Assert
        Assert.Equal(sig1, sig2);
        Assert.NotEmpty(sig1);
    }

    [Fact]
    [Trait("Category", "Phase31")]
    public void HmacKeyDerivation_ProducesConsistent32ByteKey()
    {
        // Arrange
        LicenseConfigs configs = new()
        {
            ProjectSeed = "test-seed",
            FingerprintSalt = "test-salt"
        };
        LicensePayload payload = new()
        {
            Signature = "test-sig",
            ServerNonce = "test-nonce"
        };

        HmacFingerprintSigner signer = new(payload, configs);

        // Use reflection to call private BuildKey
        MethodInfo buildKey = typeof(HmacFingerprintSigner)
            .GetMethod("BuildKey", BindingFlags.NonPublic | BindingFlags.Instance)!;
        byte[] key = (byte[])buildKey.Invoke(signer, new object?[] { payload })!;

        // Assert: SHA256 output is always exactly 32 bytes
        Assert.Equal(32, key.Length);

        // Verify consistency: same inputs produce same key
        byte[] key2 = (byte[])buildKey.Invoke(signer, new object?[] { payload })!;
        Assert.Equal(key, key2);
    }

    [Fact]
    [Trait("Category", "Phase31")]
    public void HmacKeyDerivation_SignatureConsistency()
    {
        // Arrange
        LicenseConfigs configs = new() { ProjectSeed = "seed", FingerprintSalt = "salt" };
        LicensePayload payload = new() { Signature = "sig", ServerNonce = "nonce" };
        HmacFingerprintSigner signer = new(payload, configs);
        LicenseActionContext context = new()
        {
            TenantId = "tenant-1",
            ActionType = "test",
            ActionName = "test-action",
            PayloadHash = "hash123",
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        // Act
        string sig1 = signer.ComputeSignature("GENESIS", context, 1);
        string sig2 = signer.ComputeSignature("GENESIS", context, 1);

        // Assert
        Assert.Equal(sig1, sig2);
        // HMAC-SHA256 produces 32 bytes = 64 hex chars
        Assert.Equal(64, sig1.Length);
    }
}
