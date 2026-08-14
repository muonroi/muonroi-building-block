namespace Muonroi.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for static helper methods in AuthorizeInternal.
/// Uses reflection for internal methods, direct calls for public ones.
/// </summary>
public class AuthorizeInternalTests
{
    private sealed class AuthTestDbContext(DbContextOptions<AuthTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    #region IsAccountLocked (internal - via reflection)

    private static bool InvokeIsAccountLocked(MUserLoginAttempt? history, out string errorMessage)
    {
        MethodInfo method = typeof(AuthorizeInternal).GetMethod(
            "IsAccountLocked",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        object[] args = [history!, null!];
        bool result;
        try
        {
            result = (bool)method.Invoke(null, args)!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            throw; // unreachable
        }

        errorMessage = (string)args[1];
        return result;
    }

    [Fact]
    public void IsAccountLocked_Returns_False_When_No_History()
    {
        bool locked = InvokeIsAccountLocked(null, out string errorMessage);

        locked.Should().BeFalse();
        errorMessage.Should().BeEmpty();
    }

    [Fact]
    public void IsAccountLocked_Returns_False_When_LockTo_In_Past()
    {
        MUserLoginAttempt history = new()
        {
            LockTo = DateTime.UtcNow.AddMinutes(-5)
        };

        bool locked = InvokeIsAccountLocked(history, out _);

        locked.Should().BeFalse();
    }

    [Fact]
    public void IsAccountLocked_Returns_True_When_LockTo_In_Future()
    {
        MUserLoginAttempt history = new()
        {
            LockTo = DateTime.UtcNow.AddMinutes(5)
        };

        bool locked = InvokeIsAccountLocked(history, out string errorMessage);

        locked.Should().BeTrue();
        errorMessage.Should().NotBeEmpty();
    }

    #endregion

    #region GenerateRefreshToken (internal - via reflection)

    private static string InvokeGenerateRefreshToken()
    {
        MethodInfo method = typeof(AuthorizeInternal).GetMethod(
            "GenerateRefreshToken",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        object[] args = [null!];
        try
        {
            method.Invoke(null, args);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }

        return (string)args[0];
    }

    [Fact]
    public void GenerateRefreshToken_Returns_NonEmpty_Base64()
    {
        string refreshToken = InvokeGenerateRefreshToken();

        refreshToken.Should().NotBeNullOrEmpty();
        byte[] bytes = Convert.FromBase64String(refreshToken);
        bytes.Length.Should().Be(32);
    }

    #endregion

    #region ResolveTokenValidity (internal - via reflection)

    private static async Task<MResponse<string>> InvokeResolveTokenValidity(
        MDbContext dbContext, string tokenValidity, string lang, CancellationToken ct)
    {
        MethodInfo method = typeof(AuthorizeInternal).GetMethod(
            "ResolveTokenValidity",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(dbContext.GetType());

        try
        {
            Task<MResponse<string>> task = (Task<MResponse<string>>)generic.Invoke(null, [dbContext, tokenValidity, lang, ct])!;
            return await task;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            throw; // unreachable
        }
    }

    [Fact]
    public async Task ResolveTokenValidity_Returns_Error_When_Empty()
    {
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);

        MResponse<string> result = await InvokeResolveTokenValidity(db, "", "en", CancellationToken.None);

        result.IsOk.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveTokenValidity_Returns_Error_When_Token_Not_Found()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);

        MResponse<string> result = await InvokeResolveTokenValidity(db, "nonexistent-key", "en", CancellationToken.None);

        result.IsOk.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveTokenValidity_Returns_Token_When_Found()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);

        MRefreshToken token = new()
        {
            Token = "refresh-token-value",
            TokenValidityKey = "validity-key-123",
            CreatorUserId = Guid.NewGuid(),
            IsDeleted = false,
            IsRevoked = false,
            ExpiredDate = DateTime.UtcNow.AddHours(1),
            CreationTime = DateTime.UtcNow
        };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        MResponse<string> result = await InvokeResolveTokenValidity(db, "validity-key-123", "en", CancellationToken.None);

        result.IsOk.Should().BeTrue();
        result.Result.Should().Be("refresh-token-value");
    }

    [Fact]
    public async Task ResolveTokenValidity_Returns_Error_When_Token_Revoked()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);

        MRefreshToken token = new()
        {
            Token = "revoked-token",
            TokenValidityKey = "revoked-key",
            CreatorUserId = Guid.NewGuid(),
            IsDeleted = false,
            IsRevoked = true,
            ExpiredDate = DateTime.UtcNow.AddHours(1),
            CreationTime = DateTime.UtcNow
        };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        MResponse<string> result = await InvokeResolveTokenValidity(db, "revoked-key", "en", CancellationToken.None);

        result.IsOk.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveTokenValidity_Returns_Error_When_Token_Deleted()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);

        MRefreshToken token = new()
        {
            Token = "deleted-token",
            TokenValidityKey = "deleted-key",
            CreatorUserId = Guid.NewGuid(),
            IsDeleted = true,
            IsRevoked = false,
            ExpiredDate = DateTime.UtcNow.AddHours(1),
            CreationTime = DateTime.UtcNow
        };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();

        MResponse<string> result = await InvokeResolveTokenValidity(db, "deleted-key", "en", CancellationToken.None);

        result.IsOk.Should().BeFalse();
    }

    #endregion

    #region HandleFailedLoginAttempt (public)

    [Fact]
    public async Task HandleFailedLoginAttempt_Creates_New_Attempt_Record()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);
        MUser user = new()
        {
            EntityId = Guid.NewGuid(),
            UserName = "locktest",
            EmailAddress = "lock@test.com",
            Name = "Test",
            Surname = "User",
            Password = "p@ss",
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await AuthorizeInternal.HandleFailedLoginAttempt(user, null, db, CancellationToken.None);

        int count = await db.MUserLoginAttempts.CountAsync();
        count.Should().Be(1);
        MUserLoginAttempt attempt = await db.MUserLoginAttempts.FirstAsync();
        attempt.AttemptTime.Should().Be(1);
    }

    #endregion

    #region ResetLoginAttemptOnSuccess (internal - via reflection)

    private static async Task InvokeResetLoginAttemptOnSuccess(
        MUser user, MUserLoginAttempt? history, MDbContext dbContext, CancellationToken ct)
    {
        MethodInfo method = typeof(AuthorizeInternal).GetMethod(
            "ResetLoginAttemptOnSuccess",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(dbContext.GetType());

        try
        {
            Task task = (Task)generic.Invoke(null, [user, history, dbContext, ct])!;
            await task;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    [Fact]
    public async Task ResetLoginAttemptOnSuccess_Resets_AttemptTime()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<AuthTestDbContext> options = new DbContextOptionsBuilder<AuthTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using AuthTestDbContext db = new(options);
        MUser user = new()
        {
            EntityId = Guid.NewGuid(),
            UserName = "resettest",
            EmailAddress = "reset@test.com",
            Name = "Test",
            Surname = "User",
            Password = "p@ss",
            IsActive = false
        };
        db.Users.Add(user);
        MUserLoginAttempt attempt = new()
        {
            UserGuid = user.EntityId,
            AttemptTime = 3,
            LockTo = DateTime.UtcNow.AddMinutes(5),
            CreationTime = DateTime.UtcNow
        };
        db.MUserLoginAttempts.Add(attempt);
        await db.SaveChangesAsync();

        await InvokeResetLoginAttemptOnSuccess(user, attempt, db, CancellationToken.None);

        attempt.AttemptTime.Should().Be(0);
        attempt.LockTo.Should().Be(DateTime.MinValue);
        user.IsActive.Should().BeTrue();
    }

    #endregion
}
