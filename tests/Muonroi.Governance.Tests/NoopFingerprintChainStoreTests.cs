namespace Muonroi.Governance.Tests;

public class NoopFingerprintChainStoreTests
{
    [Fact]
    public void Append_Does_Not_Throw()
    {
        NoopFingerprintChainStore store = new();

        Action act = () => store.Append(new FingerprintChainEntry
        {
            Sequence = 1,
            ActionType = "test",
            Signature = "sig"
        });

        act.Should().NotThrow();
    }

    [Fact]
    public void GetLastSignature_Returns_Null()
    {
        NoopFingerprintChainStore store = new();

        store.GetLastSignature().Should().BeNull();
        store.GetLastSignature("tenant-1").Should().BeNull();
    }

    [Fact]
    public void GetLastSequence_Returns_Zero()
    {
        NoopFingerprintChainStore store = new();

        store.GetLastSequence().Should().Be(0);
        store.GetLastSequence("tenant-1").Should().Be(0);
    }

    [Fact]
    public void GetRecentEntries_Returns_Empty()
    {
        NoopFingerprintChainStore store = new();

        store.GetRecentEntries(10).Should().BeEmpty();
        store.GetRecentEntries(10, 5, "tenant-1").Should().BeEmpty();
    }

    [Fact]
    public void GetTenantPartitions_Returns_Empty()
    {
        NoopFingerprintChainStore store = new();

        store.GetTenantPartitions().Should().BeEmpty();
    }
}

public class NoopFingerprintSignerTests
{
    [Fact]
    public void ComputeSignature_Returns_PreviousSignature()
    {
        NoopFingerprintSigner signer = new();
        LicenseActionContext context = new() { ActionType = "test" };

        string result = signer.ComputeSignature("prev-sig", context, 1);

        result.Should().Be("prev-sig");
    }
}
