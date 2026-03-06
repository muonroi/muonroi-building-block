namespace Muonroi.BuildingBlock.Test;

public class DefaultAuthContextFactoryTests
{
    [Fact]
    public void Create_Returns_Context_From_HttpItems()
    {
        DefaultHttpContext http = new();
        MAuthenticateInfoContext ctx = new(false);
        http.Items[typeof(MAuthenticateInfoContext).FullName!] = ctx;
        HttpContextAccessor accessor = new()
        {
            HttpContext = http
        };
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("db1").Options;
        TestDbContext db = new(options);
        DefaultAuthContextFactory factory = new(accessor, setting, config, db,
            NullLogger<MAuthenticateInfoContext>.Instance);

        MAuthenticateInfoContext result = factory.Create();

        Assert.Same(ctx, result);
    }

    [Fact]
    public void Create_No_Context_Returns_Default()
    {
        ResourceSetting setting = [];
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("db2").Options;
        TestDbContext db = new(options);
        DefaultAuthContextFactory factory = new(null, setting, config, db,
            NullLogger<MAuthenticateInfoContext>.Instance, null);

        MAuthenticateInfoContext result = factory.Create();

        Assert.False(result.IsAuthenticated);
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        DefaultAuthContextFactory factory = new(null, null!, null!, null!,
            NullLogger<MAuthenticateInfoContext>.Instance, null);
        Assert.NotNull(factory);
    }
}
