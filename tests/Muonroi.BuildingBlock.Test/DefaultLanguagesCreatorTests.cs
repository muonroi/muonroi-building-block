namespace Muonroi.BuildingBlock.Test;

public class DefaultLanguagesCreatorTests
{
    [Fact]
    public void InitialLanguages_Returns_Default_List()
    {
        List<MLanguage> langs = DefaultLanguagesCreator<TestDbContext>.InitialLanguages;
        Assert.Equal(2, langs.Count);
        Assert.Contains(langs, l => l.Name == "en");
        Assert.Contains(langs, l => l.Name == "vi");
    }

    [Fact]
    public void GetInitialLanguages_Returns_Default_List()
    {
        MethodInfo mi = typeof(DefaultLanguagesCreator<TestDbContext>)
            .GetMethod("GetInitialLanguages", BindingFlags.Static | BindingFlags.NonPublic)!;
        List<MLanguage> langs = (List<MLanguage>)mi.Invoke(null, null)!;
        Assert.Equal(2, langs.Count);
        Assert.Contains(langs, l => l.Name == "en");
        Assert.Contains(langs, l => l.Name == "vi");
    }

    [Fact]
    public void Constructor_Allows_Null_Context()
    {
        DefaultLanguagesCreator<TestDbContext> creator = new(null!);
        Assert.NotNull(creator);
    }

    [Fact]
    public void Create_Adds_Languages_And_Ignores_Duplicates()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("create_adds").Options;
        using TestDbContext db = new(opts);
        DefaultLanguagesCreator<TestDbContext> creator = new(db);
        creator.Create();
        Assert.Equal(2, db.Languages.Count());
        creator.Create();
        Assert.Equal(2, db.Languages.Count());
    }

    [Fact]
    public void CreateLanguages_Adds_Without_Duplicates()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("create_langs").Options;
        using TestDbContext db = new(opts);
        DefaultLanguagesCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(DefaultLanguagesCreator<TestDbContext>)
            .GetMethod("CreateLanguages", BindingFlags.Instance | BindingFlags.NonPublic)!;
        mi.Invoke(creator, null);
        Assert.Equal(2, db.Languages.Count());
        mi.Invoke(creator, null);
        Assert.Equal(2, db.Languages.Count());
    }

    [Fact]
    public void AddLanguageIfNotExists_Behavior()
    {
        DbContextOptions<TestDbContext> opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("add_lang").Options;
        using TestDbContext db = new(opts);
        DefaultLanguagesCreator<TestDbContext> creator = new(db);
        MethodInfo mi = typeof(DefaultLanguagesCreator<TestDbContext>)
            .GetMethod("AddLanguageIfNotExists", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MLanguage fr = new("fr", "French");
        mi.Invoke(creator, [fr]);
        Assert.Equal(1, db.Languages.Count());
        mi.Invoke(creator, [fr]);
        Assert.Equal(1, db.Languages.Count());
        Assert.Throws<TargetInvocationException>(() => mi.Invoke(creator, [null!]));
    }
}
