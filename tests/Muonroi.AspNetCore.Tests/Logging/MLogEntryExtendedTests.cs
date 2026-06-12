namespace Muonroi.AspNetCore.Tests.Logging;

public class MLogEntryExtendedTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var entry = new MLogEntry();

        entry.Identity.Should().NotBeNullOrEmpty();
        entry.TransactionId.Should().BeNull();
        entry.SiteCode.Should().BeNull();
        entry.TenantId.Should().BeNull();
        entry.LogType.Should().BeNull();
        entry.Server.Should().BeNull();
        entry.Caller.Should().BeNull();
        entry.Client.Should().BeNull();
        entry.Username.Should().BeNull();
        entry.Location.Should().BeNull();
        entry.Data.Should().BeNull();
        entry.Elapsed.Should().Be(0);
        entry.ErrorCode.Should().BeNull();
        entry.Message.Should().BeNull();
        entry.LogTrace.Should().BeNull();
        entry.ApplicationInfo.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var entry = new MLogEntry
        {
            Identity = "test-id",
            TransactionId = "tx-123",
            SiteCode = "SITE-01",
            TenantId = "tenant-1",
            LogType = "Error",
            Server = "server-1",
            Caller = "TestCaller",
            Client = "TestClient",
            Username = "admin",
            Location = "/api/test",
            Data = "{\"key\":\"value\"}",
            Elapsed = 150,
            ErrorCode = "ERR001",
            Message = "Something happened",
            LogTrace = "stack trace here",
            ApplicationInfo = "MyApp v1.0"
        };

        entry.Identity.Should().Be("test-id");
        entry.TransactionId.Should().Be("tx-123");
        entry.SiteCode.Should().Be("SITE-01");
        entry.TenantId.Should().Be("tenant-1");
        entry.LogType.Should().Be("Error");
        entry.Server.Should().Be("server-1");
        entry.Caller.Should().Be("TestCaller");
        entry.Client.Should().Be("TestClient");
        entry.Username.Should().Be("admin");
        entry.Location.Should().Be("/api/test");
        entry.Data.Should().Be("{\"key\":\"value\"}");
        entry.Elapsed.Should().Be(150);
        entry.ErrorCode.Should().Be("ERR001");
        entry.Message.Should().Be("Something happened");
        entry.LogTrace.Should().Be("stack trace here");
        entry.ApplicationInfo.Should().Be("MyApp v1.0");
    }

    [Fact]
    public void ExecDate_DefaultsToRecentUtcNow()
    {
        var entry = new MLogEntry();
        entry.ExecDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MultipleInstances_HaveDifferentIdentities()
    {
        var entry1 = new MLogEntry();
        var entry2 = new MLogEntry();

        entry1.Identity.Should().NotBe(entry2.Identity);
    }
}
