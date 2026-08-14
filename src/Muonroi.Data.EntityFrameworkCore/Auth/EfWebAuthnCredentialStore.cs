namespace Muonroi.Data.EntityFrameworkCore.Auth;

/// <summary>
/// EF Core implementation of <see cref="IWebAuthnCredentialStore"/> using <see cref="MDbContext"/>.
/// </summary>
public class EfWebAuthnCredentialStore(MDbContext context) : IWebAuthnCredentialStore
{
    /// <inheritdoc />
    public async Task<List<byte[]>> GetCredentialIdsByUserAsync(string? tenantId, Guid userId, CancellationToken ct = default)
    {
        return await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => x.CredentialId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<WebAuthnCredentialInfo>> GetCredentialsByUserAsync(string? tenantId, Guid userId, CancellationToken ct = default)
    {
        return await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => new WebAuthnCredentialInfo
            {
                TenantId = x.TenantId,
                UserId = x.UserId,
                CredentialId = x.CredentialId,
                PublicKey = x.PublicKey,
                SignCount = x.SignCount,
                AaGuid = x.AaGuid,
                CredType = x.CredType,
                IsBackupEligible = x.IsBackupEligible,
                IsBackedUp = x.IsBackedUp,
                UserHandle = x.UserHandle,
                LastUsedAt = x.LastUsedAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<byte[]>> GetAllCredentialIdsAsync(string? tenantId, CancellationToken ct = default)
    {
        return await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.CredentialId)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task SaveCredentialAsync(WebAuthnCredentialInfo credential, CancellationToken ct = default)
    {
        MWebAuthnCredential entity = new()
        {
            TenantId = credential.TenantId,
            UserId = credential.UserId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            AaGuid = credential.AaGuid,
            CredType = credential.CredType,
            IsBackupEligible = credential.IsBackupEligible,
            IsBackedUp = credential.IsBackedUp,
            UserHandle = credential.UserHandle,
            LastUsedAt = credential.LastUsedAt
        };

        await context.WebAuthnCredentials.AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateCredentialAsync(WebAuthnCredentialInfo credential, CancellationToken ct = default)
    {
        MWebAuthnCredential? entity = await context.WebAuthnCredentials
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.TenantId == credential.TenantId &&
                x.UserId == credential.UserId &&
                x.CredentialId == credential.CredentialId, ct);

        if (entity is null) return;

        entity.SignCount = credential.SignCount;
        entity.IsBackedUp = credential.IsBackedUp;
        entity.LastUsedAt = credential.LastUsedAt;
        entity.UserHandle = credential.UserHandle;

        await context.SaveChangesAsync(ct);
    }
}
