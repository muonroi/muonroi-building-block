namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

/// <summary>
/// Provides model builder extensions for UTC date/time handling.
/// </summary>
public static class ModelBuilderUtcExtension
{
    private static readonly ValueConverter<DateTime, DateTime> _utcConverter = new(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> _utcNullableConverter = new(
        v => v.HasValue ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    /// <summary>Configures all <see cref="DateTime"/> properties to use UTC.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static void UseUtcDateTime(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        foreach (IMutableProperty property in entityType.GetProperties())
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(_utcConverter);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(_utcNullableConverter);
    }
}
