using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using Muonroi.Core.Extensions;
using Muonroi.Core.Timing;
using Muonroi.Auth.BearerToken.Signers;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Data.EntityFrameworkCore.Auth;

namespace Muonroi.BuildingBlock.Test;

public class DefaultRefreshTokenValidatorTests
{
    private static (DefaultRefreshTokenValidator<TestDbContext, TestPerm> Validator, DefaultHttpContext Context,
        TestDbContext Db) CreateValidator(bool expired = false, bool invalidToken = false)
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        db.Users.Add(user);
        db.SaveChanges();

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
        MAuthenticateTokenHelper<TestPerm> helper = new(info, new HmacTokenSigner(info.SymmetricSecretKey));
        string validity = Guid.NewGuid().ToString();
        MUserModel model = new(user.EntityId.ToString(), user.UserName, validity, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress);
        List<Claim> extra = [new(ClaimConstants.TokenValidityKey, validity)];
        string token = invalidToken ? "Bearer invalid" : helper.GenerateAuthenticateToken(model, [TestPerm.One], extra);

        if (!invalidToken)
        {
            MRefreshToken refresh = new()
            {
                Token = "r",
                TokenValidityKey = validity,
                CreatorUserId = user.EntityId,
                CreationTime = Clock.UtcNow,
                LastUsedDate = Clock.UtcNow,
                ExpiredDate = expired ? Clock.UtcNow.AddMinutes(-1) : Clock.UtcNow.AddMinutes(10)
            };
            db.RefreshTokens.Add(refresh);
            db.SaveChanges();
        }

        DefaultHttpContext ctx = new();
        ctx.Request.Headers.Authorization = token;
        MultiLevelCacheService cache = new(new MemoryCache(new MemoryCacheOptions()), new InMemoryDistributedCache());
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EnableEncryption"] = "false",
                ["ApiKey"] = "key"
            })
            .Build();
        DefaultRefreshTokenValidator<TestDbContext, TestPerm> validator = new(db, cache, setting, config, info,
            new NullSerilogLogger());
        return (validator, ctx, db);
    }

    [Fact]
    public void Constructor_Creates_Instance()
    {
        (DefaultRefreshTokenValidator<TestDbContext, TestPerm> validator, DefaultHttpContext _, TestDbContext db) = CreateValidator();
        Assert.NotNull(validator);
        db.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_Returns_Context_When_Token_Valid()
    {
        (DefaultRefreshTokenValidator<TestDbContext, TestPerm> validator, DefaultHttpContext ctx, TestDbContext db) = CreateValidator();
        IAuthenticateInfoContext? result = await validator.ValidateAsync(ctx);
        Assert.NotNull(result);
        Assert.Same(result, ctx.Items[typeof(IAuthenticateInfoContext).FullName!]);
        await db.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAsync_Invalid_Token_Returns_Null()
    {
        (DefaultRefreshTokenValidator<TestDbContext, TestPerm> validator, DefaultHttpContext ctx, TestDbContext db) = CreateValidator(invalidToken: true);
        IAuthenticateInfoContext? result = await validator.ValidateAsync(ctx);
        Assert.Null(result);
        await db.DisposeAsync();
    }

    [Fact]
    public async Task ValidateAsync_Expired_Token_Returns_Context()
    {
        (DefaultRefreshTokenValidator<TestDbContext, TestPerm> validator, DefaultHttpContext ctx, TestDbContext db) = CreateValidator(true);
        IAuthenticateInfoContext? result = await validator.ValidateAsync(ctx);
        Assert.NotNull(result);
        await db.DisposeAsync();
    }
}
