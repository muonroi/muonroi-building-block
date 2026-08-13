using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

namespace Muonroi.Data.EntityFrameworkCore.SqlServer;

/// <summary>
/// Configures SQL Server database options for a <see cref="MDbContext"/>.
/// </summary>
public class SqlServerDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    /// <inheritdoc />
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        MGuard.NotEmpty(connectionString);
        options.UseSqlServer(connectionString);
    }
}
