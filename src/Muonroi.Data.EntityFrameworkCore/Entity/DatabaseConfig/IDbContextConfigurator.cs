namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Configures EF Core options for a specific <see cref="MDbContext"/>.
/// </summary>
public interface IDbContextConfigurator<T> where T : MDbContext
{
    /// <summary>Applies provider-specific configuration using the connection string.</summary>
    /// <param name="options">The options builder for the target context.</param>
    /// <param name="connectionString">The database connection string.</param>
    void Configure(DbContextOptionsBuilder<T> options, string connectionString);
}
