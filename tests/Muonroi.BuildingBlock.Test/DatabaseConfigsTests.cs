

namespace Muonroi.BuildingBlock.Test
{
    public class DatabaseConfigsTests
    {
        [Fact]
        public void ConnectionStrings_Returns_Set_Value_Or_Null()
        {
            DatabaseConfigs cfg = new();
            Assert.Null(cfg.ConnectionStrings);
            ConnectionStrings cs = new();
            cfg.ConnectionStrings = cs;
            Assert.Same(cs, cfg.ConnectionStrings);
        }

        [Fact]
        public void DatabaseSettings_Returns_Set_Value_Or_Null()
        {
            DatabaseConfigs cfg = new();
            Assert.Null(cfg.DatabaseSettings);
            DatabaseSettings settings = new();
            cfg.DatabaseSettings = settings;
            Assert.Same(settings, cfg.DatabaseSettings);
        }

        [Fact]
        public void DbType_Returns_Value_Or_Empty()
        {
            DatabaseConfigs cfg = new();
            Assert.Equal(string.Empty, cfg.DbType);
            cfg.DbType = "Mongo";
            Assert.Equal("Mongo", cfg.DbType);
        }

        [Fact]
        public void MongoDbConnectionString_Returns_Value_Or_Null()
        {
            ConnectionStrings cs = new();
            Assert.Null(cs.MongoDbConnectionString);
            cs.MongoDbConnectionString = "mongo";
            Assert.Equal("mongo", cs.MongoDbConnectionString);
        }

        [Fact]
        public void SqlServerConnectionString_Returns_Value_Or_Null()
        {
            ConnectionStrings cs = new();
            Assert.Null(cs.SqlServerConnectionString);
            cs.SqlServerConnectionString = "sql";
            Assert.Equal("sql", cs.SqlServerConnectionString);
        }

        [Fact]
        public void MySqlConnectionString_Returns_Value_Or_Null()
        {
            ConnectionStrings cs = new();
            Assert.Null(cs.MySqlConnectionString);
            cs.MySqlConnectionString = "mysql";
            Assert.Equal("mysql", cs.MySqlConnectionString);
        }

        [Fact]
        public void PostgreSqlConnectionString_Returns_Value_Or_Null()
        {
            ConnectionStrings cs = new();
            Assert.Null(cs.PostgreSqlConnectionString);
            cs.PostgreSqlConnectionString = "pg";
            Assert.Equal("pg", cs.PostgreSqlConnectionString);
        }

        [Fact]
        public void SqliteConnectionString_Returns_Value_Or_Null()
        {
            ConnectionStrings cs = new();
            Assert.Null(cs.SqliteConnectionString);
            cs.SqliteConnectionString = "sqlite";
            Assert.Equal("sqlite", cs.SqliteConnectionString);
        }

        [Fact]
        public void DatabaseName_Returns_Value_Or_Empty()
        {
            DatabaseSettings settings = new();
            Assert.Equal(string.Empty, settings.DatabaseName);
            settings.DatabaseName = "db";
            Assert.Equal("db", settings.DatabaseName);
        }
    }
}
