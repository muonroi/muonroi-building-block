namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

public class DefaultLanguagesCreator<TContext>(TContext context, IMDateTimeService dateTimeService)
    where TContext : MDbContext
{
    public static List<MLanguage> InitialLanguages => GetInitialLanguages();

    private static List<MLanguage> GetInitialLanguages()
    {
        return
        [
            new MLanguage("en", "English", "famfamfam-flags us"),
            new MLanguage("vi", "Tiếng Việt", "famfamfam-flags vn")
        ];
    }

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
