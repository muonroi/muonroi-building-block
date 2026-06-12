using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class BaseGrpcServiceTests
{
    private static readonly LicenseState GrpcLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.Grpc]
    };

    private class TestGrpc(MAuthenticateInfoContext ctx, LicenseState? state = null, ILicenseGuard? guard = null)
        : BaseGrpcService(ctx, state, null, guard)
    {
        public Task<T> Call<T>(Func<Metadata, Task<T>> func)
        {
            return CallGrpcServiceAsync(func);
        }
    }

    private sealed class DenyGrpcGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();
        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.Grpc, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
                throw new InvalidOperationException("grpc feature not licensed");
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => "test";

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("k", encryptedData);
        }
    }

    [Fact]
    public async Task CallGrpcServiceAsync_Returns_Result()
    {
        TestGrpc svc = new(new MAuthenticateInfoContext(false), GrpcLicensed);
        string res = await svc.Call(_ => Task.FromResult("ok"));
        Assert.Equal("ok", res);
    }

    [Fact]
    public async Task CallGrpcServiceAsync_Throws_Exception()
    {
        TestGrpc svc = new(new MAuthenticateInfoContext(false), GrpcLicensed);
        await Assert.ThrowsAsync<MInternalException>(() =>
            svc.Call<string>(_ => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task CallGrpcServiceAsync_NullInput_Throws()
    {
        TestGrpc svc = new(new MAuthenticateInfoContext(false));
        await Assert.ThrowsAsync<NullReferenceException>(() => svc.Call<string>(null!));
    }

    [Fact]
    public async Task CallGrpcServiceAsync_FreeMode_Throws_License_Error()
    {
        TestGrpc svc = new(new MAuthenticateInfoContext(false));
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() => svc.Call(_ => Task.FromResult("ok")));
        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task CallGrpcServiceAsync_LicenseGuardDenies_ShouldThrow()
    {
        TestGrpc svc = new(new MAuthenticateInfoContext(false), GrpcLicensed, new DenyGrpcGuard());
        InvalidOperationException ex = await Assert.ThrowsAsync<MInternalException>(() => svc.Call(_ => Task.FromResult("ok")));
        Assert.Contains("grpc feature not licensed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
