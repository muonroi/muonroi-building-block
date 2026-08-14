namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceMethodsTests
{
    private static PermissionService<TestPerm, TestDbContext> CreateService(TestDbContext db,
        MAuthenticateInfoContext ctx)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        return new PermissionService<TestPerm, TestDbContext>(db, ctx, new FakeDateTimeService());
    }

    [Fact]
    public async Task GetRolesAsync_Returns_NonDeleted_Roles()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_roles").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r1",
            DisplayName = "r1",
            NormalizedName = "R1"
        };
        await db.Roles.AddRangeAsync(
            role,
            new MRole { Name = "r2", DisplayName = "r2", NormalizedName = "R2" },
            new MRole { Name = "del", DisplayName = "del", NormalizedName = "DEL", IsDeleted = true });
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<MRole>> result = await svc.GetRolesAsync(CancellationToken.None);

        Assert.Equal(2, result.Result?.Count);
        Assert.DoesNotContain(result.Result!, r => r.IsDeleted);
    }

    [Fact]
    public async Task GetRolesAsync_No_Roles_Returns_Empty()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_roles_empty").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<MRole>> result = await svc.GetRolesAsync(CancellationToken.None);

        Assert.Empty(result.Result!);
    }

    [Fact]
    public async Task GetUserPermissionTreeAsync_Returns_Tree()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("tree_success").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission p1 = new()
        {
            Name = ((long)TestPerm.Read).ToString()
        };
        MPermission p2 = new()
        {
            Name = ((long)TestPerm.Write).ToString()
        };
        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.Permissions.AddRangeAsync(p1, p2);
        await db.SaveChangesAsync();
        MUserRole entity = new()
        {
            UserId = user.EntityId,
            RoleId = role.EntityId
        };
        await db.UserRoles.AddAsync(entity);
        MRolePermission permission = new()
        {
            RoleId = role.EntityId,
            PermissionId = p1.EntityId
        };
        await db.RolePermissions.AddRangeAsync(
            permission,
            new MRolePermission { RoleId = role.EntityId, PermissionId = p2.EntityId });
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<PermissionTree> result = await svc.GetUserPermissionTreeAsync(user.EntityId, CancellationToken.None);

        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task GetUserPermissionTreeAsync_UserWithout_Permissions_Returns_Empty()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("tree_empty").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<PermissionTree> result = await svc.GetUserPermissionTreeAsync(user.EntityId, CancellationToken.None);

        Assert.Empty(result.Result!.Menus);
    }

    [Fact]
    public async Task GetUserPermissionTreeAsync_InvalidUser_Returns_Empty()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("tree_invalid").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<PermissionTree> result = await svc.GetUserPermissionTreeAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(result.Result!.Menus);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_Returns_Permissions()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("user_perms_success").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission p1 = new()
        {
            Name = ((long)TestPerm.Read).ToString()
        };
        await db.Users.AddAsync(user);
        await db.Roles.AddAsync(role);
        await db.Permissions.AddAsync(p1);
        await db.SaveChangesAsync();
        MUserRole entity = new()
        {
            UserId = user.EntityId,
            RoleId = role.EntityId
        };
        await db.UserRoles.AddAsync(entity);
        MRolePermission permission = new()
        {
            RoleId = role.EntityId,
            PermissionId = p1.EntityId
        };
        await db.RolePermissions.AddAsync(permission);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<TestPerm>> result = await svc.GetUserPermissionsAsync(user.EntityId, CancellationToken.None);

        Assert.Contains(TestPerm.Read, result.Result!);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_NoPermissions_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("user_perms_none").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<TestPerm>> result = await svc.GetUserPermissionsAsync(user.EntityId, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_InvalidUser_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("user_perms_invalid").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<TestPerm>> result = await svc.GetUserPermissionsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAsync_Revokes_Token()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logout_success_perm").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRefreshToken token = new()
        {
            TokenValidityKey = Guid.NewGuid().ToString(),
            CreatorUserId = user.EntityId
        };
        await db.Users.AddAsync(user);
        await db.RefreshTokens.AddAsync(token);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            TokenValidityKey = token.TokenValidityKey,
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        MRefreshToken check = await db.RefreshTokens.FirstAsync();
        Assert.True(check.IsRevoked);
    }

    [Fact]
    public async Task LogoutAsync_User_Not_Found_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logout_user_nf_perm").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = Guid.NewGuid().ToString(),
            TokenValidityKey = Guid.NewGuid().ToString(),
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAsync_Token_Not_Found_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logout_token_nf_perm").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            TokenValidityKey = Guid.NewGuid().ToString(),
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAllAsync_Revokes_All_Tokens()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logoutall_success_perm").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        MRefreshToken token = new()
        {
            CreatorUserId = user.EntityId,
            TokenValidityKey = Guid.NewGuid().ToString()
        };
        await db.RefreshTokens.AddRangeAsync(
            token,
            new MRefreshToken { CreatorUserId = user.EntityId, TokenValidityKey = Guid.NewGuid().ToString() });
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.All(db.RefreshTokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task LogoutAllAsync_NoTokens_Returns_OK()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logoutall_notokens_perm").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
    }

    [Fact]
    public async Task LogoutAllAsync_ExpiredTokens_Revoked()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logoutall_expired_perm").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        MRefreshToken entity = new()
        {
            CreatorUserId = user.EntityId,
            TokenValidityKey = Guid.NewGuid().ToString(),
            ExpiredDate = Clock.UtcNow.AddDays(-1)
        };
        await db.RefreshTokens.AddAsync(entity);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        MRefreshToken token = await db.RefreshTokens.FirstAsync();
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task LogoutAllAsync_RevokedOrDeletedTokens_Ignored()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logoutall_revoked_perm").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        MRefreshToken token = new()
        {
            CreatorUserId = user.EntityId,
            TokenValidityKey = Guid.NewGuid().ToString(),
            IsRevoked = true
        };
        await db.RefreshTokens.AddRangeAsync(
            token,
            new MRefreshToken
            { CreatorUserId = user.EntityId, TokenValidityKey = Guid.NewGuid().ToString(), IsDeleted = true });
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = user.EntityId.ToString(),
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.All(db.RefreshTokens.Where(t => !t.IsDeleted), t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task LogoutAllAsync_Invalid_UserGuid_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("logoutall_invalid_guid_perm").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = "bad-guid",
            Language = "en"
        };
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.LogoutAllAsync(CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task LogoutAllAsync_Db_Error_Throws()
    {
        const string dbName = "logoutall_db_error_perm";
        DbContextOptions<TestDbContext> seedOpt = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(dbName).Options;
        string userId = Guid.NewGuid().ToString();
        using (TestDbContext seed = new(seedOpt))
        {
            MRefreshToken token = new()
            {
                CreatorUserId = Guid.Parse(userId),
                TokenValidityKey = Guid.NewGuid().ToString()
            };
            await seed.RefreshTokens.AddAsync(token);
            await seed.SaveChangesAsync();
        }

        DbContextOptions<FaultyDbContext> options = new DbContextOptionsBuilder<FaultyDbContext>().UseInMemoryDatabase(dbName).Options;
        using FaultyDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(true)
        {
            CurrentUserGuid = userId,
            Language = "en"
        };
        PermissionService<TestPerm, FaultyDbContext> svc = new(db, ctx, new FakeDateTimeService());

        await Assert.ThrowsAsync<Exception>(() => svc.LogoutAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RemovePermissionFromRoleAsync_Removes_WhenExists()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("remove_perm_success").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = ((long)TestPerm.Read).ToString()
        };
        await db.Roles.AddAsync(role);
        await db.Permissions.AddAsync(perm);
        await db.SaveChangesAsync();
        MRolePermission rp = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };
        await db.RolePermissions.AddAsync(rp);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.RemovePermissionFromRoleAsync(role.EntityId, perm.EntityId, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True((await db.RolePermissions.FirstAsync()).IsDeleted);
    }

    [Fact]
    public async Task RemovePermissionFromRoleAsync_InvalidIds_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("remove_perm_invalid").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.RemovePermissionFromRoleAsync(Guid.Empty, Guid.Empty, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task RemovePermissionFromRoleAsync_Permission_Not_In_Role_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("remove_perm_notfound").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = ((long)TestPerm.Read).ToString()
        };
        await db.Roles.AddAsync(role);
        await db.Permissions.AddAsync(perm);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<object> result = await svc.RemovePermissionFromRoleAsync(role.EntityId, perm.EntityId, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task UpdateRoleAsync_Updates_Role()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("update_role_success").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        await db.Roles.AddAsync(role);
        await db.SaveChangesAsync();
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);
        UpdateRoleRequestModel request = new()
        {
            Name = "new",
            DisplayName = "new",
            Id = role.EntityId
        };

        MResponse<MRole> result = await svc.UpdateRoleAsync(request, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Equal("new", result.Result?.Name);
    }

    [Fact]
    public async Task UpdateRoleAsync_Role_Not_Found_Returns_Error()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("update_role_nf").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, ctx);
        UpdateRoleRequestModel request = new()
        {
            Name = "n",
            DisplayName = "new",
            Id = Guid.NewGuid()
        };

        MResponse<MRole> result = await svc.UpdateRoleAsync(request, CancellationToken.None);

        Assert.False(result.IsOk);
    }
}
