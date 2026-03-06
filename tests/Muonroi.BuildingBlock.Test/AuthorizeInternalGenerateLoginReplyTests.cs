using Microsoft.EntityFrameworkCore;

namespace Muonroi.BuildingBlock.Test;

public class AuthorizeInternalGenerateLoginReplyTests
{
    private static (MTokenInfo Info, MAuthenticateTokenHelper<TestPerm> Helper) CreateTokenHelper()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "abc",
            Issuer = "i",
            Audience = "a",
            ExpiryMinutes = 1,
            RefreshTokenTtl = 5,
            RefreshTokenEim = 5,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        return (info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)));
    }

    [Fact]
    public async Task GenerateLoginReply_Success()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("gen_login_success").Options;
        using TestDbContext db = new(options);
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a",
            Name = "n",
            Surname = "s",
            Password = "p"
        };

        MethodInfo mi =
            typeof(AuthorizeInternal).GetMethod("GenerateLoginReply", BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = mi.MakeGenericMethod(typeof(TestDbContext), typeof(TestPerm));
        Task<LoginResponseModel> task = (Task<LoginResponseModel>)generic.Invoke(null,
            ["acc", "ref", user, "val", info, db, cache, new List<TestPerm> { TestPerm.Read }])!;
        LoginResponseModel result = await task;

        Assert.Equal("u", result.Username);
        Assert.Equal("ref", result.RefreshToken);
        Assert.Single(db.RefreshTokens);
    }

    [Fact]
    public async Task GenerateLoginReply_NullUser_Throws()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("gen_login_null").Options;
        using TestDbContext db = new(options);
        InMemoryDistributedCache dist = new();
        IMemoryCache memory = new MemoryCache(new MemoryCacheOptions());
        MultiLevelCacheService cache = new(memory, dist);
        (MTokenInfo info, MAuthenticateTokenHelper<TestPerm> _) = CreateTokenHelper();
        MethodInfo mi =
            typeof(AuthorizeInternal).GetMethod("GenerateLoginReply", BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = mi.MakeGenericMethod(typeof(TestDbContext), typeof(TestPerm));

        await Assert.ThrowsAsync<NullReferenceException>(async () =>
        {
            Task<LoginResponseModel> t = (Task<LoginResponseModel>)generic.Invoke(null,
                ["a", "r", null!, "v", info, db, cache, new List<TestPerm>()])!;
            await t;
        });
    }
}
