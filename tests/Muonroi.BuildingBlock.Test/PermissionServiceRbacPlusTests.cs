using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class PermissionServiceRbacPlusTests
{
    [Fact]
    public async Task AssignRoleToUserAsync_Invalidates_UserPermissionCache()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using TestDbContext db = new(options);

        MRole role = new()
        {
            Name = "admin",
            DisplayName = "Admin",
            NormalizedName = "ADMIN"
        };
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        RecordingCacheService cache = new();
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> service = new(db, ctx, cache, new TestLicenseGuard());

        AssignRoleRequestModel model = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        MResponse<object> result = await service.AssignRoleToUserAsync(model, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Contains(RbacCacheKeys.UserPermissionsByEntityId(user.EntityId), cache.RemovedKeys);
        Assert.Contains(RbacCacheKeys.LegacyUserPermissions(user.EntityId), cache.RemovedKeys);
        Assert.Contains(RbacCacheKeys.UserPermissionsByNumericId(user.Id), cache.RemovedKeys);
        Assert.Contains(RbacCacheKeys.LegacyUserPermissions(user.Id), cache.RemovedKeys);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_Invalidates_AffectedUsersCache()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using TestDbContext db = new(options);

        MRole role = new()
        {
            Name = "admin",
            DisplayName = "Admin",
            NormalizedName = "ADMIN"
        };
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MPermission permission = new()
        {
            Name = "orders.view",
            UiKey = "orders.view"
        };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(user);
        await db.Permissions.AddAsync(permission);
        await db.SaveChangesAsync();
        MUserRole entity = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        await db.UserRoles.AddAsync(entity);
        await db.SaveChangesAsync();

        RecordingCacheService cache = new();
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> service = new(db, ctx, cache, new TestLicenseGuard());

        AssignPermissionRequestModel model = new()
        {
            RoleId = role.EntityId,
            PermissionId = permission.EntityId
        };
        MResponse<object> result = await service.AssignPermissionToRoleAsync(model, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Contains(RbacCacheKeys.UserPermissionsByEntityId(user.EntityId), cache.RemovedKeys);
    }

    [Fact]
    public async Task AdvancedAuthFeature_NotLicensed_ShouldThrow()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using TestDbContext db = new(options);

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> service = new(db, ctx, null, new DenyAdvancedAuthGuard());

        await Assert.ThrowsAsync<MInternalException>(() => service.GetRolesAsync(CancellationToken.None));
    }

    private sealed class RecordingCacheService : IMultiLevelCacheService
    {
        public HashSet<string> RemovedKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<T?> GetOrSetAsync<T>(string key, Func<Task<T?>> factory, int? absoluteExpirationInMinutes = 1440,
            CancellationToken token = default)
        {
            return factory();
        }

        public Task SetAsync<T>(string key, T value, int? absoluteExpirationInMinutes = 1440,
            CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken token = default)
        {
            return Task.FromResult<T?>(default);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class DenyAdvancedAuthGuard : ILicenseGuard
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
            return !string.Equals(featureName, FreeTierFeatures.Premium.AdvancedAuth, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
            {
                throw new InvalidOperationException("advanced-auth feature not licensed");
            }
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken()
        {
            return "TEST_CHAIN";
        }

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("test-key", encryptedData);
        }
    }
}
