namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

/// <summary>
/// Seeds default languages into the database.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <param name="context">The database context.</param>
/// <param name="dateTimeService">The date/time service.</param>
public class DefaultLanguagesCreator<TContext>(TContext context, IMDateTimeService dateTimeService)
    where TContext : MDbContext
{
    /// <summary>
    /// Gets the initial language list used for seeding.
    /// </summary>
    public static List<MLanguage> InitialLanguages => GetInitialLanguages();

    private static List<MLanguage> GetInitialLanguages()
    {
        return
        [
            new MLanguage("en", "English", "famfamfam-flags us"),
            new MLanguage("vi", "Tiếng Việt", "famfamfam-flags vn")
        ];
    }

    /// <summary>
    /// Creates default languages if they do not already exist.
    /// </summary>
    public void Create()
    {
        CreateLanguages();
    }

    private void CreateLanguages()
    {
        foreach (MLanguage language in InitialLanguages) AddLanguageIfNotExists(language);
    }

    private void AddLanguageIfNotExists(MLanguage language)
    {
        language.CreatedDateTs = dateTimeService.UtcNow().GetTimeStamp();
        if (context.Languages.IgnoreQueryFilters().Any(l => l.Name == language.Name)) return;
        context.Languages.Add(language);
        context.SaveChanges();
    }
}
