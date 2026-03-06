using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;

namespace Muonroi.Auth.Mfa.WebAuthenticate;

public class WebAuthenticateService(
    IFido2 fido2,
    IDistributedCache challengeCache,
    MDbContext context,
    IMJsonSerializeService jsonService,
    IMDateTimeService dateTimeService)
{
    private static readonly DistributedCacheEntryOptions ChallengeTtl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<CredentialCreateOptions> BeginRegistrationAsync(
        Guid userId,
        string userName,
        string displayName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        List<PublicKeyCredentialDescriptor> existingCredentials = await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new PublicKeyCredentialDescriptor(x.CredentialId))
            .ToListAsync(ct);

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

    public async Task<RegistrationResult> CompleteRegistrationAsync(
        Guid userId,
        AuthenticatorAttestationRawResponse response,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        CredentialCreateOptions options = await GetRequiredRegistrationOptionsAsync(userId, ct);
        RegisteredPublicKeyCredential result = await fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = IsCredentialIdUniqueToUserAsync
            },
            ct);

        MWebAuthnCredential credential = new()
        {
            UserId = userId,
            CredentialId = result.Id,
            PublicKey = result.PublicKey,
            SignCount = result.SignCount,
            AaGuid = result.AaGuid,
            CredType = "public-key",
            IsBackupEligible = result.IsBackupEligible,
            IsBackedUp = result.IsBackedUp
        };

        await context.WebAuthnCredentials.AddAsync(credential, ct);
        await context.SaveChangesAsync(ct);
        await challengeCache.RemoveAsync(GetRegistrationCacheKey(userId), ct);

        bool syncable = credential.IsBackupEligible || credential.IsBackedUp;
        return new RegistrationResult
        {
            UserId = userId,
            CredentialId = Base64UrlEncoder.Encode(credential.CredentialId),
            Syncable = syncable
        };
    }

    public async Task<AssertionOptions> BeginAuthenticationAsync(Guid userId, CancellationToken ct = default)
    {
        List<PublicKeyCredentialDescriptor> allowedCredentials = await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new PublicKeyCredentialDescriptor(x.CredentialId))
            .ToListAsync(ct);

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

    public async Task<AuthenticationResult> CompleteAuthenticationAsync(
        Guid userId,
        AuthenticatorAssertionRawResponse response,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        AssertionOptions options = await GetRequiredAuthenticationOptionsAsync(userId, ct);
        byte[] credentialId = ExtractCredentialId(response);

        List<MWebAuthnCredential> credentials = await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId)
            .ToListAsync(ct);
        MWebAuthnCredential? credential = credentials.FirstOrDefault(x => x.CredentialId.SequenceEqual(credentialId))
            ?? throw new InvalidOperationException("Credential not found for user.");
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

        await context.SaveChangesAsync(ct);
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
            throw new InvalidOperationException("Registration challenge not found or expired.");
        }

        CredentialCreateOptions? options = jsonService.Deserialize<CredentialCreateOptions>(raw);
        return options ?? throw new InvalidOperationException("Registration challenge payload is invalid.");
    }

    private async Task<AssertionOptions> GetRequiredAuthenticationOptionsAsync(Guid userId, CancellationToken ct)
    {
        string key = GetAuthenticationCacheKey(userId);
        string? raw = await challengeCache.GetStringAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Authentication challenge not found or expired.");
        }

        AssertionOptions? options = jsonService.Deserialize<AssertionOptions>(raw);
        return options ?? throw new InvalidOperationException("Authentication challenge payload is invalid.");
    }

    private async Task<bool> IsCredentialIdUniqueToUserAsync(IsCredentialIdUniqueToUserParams input, CancellationToken ct)
    {
        List<byte[]> existingIds = await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(x => x.CredentialId)
            .ToListAsync(ct);
        return existingIds.All(x => !x.SequenceEqual(input.CredentialId));
    }

    private async Task<bool> IsUserHandleOwnerOfCredentialAsync(IsUserHandleOwnerOfCredentialIdParams input, CancellationToken ct)
    {
        List<MWebAuthnCredential> credentials = await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(ct);

        MWebAuthnCredential? credential = credentials.FirstOrDefault(x => x.CredentialId.SequenceEqual(input.CredentialId));
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

        throw new InvalidOperationException("Assertion response does not contain credential id.");
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

public sealed record RegistrationResult
{
    public Guid UserId { get; init; }
    public string CredentialId { get; init; } = string.Empty;
    public bool Syncable { get; init; }
    public int Aal => Syncable ? 3 : 2;
}

public sealed record AuthenticationResult
{
    public VerifyAssertionResult Verification { get; init; } = null!;
    public int Aal { get; init; }
}
