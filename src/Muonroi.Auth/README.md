# Muonroi.Auth

> JWT authentication infrastructure for .NET: RSA key management, token revocation, DPoP binding, PKCE/OIDC login, and FIDO2/WebAuthn MFA — all wired through standard ASP.NET Core DI.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Auth.svg)](https://www.nuget.org/packages/Muonroi.Auth/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Auth` is the security layer of the Muonroi Building Block ecosystem. It handles the full JWT lifecycle — RSA key generation and rotation, token signing, audience/issuer validation, JTI revocation, and JWKS endpoint exposure — with pluggable in-memory or Redis-backed stores. It also ships first-class support for DPoP token binding (RFC 9449), Authorization Code + PKCE flows, OpenID Connect login, and FIDO2/WebAuthn registration and assertion.

## Installation

```bash
dotnet add package Muonroi.Auth --prerelease
```

## Quick Start

Choose an RSA key store (in-memory for development, Redis for production) and register it alongside ASP.NET Core's JWT bearer pipeline.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// In-memory RSA key store + in-memory token revocation
builder.Services.AddInMemoryRsaKeyStore();

// Redis-backed RSA key store + Redis token revocation
// builder.Services.AddRedisRsaKeyStore(builder.Configuration);

// Optional: FIDO2/WebAuthn MFA
builder.Services.AddWebAuthn(builder.Configuration);

// Optional: OIDC login with cookie session
builder.Services.AddOidcLogin(builder.Configuration);

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.Run();
```

`appsettings.json` section consumed by `AddInMemoryRsaKeyStore` / `AddRedisRsaKeyStore`:

```json
{
  "TokenConfigs": {
    "Issuer": "https://auth.example.com",
    "Audience": "https://api.example.com",
    "ExpiryMinutes": 60,
    "RefreshTokenTtl": 30,
    "UseRsa": true
  },
  "WebAuthn": {
    "ServerDomain": "example.com",
    "ServerName": "MyApp",
    "Origins": ["https://example.com"]
  },
  "OidcConfig": {
    "Authority": "https://idp.example.com",
    "ClientId": "my-client",
    "ClientSecret": "secret",
    "CallbackPath": "/signin-oidc",
    "Scopes": ["openid", "profile", "email"]
  }
}
```

Generate and validate a token directly:

```csharp
// Resolve from DI
var jwtService = app.Services.GetRequiredService<JwtService>();

// Issue a 60-minute token
string token = jwtService.GenerateToken("user-123", TimeSpan.FromMinutes(60));

// Validate and extract claims
ClaimsPrincipal principal = jwtService.ValidateToken(token);

// Revoke on sign-out
jwtService.RevokeToken(token);

// Force key rotation
jwtService.RotateKeys();
```

## Features

- **RSA key stores** — in-memory (`InMemoryRsaKeyStore`) or Redis-backed (`RedisRsaKeyStore`) with automatic key-ID (`kid`) tracking and rotation.
- **Token revocation** — JTI-based revocation via `ITokenRevocationStore`; in-memory (`TokenRevocationStore`) or Redis (`RedisTokenRevocationStore`).
- **JWKS endpoint** — `JsonWebKeySetController` serves public keys at `/.well-known/jsonWebKeySet.json` for external validators.
- **DPoP binding** — `DPoPBindingService` creates DPoP-bound access tokens (JWK thumbprint `cnf/jkt` claim) and validates DPoP proof JWTs including replay detection.
- **PKCE / OIDC** — `PkceClient` handles Authorization Code + PKCE exchange; `OidcHandler.AddOidcLogin` wires ASP.NET Core OIDC + cookie authentication from `OidcConfig` configuration.
- **FIDO2 / WebAuthn MFA** — `WebAuthenticateService` manages credential registration (`BeginRegistrationAsync` / `CompleteRegistrationAsync`) and assertion (`BeginAuthenticationAsync` / `CompleteAuthenticationAsync`).
- **Password helpers** — `MPasswordHelper.HashPassword` (BCrypt, cost 8) and `MPasswordHelper.VerifyPassword`; `BCryptPasswordHasher` implements `IPasswordHasher` for DI-based usage.
- **Bearer token signers** — `RsaTokenSigner` (RSA-SHA256) and `HmacTokenSigner` (HMAC) implement `ITokenSigner` for lower-level signing scenarios.

## Configuration

### Token config (`TokenConfigs` section bound to `MTokenInfo`)

| Key | Type | Description |
|-----|------|-------------|
| `Issuer` | `string` | JWT `iss` claim value |
| `Audience` | `string` | JWT `aud` claim value |
| `ExpiryMinutes` | `int` | Access token lifetime |
| `RefreshTokenTtl` | `int` | Refresh token TTL in days |
| `RefreshTokenEim` | `int` | Refresh token expiry in minutes |
| `UseRsa` | `bool` | `true` (default) — RS256; `false` — HS256 |
| `PublicKey` / `PublicKeyPath` | `string` | Inline PEM or file path for RSA public key |
| `PrivateKey` / `PrivateKeyPath` | `string` | Inline PEM or file path for RSA private key |
| `MultiTenantEnabled` | `bool` | Enables per-tenant signing keys |
| `SigningKeysByTenant` | `Dictionary<string,string>` | Tenant-specific keys keyed by tenant ID |
| `EnableCookieAuth` | `bool` | Emit auth cookie alongside bearer token |
| `CookieName` | `string` | Cookie name (default: `AuthToken`) |
| `CookieSameSite` | `string` | `Lax` (default), `Strict`, or `None` |

### OIDC config (`OidcConfig` section bound to `MOidcConfig`)

| Key | Type | Description |
|-----|------|-------------|
| `Authority` | `string` | OpenID Connect provider base URL |
| `ClientId` | `string` | Registered client ID |
| `ClientSecret` | `string` | Registered client secret |
| `CallbackPath` | `string` | Redirect URI path (default: `/signin-oidc`) |
| `Scopes` | `string[]` | Requested scopes |

### WebAuthn config (`WebAuthn` section)

| Key | Type | Description |
|-----|------|-------------|
| `ServerDomain` | `string` | Relying Party domain |
| `ServerName` | `string` | Relying Party display name (default: `Muonroi`) |
| `Origins` | `string[]` | Allowed origin URLs |

## API Reference

| Type | Purpose |
|------|---------|
| `AuthServiceCollectionExtensions` | DI registration: `AddInMemoryRsaKeyStore()`, `AddRedisRsaKeyStore(config)`, `AddDefaultTokenRevocationStore()` |
| `JwtService` | Issues (`GenerateToken`), validates (`ValidateToken`), revokes (`RevokeToken`), and rotates (`RotateKeys`) JWTs; exposes `GetJsonWebKeySet()` |
| `IRsaKeyStore` | Contract for RSA key management: `GetCurrentSigningCredentials()`, `GetKey(kid)`, `RotateKeys()`, `GetJsonWebKeySet()` |
| `InMemoryRsaKeyStore` | In-process `IRsaKeyStore` — suitable for single-node deployments and testing |
| `RedisRsaKeyStore` | Distributed `IRsaKeyStore` backed by `IDistributedCache` (Redis) for multi-node deployments |
| `ITokenRevocationStore` | Contract for JTI revocation: `Revoke(jti, expires)`, `IsRevoked(jti)` |
| `TokenRevocationStore` | In-memory `ITokenRevocationStore` |
| `RedisTokenRevocationStore` | Redis-backed `ITokenRevocationStore` |
| `JsonWebKeySetController` | MVC controller serving public JWKS at `GET /.well-known/jsonWebKeySet.json` |
| `DPoPBindingService` | Static helpers: `CreateAccessToken(jwk, credentials)`, `ValidateProof(proofJwt, method, uri, jkt)`, `ComputeJkt(jwk)` |
| `OidcHandler` | `AddOidcLogin(services, config, scheme)` — wires cookie + OIDC authentication |
| `PkceClient` | HTTP client for Authorization Code + PKCE token exchange and refresh |
| `OidcOptions` | Options for `PkceClient` (`Authority`, `ClientId`, `RedirectUri`, `Scopes`) |
| `WebAuthenticateService` | FIDO2 registration and assertion flows |
| `WebAuthnServiceCollectionExtensions` | `AddWebAuthn(services, config)` — registers FIDO2 + `WebAuthenticateService` |
| `MPasswordHelper` | Static BCrypt helpers: `HashPassword(password, out salt)`, `VerifyPassword(entered, hash)` |
| `BCryptPasswordHasher` | `IPasswordHasher` implementation registered by `AddInMemoryRsaKeyStore` / `AddRedisRsaKeyStore` |
| `RsaTokenSigner` | `ITokenSigner` using RSA-SHA256 `SigningCredentials` |
| `HmacTokenSigner` | `ITokenSigner` using HMAC signing key |
| `MTokenInfo` | Configuration model bound from the `TokenConfigs` section |
| `MOidcConfig` | Configuration model bound from the `OidcConfig` section |

## Samples

No dedicated sample application exists for this package yet. The Quick Start snippet above is derived directly from the public registration API. See the root [`samples/`](../../samples/) directory for broader ecosystem examples.

## Compatibility

- Target framework: `net8.0`
- Depends on: `Muonroi.Core`, `Muonroi.Core.Abstractions`, `Muonroi.Caching.Memory`, `Muonroi.Caching.Abstractions`
- External dependencies: `BCrypt.Net-Next`, `Fido2`, `Fido2.AspNet`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.AspNetCore.Authentication.OpenIdConnect`, `System.IdentityModel.Tokens.Jwt`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `MTokenInfo`, `MOidcConfig`, `IPasswordHasher`, and `IMDateTimeService` consumed by this package
- [`Muonroi.Core`](../Muonroi.Core/) — core DI extensions including `AddJwtConfigs` used alongside auth registration
- [`Muonroi.Caching.Abstractions`](../Muonroi.Caching.Abstractions/) — `IDistributedCache` abstraction used by Redis key/revocation stores
- [`Muonroi.AspNetCore`](../Muonroi.AspNetCore/) — higher-level auth middleware (`MCookieAuthMiddleware`) and controller base (`MAuthControllerBase`) that build on top of this package

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
