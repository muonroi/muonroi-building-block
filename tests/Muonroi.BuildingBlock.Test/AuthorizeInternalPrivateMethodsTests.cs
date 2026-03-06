using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Muonroi.Core.Abstractions.Helpers;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Auth.BearerToken.Signers;
using Muonroi.Data.EntityFrameworkCore.Auth;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Core.Abstractions.Constants;

namespace Muonroi.BuildingBlock.Test;

public class AuthorizeInternalPrivateMethodsTests
{
    private static (MTokenInfo Info, MAuthenticateTokenHelper<TestPerm> Helper) CreateTokenHelper()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            RefreshTokenTtl = 5,
            RefreshTokenEim = 5,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        return (info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)));
    }

    private static MethodInfo GetMethod(string name)
    {
        return typeof(AuthorizeInternal)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    [Fact]
    public void CreateValidationParameters_SymmetricKey()
    {
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        info.UseRsa = false;
        MethodInfo mi = GetMethod("CreateValidationParameters");
        TokenValidationParameters param = (TokenValidationParameters)mi.Invoke(null, [info])!;

        Assert.IsType<SymmetricSecurityKey>(param.IssuerSigningKey);
        Assert.Equal(info.Issuer, param.ValidIssuer);
        Assert.Equal(info.Audience, param.ValidAudience);
    }

    [Fact]
    public void CreateValidationParameters_RsaKey()
    {
        using RSA rsa = RSA.Create();
        string publicKey = rsa.ExportRSAPublicKeyPem();
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        info.UseRsa = true;
        info.PublicKey = publicKey;
        MethodInfo mi = GetMethod("CreateValidationParameters");
        TokenValidationParameters param = (TokenValidationParameters)mi.Invoke(null, [info])!;
        Assert.IsType<RsaSecurityKey>(param.IssuerSigningKey);
    }

    [Fact]
    public void CreateValidationParameters_InvalidRsaKey_Throws()
    {
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        info.UseRsa = true;
        info.PublicKey = "invalid";
        MethodInfo mi = GetMethod("CreateValidationParameters");
        Assert.ThrowsAny<Exception>(() => mi.Invoke(null, [info]));
    }

    [Fact]
    public async Task ResetLoginAttemptOnSuccess_WithHistory_UpdatesEntities()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("reset_success").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p",
            IsActive = false
        };
        await db.Users.AddAsync(user);
        MUserLoginAttempt attempt = new()
        {
            UserGuid = user.EntityId,
            AttemptTime = 3,
            LockTo = DateTime.UtcNow.AddMinutes(5)
        };
        await db.MUserLoginAttempts.AddAsync(attempt);
        await db.SaveChangesAsync();

        MethodInfo mi = GetMethod("ResetLoginAttemptOnSuccess").MakeGenericMethod(typeof(TestDbContext));
        await (Task)mi.Invoke(null, [user, attempt, db, CancellationToken.None])!;

        MUserLoginAttempt updated = await db.MUserLoginAttempts.FirstAsync();
        Assert.Equal(0, updated.AttemptTime);
        Assert.Equal(DateTime.MinValue, updated.LockTo);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task ResetLoginAttemptOnSuccess_NoHistory_ActivatesUser()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("reset_nohistory").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p",
            IsActive = false
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        MethodInfo mi = GetMethod("ResetLoginAttemptOnSuccess").MakeGenericMethod(typeof(TestDbContext));
        await (Task)mi.Invoke(null, [user, null, db, CancellationToken.None])!;

        Assert.True(user.IsActive);
    }

    [Fact]
    public void GenerateAccessToken_AddsClaims()
    {
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        List<TestPerm> perms = [TestPerm.Read, TestPerm.Write];
        List<Claim> extra = [new("extra", "1")];

        MethodInfo mi = GetMethod("GenerateAccessToken").MakeGenericMethod(typeof(TestPerm));
        object?[] args = [user, perms, null, null, helper, extra];
        mi.Invoke(null, args);
        string token = (string)args[2]!;
        string validity = (string)args[3]!;

        MethodInfo extract = GetMethod("ExtractClaimsFromToken");
        List<Claim> claims = (List<Claim>)extract.Invoke(null, [token])!;
        Claim? vClaim = claims.FirstOrDefault(c => c.Type == ClaimConstants.TokenValidityKey);
        Claim? perm = claims.FirstOrDefault(c => c.Type == ClaimConstants.Permission);
        Claim? extraClaim = claims.FirstOrDefault(c => c.Type == "extra");

        long expected = MPermissionExtension<TestPerm>.CalculatePermissionsBitmask(perms);
        Assert.Equal(validity, vClaim!.Value);
        Assert.Equal(expected.ToString(), perm!.Value);
        Assert.Equal("1", extraClaim!.Value);
    }

    [Fact]
    public void GenerateAccessToken_NoExtraClaims()
    {
        (MTokenInfo _, MAuthenticateTokenHelper<TestPerm> helper) = CreateTokenHelper();
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        List<TestPerm> perms = [TestPerm.One];

        MethodInfo mi = GetMethod("GenerateAccessToken").MakeGenericMethod(typeof(TestPerm));
        object?[] args = [user, perms, null, null, helper, null];
        mi.Invoke(null, args);
        string token = (string)args[2]!;

        MethodInfo extract = GetMethod("ExtractClaimsFromToken");
        List<Claim> claims = (List<Claim>)extract.Invoke(null, [token])!;
        Assert.DoesNotContain(claims, c => c.Type == "extra");
    }

    [Fact]
    public async Task HandleFailedLoginAttempt_IncrementsAttemptAndLocksUser()
    {
        IClockProvider original = Clock.Provider;
        FixedClockProvider provider = new(new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc));
        Clock.Provider = provider;

        try
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

            await using TestDbContext db = new(options);
            MUser user = new()
            {
                UserName = "user",
                EmailAddress = "user@example.com",
                Name = "User",
                Surname = "Test",
                Password = "pwd",
                IsActive = true
            };

            await db.Users.AddAsync(user);
            MUserLoginAttempt attempt = new()
            {
                UserGuid = user.EntityId,
                AttemptTime = 2,
                LockTo = DateTime.MinValue,
                CreationTime = provider.UtcNow
            };

            await db.MUserLoginAttempts.AddAsync(attempt);
            await db.SaveChangesAsync();

            DateTime baseline = provider.UtcNow;

            await AuthorizeInternal.HandleFailedLoginAttempt(user, attempt, db, CancellationToken.None);

            MUserLoginAttempt updated = await db.MUserLoginAttempts.SingleAsync();

            Assert.Equal(3, updated.AttemptTime);
            Assert.Equal(baseline.AddMinutes(5), updated.LockTo);
            Assert.False(user.IsActive);
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    [Fact]
    public async Task HandleFailedLoginAttempt_ReachesMaxAttempts_PermanentlyLocks()
    {
        IClockProvider original = Clock.Provider;
        FixedClockProvider provider = new(new DateTime(2024, 02, 01, 0, 0, 0, DateTimeKind.Utc));
        Clock.Provider = provider;

        try
        {
            DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

            await using TestDbContext db = new(options);
            MUser user = new()
            {
                UserName = "locked",
                EmailAddress = "locked@example.com",
                Name = "Locked",
                Surname = "User",
                Password = "pwd",
                IsActive = true
            };

            await db.Users.AddAsync(user);
            MUserLoginAttempt attempt = new()
            {
                UserGuid = user.EntityId,
                AttemptTime = 5,
                LockTo = DateTime.MinValue,
                CreationTime = provider.UtcNow
            };

            await db.MUserLoginAttempts.AddAsync(attempt);
            await db.SaveChangesAsync();

            await AuthorizeInternal.HandleFailedLoginAttempt(user, attempt, db, CancellationToken.None);

            MUserLoginAttempt updated = await db.MUserLoginAttempts.SingleAsync();

            Assert.Equal(6, updated.AttemptTime);
            Assert.Equal(DateTime.MaxValue, updated.LockTo);
            Assert.False(user.IsActive);
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    private sealed class FixedClockProvider(DateTime utcNow) : IClockProvider
    {
        private readonly DateTime _utcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        public DateTime Now => _utcNow.ToLocalTime();

        public DateTime UtcNow => _utcNow;

        public DateTimeKind Kind => DateTimeKind.Utc;

        public bool SupportsMultipleTimezone => true;

        public DateTime Normalize(DateTime dateTime)
        {
            return dateTime;
        }
    }
}
