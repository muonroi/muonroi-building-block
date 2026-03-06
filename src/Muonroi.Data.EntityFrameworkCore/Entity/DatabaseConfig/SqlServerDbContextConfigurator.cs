namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

public class SqlServerDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        options.UseSqlServer(connectionString);
    }
}
