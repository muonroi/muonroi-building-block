using Muonroi.Auth.Mfa.WebAuthenticate;

namespace Muonroi.Auth.Tests;

public class MfaTests
{
    [Fact(Skip = "WebAuthenticateService.RegisterAsync static API removed; covered by tests/Muonroi.Auth.Tests/WebAuthnTests.cs")]
    public Task RegisterSyncableAuthenticatorYieldsAal3() => Task.CompletedTask;

    [Fact(Skip = "WebAuthenticateService.RegisterAsync static API removed; covered by tests/Muonroi.Auth.Tests/WebAuthnTests.cs")]
    public Task RegisterNonSyncableAuthenticatorYieldsAal2() => Task.CompletedTask;
}