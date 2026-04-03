namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Configures PostgreSQL database options for a <see cref="MDbContext"/>.
/// </summary>
public class PostgreSqlDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        options.UseNpgsql(connectionString);
    }
}
