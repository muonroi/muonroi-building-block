namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

public class MySqlDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        options.UseMySql(
            connectionString,
            Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString),
            builder => { builder.EnableStringComparisonTranslations(); });
    }
}
