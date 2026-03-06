namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

public class PostgreSqlDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        options.UseNpgsql(connectionString);
    }
}
