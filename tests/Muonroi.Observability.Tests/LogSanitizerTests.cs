using Muonroi.Core.Abstractions.Exceptions;
using FluentAssertions;
using Muonroi.Observability.Logging;
using Xunit;

namespace Muonroi.Observability.Tests;

public class LogSanitizerTests
{
    [Fact]
    public void Sanitize_Should_Redact_Denied_Fields()
    {
        var sanitizer = new LogSanitizer(["password", "secret"]);
        var data = new Dictionary<string, object?>
        {
            ["username"] = "admin",
            ["password"] = "hunter2",
            ["secret"] = "top-secret"
        };

        IDictionary<string, object?> result = sanitizer.Sanitize(data);

        result["username"].Should().Be("admin");
        result["password"].Should().Be("***");
        result["secret"].Should().Be("***");
    }

    [Fact]
    public void Sanitize_Should_Be_Case_Insensitive()
    {
        var sanitizer = new LogSanitizer(["Password"]);
        var data = new Dictionary<string, object?>
        {
            ["password"] = "hunter2"
        };

        IDictionary<string, object?> result = sanitizer.Sanitize(data);
        result["password"].Should().Be("***");
    }

    [Fact]
    public void Sanitize_With_Empty_DenyList_Should_Return_Data_Unchanged()
    {
        var sanitizer = new LogSanitizer([]);
        var data = new Dictionary<string, object?>
        {
            ["username"] = "admin",
            ["password"] = "hunter2"
        };

        IDictionary<string, object?> result = sanitizer.Sanitize(data);
        result["password"].Should().Be("hunter2");
    }

    [Fact]
    public void Sanitize_With_Null_DenyList_Should_Return_Data_Unchanged()
    {
        var sanitizer = new LogSanitizer(null);
        var data = new Dictionary<string, object?>
        {
            ["key"] = "value"
        };

        IDictionary<string, object?> result = sanitizer.Sanitize(data);
        result["key"].Should().Be("value");
    }

    [Fact]
    public void Sanitize_Should_Not_Affect_Fields_Not_In_DenyList()
    {
        var sanitizer = new LogSanitizer(["password"]);
        var data = new Dictionary<string, object?>
        {
            ["name"] = "test",
            ["email"] = "test@example.com"
        };

        IDictionary<string, object?> result = sanitizer.Sanitize(data);
        result["name"].Should().Be("test");
        result["email"].Should().Be("test@example.com");
    }
}

public class MLogEntryTests
{
    [Fact]
    public void Default_Identity_Should_Be_Valid_Guid()
    {
        var entry = new MLogEntry();
        Guid.TryParse(entry.Identity, out _).Should().BeTrue();
    }

    [Fact]
    public void Default_ExecDate_Should_Be_Close_To_UtcNow()
    {
        var entry = new MLogEntry();
        entry.ExecDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Properties_Should_Be_Settable()
    {
        var entry = new MLogEntry
        {
            TransactionId = "txn-1",
            SiteCode = "US-EAST",
            TenantId = "tenant-1",
            LogType = "API",
            Server = "server-01",
            Caller = "MyService",
            Client = "web-app",
            Username = "admin",
            Location = "/api/orders",
            Data = "{\"key\":\"value\"}",
            Elapsed = 150,
            ErrorCode = "E001",
            Message = "Request completed",
            LogTrace = "stack trace here",
            ApplicationInfo = "MyApp v1.0"
        };

        entry.TransactionId.Should().Be("txn-1");
        entry.SiteCode.Should().Be("US-EAST");
        entry.TenantId.Should().Be("tenant-1");
        entry.LogType.Should().Be("API");
        entry.Server.Should().Be("server-01");
        entry.Caller.Should().Be("MyService");
        entry.Client.Should().Be("web-app");
        entry.Username.Should().Be("admin");
        entry.Location.Should().Be("/api/orders");
        entry.Data.Should().Be("{\"key\":\"value\"}");
        entry.Elapsed.Should().Be(150);
        entry.ErrorCode.Should().Be("E001");
        entry.Message.Should().Be("Request completed");
        entry.LogTrace.Should().Be("stack trace here");
        entry.ApplicationInfo.Should().Be("MyApp v1.0");
    }

    [Fact]
    public void Two_Instances_Should_Have_Different_Identities()
    {
        var entry1 = new MLogEntry();
        var entry2 = new MLogEntry();
        entry1.Identity.Should().NotBe(entry2.Identity);
    }
}

public class BootstrapMethodTests
{
    [Fact]
    public void BootstrapMethod_Should_Have_Three_Values()
    {
        Enum.GetValues<BootstrapMethod>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(BootstrapMethod.Silent)]
    [InlineData(BootstrapMethod.Failure)]
    [InlineData(BootstrapMethod.None)]
    public void BootstrapMethod_Value_Should_Be_Defined(BootstrapMethod value)
    {
        Enum.IsDefined(value).Should().BeTrue();
    }
}

public class TenantIdEnricherTests
{
    [Fact]
    public void Constructor_Should_Throw_On_Null_Accessor()
    {
        Action act = () => new TenantIdEnricher(null!);
        act.Should().Throw<MArgumentException>();
    }
}
