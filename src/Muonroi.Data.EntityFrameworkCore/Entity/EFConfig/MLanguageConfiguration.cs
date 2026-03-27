namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Configures the <see cref="MLanguage"/> entity model.
/// </summary>
public class MLanguageConfiguration : IEntityTypeConfiguration<MLanguage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MLanguage> builder)
    {
        builder.HasIndex(b => b.Name)
            .HasDatabaseName("IX_MLanguages_Name")
            .IsUnique();
    }
}
