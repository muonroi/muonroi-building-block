namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public class MLanguageConfiguration : IEntityTypeConfiguration<MLanguage>
{
    public void Configure(EntityTypeBuilder<MLanguage> builder)
    {
        builder.HasIndex(b => b.Name)
            .HasDatabaseName("IX_MLanguages_Name")
            .IsUnique();
    }
}
