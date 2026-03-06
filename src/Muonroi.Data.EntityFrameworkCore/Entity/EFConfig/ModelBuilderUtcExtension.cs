namespace Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;

public static class ModelBuilderUtcExtension
{
    private static readonly ValueConverter<DateTime, DateTime> _utcConverter = new(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> _utcNullableConverter = new(
        v => v.HasValue ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public static void UseUtcDateTime(this ModelBuilder modelBuilder)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        foreach (IMutableProperty property in entityType.GetProperties())
            if (property.ClrType == typeof(DateTime))
                property.SetValueConverter(_utcConverter);
            else if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(_utcNullableConverter);
    }
}
