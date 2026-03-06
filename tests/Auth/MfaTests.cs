using Muonroi.Auth.Mfa.WebAuthenticate;

namespace Muonroi.Auth.Tests;

public class MfaTests
{
    [Fact]
    public async Task RegisterSyncableAuthenticatorYieldsAal3()
    {
        RegistrationResult result = await WebAuthenticateService.RegisterAsync("user", [], [], true);
        Assert.True(result.Aal >= 2);
        Assert.True(result.Syncable);
    }

    [Fact]
    public async Task RegisterNonSyncableAuthenticatorYieldsAal2()
    {
        RegistrationResult result = await WebAuthenticateService.RegisterAsync("user", [], []);
        Assert.Equal(2, result.Aal);
    }
}