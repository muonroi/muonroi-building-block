using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Caching.Distributed;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

namespace Muonroi.Auth.Mfa.WebAuthenticate;

/// <summary>
/// WebAuthn registration and authentication workflow service.
/// </summary>
public class WebAuthenticateService(
    IFido2 fido2,
    IDistributedCache challengeCache,
    IWebAuthnCredentialStore credentialStore,
    IMJsonSerializeService jsonService,
    IMDateTimeService dateTimeService)
{
    private static readonly DistributedCacheEntryOptions ChallengeTtl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    /// <summary>
    /// Starts a WebAuthn registration ceremony and returns the options.
    /// </summary>
    public async Task<CredentialCreateOptions> BeginRegistrationAsync(
        Guid userId,
        string userName,
        string displayName,
        CancellationToken ct = default)
    {
        MGuard.NotEmpty(userName);
        MGuard.NotEmpty(displayName);

        string? tenantId = TenantContext.CurrentTenantId;
        List<byte[]> existingIds = await credentialStore.GetCredentialIdsByUserAsync(tenantId, userId, ct);
        List<PublicKeyCredentialDescriptor> existingCredentials = existingIds
            .Select(id => new PublicKeyCredentialDescriptor(id))
            .ToList();

        Fido2User user = new()
        {
            Id = Encoding.UTF8.GetBytes(userId.ToString("N")),
            Name = userName,
            DisplayName = displayName
        };

        RequestNewCredentialParams request = new()
        {
            User = user,
            ExcludeCredentials = existingCredentials,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = UserVerificationRequirement.Required,
                ResidentKey = ResidentKeyRequirement.Preferred
            },
            AttestationPreference = AttestationConveyancePreference.None
        };

        CredentialCreateOptions options = fido2.RequestNewCredential(request);
        await challengeCache.SetStringAsync(
            GetRegistrationCacheKey(userId),
            jsonService.Serialize(options),
            ChallengeTtl,
            ct);

        return options;
    }

    /// <summary>
    /// Completes the WebAuthn registration ceremony.
    /// </summary>
    public async Task<RegistrationResult> CompleteRegistrationAsync(
        Guid userId,
        AuthenticatorAttestationRawResponse response,
        CancellationToken ct = default)
    {
        MGuard.NotNull(response);

        CredentialCreateOptions options = await GetRequiredRegistrationOptionsAsync(userId, ct);
        RegisteredPublicKeyCredential result = await fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = IsCredentialIdUniqueToUserAsync
            },
            ct);

        WebAuthnCredentialInfo credential = new()
        {
            TenantId = TenantContext.CurrentTenantId,
            UserId = userId,
            CredentialId = result.Id,
            PublicKey = result.PublicKey,
            SignCount = result.SignCount,
            AaGuid = result.AaGuid,
            CredType = "public-key",
            IsBackupEligible = result.IsBackupEligible,
            IsBackedUp = result.IsBackedUp
        };

        await credentialStore.SaveCredentialAsync(credential, ct);
        await challengeCache.RemoveAsync(GetRegistrationCacheKey(userId), ct);

        bool syncable = credential.IsBackupEligible || credential.IsBackedUp;
        return new RegistrationResult
        {
            UserId = userId,
            CredentialId = Base64UrlEncoder.Encode(credential.CredentialId),
            Syncable = syncable
        };
    }

    /// <summary>
    /// Starts a WebAuthn authentication ceremony and returns the options.
    /// </summary>
    public async Task<AssertionOptions> BeginAuthenticationAsync(Guid userId, CancellationToken ct = default)
    {
        string? tenantId = TenantContext.CurrentTenantId;
        List<byte[]> credentialIds = await credentialStore.GetCredentialIdsByUserAsync(tenantId, userId, ct);
        List<PublicKeyCredentialDescriptor> allowedCredentials = credentialIds
            .Select(id => new PublicKeyCredentialDescriptor(id))
            .ToList();

        AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Required
        });

        await challengeCache.SetStringAsync(
            GetAuthenticationCacheKey(userId),
            jsonService.Serialize(options),
            ChallengeTtl,
            ct);

        return options;
    }

    /// <summary>
    /// Completes the WebAuthn authentication ceremony.
    /// </summary>
    public async Task<AuthenticationResult> CompleteAuthenticationAsync(
        Guid userId,
        AuthenticatorAssertionRawResponse response,
        CancellationToken ct = default)
    {
        MGuard.NotNull(response);

        string? tenantId = TenantContext.CurrentTenantId;
        AssertionOptions options = await GetRequiredAuthenticationOptionsAsync(userId, ct);
        byte[] credentialId = ExtractCredentialId(response);

        List<WebAuthnCredentialInfo> credentials = await credentialStore.GetCredentialsByUserAsync(tenantId, userId, ct);
        WebAuthnCredentialInfo? credential = credentials.FirstOrDefault(x => x.CredentialId.SequenceEqual(credentialId))
            ?? MGuard.Fail<WebAuthnCredentialInfo>("Credential not found for user.");
        VerifyAssertionResult verificationResult = await fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = options,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = IsUserHandleOwnerOfCredentialAsync
            },
            ct);

        credential.SignCount = verificationResult.SignCount;
        credential.IsBackedUp = verificationResult.IsBackedUp;
        credential.LastUsedAt = dateTimeService.UtcNow();
        credential.UserHandle = TryEncodeUserHandle(response.Response?.UserHandle) ?? credential.UserHandle;

        await credentialStore.UpdateCredentialAsync(credential, ct);
        await challengeCache.RemoveAsync(GetAuthenticationCacheKey(userId), ct);

        bool syncable = credential.IsBackupEligible || credential.IsBackedUp;
        return new AuthenticationResult
        {
            Verification = verificationResult,
            Aal = syncable ? 3 : 2
        };
    }

    private async Task<CredentialCreateOptions> GetRequiredRegistrationOptionsAsync(Guid userId, CancellationToken ct)
    {
        string key = GetRegistrationCacheKey(userId);
        string? raw = await challengeCache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            MGuard.Fail<object>("Registration challenge not found or expired.");
        }

        CredentialCreateOptions? options = jsonService.Deserialize<CredentialCreateOptions>(raw);
        return options ?? MGuard.Fail<CredentialCreateOptions>("Registration challenge payload is invalid.");
    }

    private async Task<AssertionOptions> GetRequiredAuthenticationOptionsAsync(Guid userId, CancellationToken ct)
    {
        string key = GetAuthenticationCacheKey(userId);
        string? raw = await challengeCache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            MGuard.Fail<object>("Authentication challenge not found or expired.");
        }

        AssertionOptions? options = jsonService.Deserialize<AssertionOptions>(raw);
        return options ?? MGuard.Fail<AssertionOptions>("Authentication challenge payload is invalid.");
    }

    private async Task<bool> IsCredentialIdUniqueToUserAsync(IsCredentialIdUniqueToUserParams input, CancellationToken ct)
    {
        string? tenantId = TenantContext.CurrentTenantId;
        List<byte[]> existingIds = await credentialStore.GetAllCredentialIdsAsync(tenantId, ct);
        return existingIds.All(x => !x.SequenceEqual(input.CredentialId));
    }

    private async Task<bool> IsUserHandleOwnerOfCredentialAsync(IsUserHandleOwnerOfCredentialIdParams input, CancellationToken ct)
    {
        string? tenantId = TenantContext.CurrentTenantId;
        List<WebAuthnCredentialInfo> credentials = await credentialStore.GetCredentialsByUserAsync(tenantId, Guid.Empty, ct);

        WebAuthnCredentialInfo? credential = credentials.FirstOrDefault(x => x.CredentialId.SequenceEqual(input.CredentialId));
        if (credential is null)
        {
            return false;
        }

        if (input.UserHandle is null || input.UserHandle.Length == 0)
        {
            return true;
        }

        return string.Equals(
            credential.UserHandle,
            Base64UrlEncoder.Encode(input.UserHandle),
            StringComparison.Ordinal);
    }

    private static byte[] ExtractCredentialId(AuthenticatorAssertionRawResponse response)
    {
        if (response.RawId is { Length: > 0 })
        {
            return response.RawId;
        }

        if (!string.IsNullOrWhiteSpace(response.Id))
        {
            return Base64UrlEncoder.DecodeBytes(response.Id);
        }
        return MGuard.Fail<byte[]>("Assertion response does not contain credential id.");
    }

    private static string? TryEncodeUserHandle(byte[]? userHandle)
    {
        return userHandle is { Length: > 0 }
            ? Base64UrlEncoder.Encode(userHandle)
            : null;
    }

    private static string GetRegistrationCacheKey(Guid userId)
    {
        return $"webauthn:reg:{userId:N}";
    }

    private static string GetAuthenticationCacheKey(Guid userId)
    {
        return $"webauthn:auth:{userId:N}";
    }
}

/// <summary>
/// Result of a successful WebAuthn registration.
/// </summary>
public sealed record RegistrationResult
{
    /// <summary>
    /// Registered user identifier.
    /// </summary>
    public Guid UserId { get; init; }
    /// <summary>
    /// Registered credential id (base64url).
    /// </summary>
    public string CredentialId { get; init; } = string.Empty;
    /// <summary>
    /// Indicates whether the credential is syncable.
    /// </summary>
    public bool Syncable { get; init; }
    /// <summary>
    /// Authentication assurance level derived from the credential.
    /// </summary>
    public int Aal => Syncable ? 3 : 2;
}

/// <summary>
/// Result of a successful WebAuthn authentication.
/// </summary>
public sealed record AuthenticationResult
{
    /// <summary>
    /// Verification result from the WebAuthn assertion.
    /// </summary>
    public VerifyAssertionResult Verification { get; init; } = null!;
    /// <summary>
    /// Authentication assurance level.
    /// </summary>
    public int Aal { get; init; }
}
