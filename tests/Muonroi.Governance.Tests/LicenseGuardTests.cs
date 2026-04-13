using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Governance.Tests;

public class LicenseGuardTests
{
    private static LicenseGuard CreateGuard(
        LicenseConfigs? configs = null,
        LicenseState? state = null,
        IFingerprintChainStore? chainStore = null,
        IFingerprintSigner? signer = null,
        LicenseRuntimeStatus? runtimeStatus = null)
    {
        configs ??= new LicenseConfigs { FailMode = LicenseFailMode.Hard };
        state ??= LicenseState.CreateFree();
        chainStore ??= new NoopFingerprintChainStore();
        signer ??= new NoopFingerprintSigner();
        runtimeStatus ??= new LicenseRuntimeStatus();

        return new LicenseGuard(configs, state, chainStore, signer, runtimeStatus);
    }

    [Fact]
    public void Tier_Returns_Free_For_Free_License()
    {
        LicenseGuard guard = CreateGuard();

        guard.Tier.Should().Be(LicenseTier.Free);
    }

    [Fact]
    public void IsFreeMode_Returns_True_For_Free_License()
    {
        LicenseGuard guard = CreateGuard();

        guard.IsFreeMode.Should().BeTrue();
    }

    [Fact]
    public void HasFeature_Returns_True_For_Free_Tier_Features()
    {
        LicenseGuard guard = CreateGuard();

        guard.HasFeature("db.query").Should().BeTrue();
        guard.HasFeature("http.request").Should().BeTrue();
    }

    [Fact]
    public void HasFeature_Returns_False_For_Premium_Features_On_Free_Tier()
    {
        LicenseGuard guard = CreateGuard();

        guard.HasFeature("rule-engine").Should().BeFalse();
        guard.HasFeature("multi-tenant").Should().BeFalse();
    }

    [Fact]
    public void EnsureFeature_Throws_For_Unavailable_Feature()
    {
        LicenseGuard guard = CreateGuard();

        Action act = () => guard.EnsureFeature("rule-engine");

        act.Should().Throw<MInternalException>()
            .WithMessage("*rule-engine*not available*");
    }

    [Fact]
    public void EnsureValid_Throws_When_License_Invalid_And_FailMode_Hard()
    {
        LicenseState invalidState = new() { IsValid = false };
        LicenseGuard guard = CreateGuard(state: invalidState);

        Action act = () => guard.EnsureValid("db.query");

        act.Should().Throw<MInternalException>()
            .WithMessage("*License validation failed*");
    }

    [Fact]
    public void EnsureValid_Does_Not_Throw_When_Invalid_And_FailMode_Soft()
    {
        LicenseConfigs configs = new() { FailMode = LicenseFailMode.Soft };
        LicenseState invalidState = new() { IsValid = false };
        LicenseGuard guard = CreateGuard(configs: configs, state: invalidState);

        Action act = () => guard.EnsureValid("db.query");

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureValid_Throws_When_Feature_Not_Allowed()
    {
        LicenseState freeState = LicenseState.CreateFree();
        LicenseGuard guard = CreateGuard(state: freeState);

        Action act = () => guard.EnsureValid("rule-engine");

        act.Should().Throw<MInternalException>()
            .WithMessage("*rule-engine*not included*");
    }

    [Fact]
    public void GetChainToken_Returns_Initial_Boot_When_No_Actions_Recorded()
    {
        LicenseGuard guard = CreateGuard();

        string token = guard.GetChainToken();

        token.Should().Be("INITIAL_BOOT");
    }

    [Fact]
    public void Current_Returns_License_State()
    {
        LicenseState state = LicenseState.CreateFree();
        LicenseGuard guard = CreateGuard(state: state);

        guard.Current.Should().BeSameAs(state);
    }

    [Fact]
    public void DecryptSecurely_Uses_ProjectSeed_And_RollingToken()
    {
        LicenseConfigs configs = new() { ProjectSeed = "test-seed" };
        LicenseState state = LicenseState.CreateFree();
        LicenseGuard guard = CreateGuard(configs: configs, state: state);

        string result = guard.DecryptSecurely("test-purpose", "encrypted-data", (key, data) => $"decrypted-{key}-{data}");

        result.Should().StartWith("decrypted-");
        result.Should().Contain("encrypted-data");
    }

    [Fact]
    public void RecordAction_Does_Nothing_When_Chain_Disabled()
    {
        IFingerprintChainStore chainStore = Substitute.For<IFingerprintChainStore>();
        LicenseConfigs configs = new() { EnableChain = false };
        LicenseGuard guard = CreateGuard(configs: configs, chainStore: chainStore);

        guard.RecordAction(new LicenseActionContext
        {
            ActionType = "test",
            Timestamp = DateTimeOffset.UtcNow
        });

        chainStore.DidNotReceive().Append(Arg.Any<FingerprintChainEntry>());
    }

#pragma warning disable CS0618 // Obsolete
    [Fact]
    public void GetFunctionalKey_Returns_Poisoned_Key()
    {
        LicenseGuard guard = CreateGuard();

        string key = guard.GetFunctionalKey("test-purpose");

        key.Should().StartWith("POISONED_");
    }
#pragma warning restore CS0618
}
