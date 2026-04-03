using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Configures SQLite database options for a <see cref="MDbContext"/>.
/// </summary>
public class SqliteDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        MGuard.NotEmpty(connectionString);
        options.UseSqlite(connectionString);
    }
}
