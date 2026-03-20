namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Configures MySQL database options for a <see cref="MDbContext"/>.
/// </summary>
public class MySqlDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        options.UseMySql(
            connectionString,
            Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString),
            builder => { builder.EnableStringComparisonTranslations(); });
    }
}
