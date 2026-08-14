namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceAdditionalTests
{
    private static PermissionService<TestPerm, TestDbContext> CreateService(TestDbContext db,
        string? currentUserGuid = null)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        string userGuid = currentUserGuid ?? Guid.NewGuid().ToString();
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUserGuid = userGuid,
            Language = "en"
        };
        return new PermissionService<TestPerm, TestDbContext>(db, ctx, new FakeDateTimeService());
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_Success()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_success").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Roles.Add(role);
        db.Permissions.Add(perm);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };

        MResponse<object> result = await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.Single(db.RolePermissions.Where(rp => rp.RoleId == role.EntityId && rp.PermissionId == perm.EntityId));
        Assert.Single(db.PermissionAuditLogs);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_SetsAuditLogPerformedBy_WhenUserGuidValid()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_audit_user").Options;
        using TestDbContext db = new(options);
        Guid expectedUserId = Guid.NewGuid();
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Roles.Add(role);
        db.Permissions.Add(perm);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, expectedUserId.ToString());
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };

        await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        MPermissionAuditLog audit = await db.PermissionAuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(expectedUserId, audit.PerformedBy);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_InvalidUserGuid_SetsAuditLogPerformedByNull()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_audit_null").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Roles.Add(role);
        db.Permissions.Add(perm);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, "invalid-guid");
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };

        await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        MPermissionAuditLog audit = await db.PermissionAuditLogs.AsNoTracking().SingleAsync();
        Assert.Null(audit.PerformedBy);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_Creates_Audit_Log_With_Assign_Action()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_audit_assign").Options;
        using TestDbContext db = new(options);
        Guid expectedUserId = Guid.NewGuid();
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = TestPerm.Write.ToString()
        };
        await db.Roles.AddAsync(role);
        await db.Permissions.AddAsync(perm);
        await db.SaveChangesAsync();

        PermissionService<TestPerm, TestDbContext> svc = CreateService(db, expectedUserId.ToString());
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };

        MResponse<object> result = await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        Assert.True(result.IsOk);
        MPermissionAuditLog audit = await db.PermissionAuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal("Assign", audit.Action);
        Assert.Equal(role.EntityId, audit.RoleId);
        Assert.Equal(perm.EntityId, audit.PermissionId);
        Assert.Equal(expectedUserId, audit.PerformedBy);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_NotFound()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_nf").Options;
        using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);
        AssignPermissionRequestModel req = new()
        {
            RoleId = Guid.NewGuid(),
            PermissionId = Guid.NewGuid()
        };

        MResponse<object> result = await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_AlreadyAssigned()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_dup").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Roles.Add(role);
        db.Permissions.Add(perm);
        db.SaveChanges();
        MRolePermission entity = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };
        db.RolePermissions.Add(entity);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };

        MResponse<object> result = await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_DbError_Throws()
    {
        const string dbName = "assign_perm_db_error";
        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        using (TestDbContext seed = new(seedOptions))
        {
            MRole entity = new()
            {
                Name = "r",
                DisplayName = "r",
                NormalizedName = "R"
            };
            seed.Roles.Add(entity);
            MPermission permission = new()
            {
                Name = TestPerm.Read.ToString()
            };
            seed.Permissions.Add(permission);
            seed.SaveChanges();
        }

        DbContextOptions<FaultyDbContext> options = new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        using FaultyDbContext db = new(options);
        PermissionService<TestPerm, FaultyDbContext> svc = new(db, new MAuthenticateInfoContext(false), new FakeDateTimeService());
        MRole role = await db.Roles.FirstAsync();
        MPermission perm = await db.Permissions.FirstAsync();
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };

        await Assert.ThrowsAsync<Exception>(() => svc.AssignPermissionToRoleAsync(req, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteRoleAsync_Success()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("delete_role_success").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        db.Roles.Add(role);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<object> result = await svc.DeleteRoleAsync(role.EntityId, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True(db.Roles.First().IsDeleted);
    }

    [Fact]
    public async Task DeleteRoleAsync_NotFound()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("delete_role_nf").Options;
        using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<object> result = await svc.DeleteRoleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task DeleteRoleAsync_AssignedRole_Deletes()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("delete_role_assigned").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        db.Roles.Add(role);
        db.Users.Add(user);
        db.SaveChanges();
        MUserRole entity = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        db.UserRoles.Add(entity);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<object> result = await svc.DeleteRoleAsync(role.EntityId, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True(db.Roles.First().IsDeleted);
    }

    [Fact]
    public async Task DeleteRoleAsync_DbError_Throws()
    {
        const string dbName = "delete_role_db_error";
        DbContextOptions<TestDbContext> seedOptions = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        using (TestDbContext seed = new(seedOptions))
        {
            MRole entity = new()
            {
                Name = "r",
                DisplayName = "r",
                NormalizedName = "R"
            };
            seed.Roles.Add(entity);
            seed.SaveChanges();
        }

        DbContextOptions<FaultyDbContext> options = new DbContextOptionsBuilder<FaultyDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        using FaultyDbContext db = new(options);
        PermissionService<TestPerm, FaultyDbContext> svc = new(db, new MAuthenticateInfoContext(false), new FakeDateTimeService());
        MRole role = await db.Roles.FirstAsync();

        await Assert.ThrowsAsync<Exception>(() => svc.DeleteRoleAsync(role.EntityId, CancellationToken.None));
    }

    [Fact]
    public async Task GetPermissionsAsync_Returns_List()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_perms").Options;
        using TestDbContext db = new(options);
        MPermission entity = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Permissions.AddRange(entity,
            new MPermission { Name = TestPerm.Write.ToString() });
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MPermission>> result = await svc.GetPermissionsAsync(CancellationToken.None);

        Assert.Equal(2, result.Result?.Count);
    }

    [Fact]
    public async Task GetPermissionsAsync_NoPermissions()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_perms_empty").Options;
        using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MPermission>> result = await svc.GetPermissionsAsync(CancellationToken.None);

        Assert.Empty(result.Result!);
    }

    [Fact]
    public async Task GetPermissionsAsync_Exclude_Deleted()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_perms_filter").Options;
        using TestDbContext db = new(options);
        MPermission entity = new()
        {
            Name = "a"
        };
        db.Permissions.AddRange(
            entity,
            new MPermission { Name = "b", IsDeleted = true });
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MPermission>> result = await svc.GetPermissionsAsync(CancellationToken.None);

        Assert.Single(result.Result!);
        Assert.DoesNotContain(result.Result!, p => p.IsDeleted);
    }

    [Fact]
    public async Task GetRolePermissionsAsync_Returns_List()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_role_perms").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Roles.Add(role);
        db.Permissions.Add(perm);
        db.SaveChanges();
        MRolePermission entity = new()
        {
            RoleId = role.EntityId,
            PermissionId = perm.EntityId
        };
        db.RolePermissions.Add(entity);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MPermission>> result = await svc.GetRolePermissionsAsync(role.EntityId, CancellationToken.None);

        Assert.Single(result.Result!);
    }

    [Fact]
    public async Task GetRolePermissionsAsync_Role_Not_Found()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_role_perms_nf").Options;
        using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MPermission>> result = await svc.GetRolePermissionsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(result.Result!);
    }

    [Fact]
    public async Task GetRolePermissionsAsync_NoPermissions()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_role_perms_empty").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        db.Roles.Add(role);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MPermission>> result = await svc.GetRolePermissionsAsync(role.EntityId, CancellationToken.None);

        Assert.Empty(result.Result!);
    }

    [Fact]
    public async Task GetRoleUsersAsync_Returns_List()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_role_users").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        db.Roles.Add(role);
        db.Users.Add(user);
        db.SaveChanges();
        MUserRole entity = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        db.UserRoles.Add(entity);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MUser>> result = await svc.GetRoleUsersAsync(role.EntityId, CancellationToken.None);

        Assert.Single(result.Result!);
    }

    [Fact]
    public async Task GetRoleUsersAsync_Role_Not_Found()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_role_users_nf").Options;
        using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MUser>> result = await svc.GetRoleUsersAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(result.Result!);
    }

    [Fact]
    public async Task GetRoleUsersAsync_NoUsers()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_role_users_empty").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        db.Roles.Add(role);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<List<MUser>> result = await svc.GetRoleUsersAsync(role.EntityId, CancellationToken.None);

        Assert.Empty(result.Result!);
    }
}
