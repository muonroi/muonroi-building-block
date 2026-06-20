using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Security;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.DataSample;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Governance.License;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests.Ecosystem;

/// <summary>
/// Integration tests that prove all 4 security fixes (SEC-01..04) work correctly across
/// three ecosystem modes: standalone (0 capabilities), partial (subset), and full (all 4 capabilities).
///
/// Purpose: Validates the "better together" pattern end-to-end — core functionality always works
/// without ecosystem packages, and enhancements activate only when the corresponding capability
/// is registered.
/// </summary>
[Collection("EcosystemSecurity")]
public class EcosystemSecurityIntegrationTests : IDisposable
{
    // Track temp files to clean up after tests
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (string path in _tempFiles)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // ----- Helpers -----

    /// <summary>
    /// Creates an MEcosystemRegistry with the specified capabilities pre-registered.
    /// </summary>
    private static MEcosystemRegistry CreateRegistryWithCapabilities(params MCapability[] caps)
    {
        MEcosystemRegistry registry = new();
        foreach (MCapability cap in caps)
        {
            registry.Register(cap);
        }
        return registry;
    }

    /// <summary>
    /// Creates a temp file with the given content and returns its absolute path.
    /// </summary>
    private string CreateTempFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    /// <summary>
    /// Captures Console.Out output during the given action.
    /// </summary>
    private static string CaptureConsoleOutput(Action action)
    {
        TextWriter original = Console.Out;
        using StringWriter writer = new();
        Console.SetOut(writer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }
        return writer.ToString();
    }

    /// <summary>
    /// Creates an EcosystemTestDbContext backed by an isolated in-memory database.
    /// </summary>
    private static EcosystemTestDbContext CreateTestDbContext()
    {
        DbContextOptions<EcosystemTestDbContext> options =
            new DbContextOptionsBuilder<EcosystemTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        IMDateTimeService dateTimeSvc = Mock.Of<IMDateTimeService>(s => s.UtcNow() == DateTime.UtcNow);
        return new EcosystemTestDbContext(options, Mock.Of<IMediator>(), Mock.Of<ILicenseGuard>(), dateTimeSvc);
    }

    // ========================================================================================
    // STANDALONE MODE (registry = null or Active = None) — SEC-01..04
    // ========================================================================================

    /// <summary>
    /// SEC-02 standalone: BackgroundJobHandler with registered provider and NO ecosystem registry
    /// dispatches without error, no ecosystem log emitted.
    /// </summary>
    [Fact]
    public void Sec02_Standalone_BackgroundJobHandler_Dispatches_WithoutError()
    {
        // Arrange — register a dummy provider, build config pointing to it
        const JobType testJobType = JobType.Hangfire;
        BackgroundJobHandler.RegisterProvider(testJobType, (svc, _) => svc);

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobs:JobType"] = testJobType.ToString()
            })
            .Build();

        ServiceCollection services = new();
        // No IMEcosystemRegistry registered → standalone mode

        string capturedOutput = CaptureConsoleOutput(() =>
        {
            services.AddBackgroundJobs(config);
        });

        // Assert: no exception, no ecosystem log
        capturedOutput.Should().NotContain("[Ecosystem]",
            "no ecosystem capability is registered, so no ecosystem log should be emitted");
    }

    /// <summary>
    /// SEC-03 standalone: MRuleContextJsonRegistry with registered type, NO registry
    /// — DeserializeContext succeeds and returns typed object.
    /// </summary>
    [Fact]
    public void Sec03_Standalone_MRuleContextJsonRegistry_Deserializes_KnownType()
    {
        // Arrange
        MRuleContextJsonRegistry registry = new(registry: null);
        registry.AddRuleContext<TestRuleContext>("TestContext");

        string json = "{\"Name\":\"hello\",\"Value\":42}";

        // Act
        object? result = registry.DeserializeContext("TestContext", json);

        // Assert
        result.Should().BeOfType<TestRuleContext>();
        TestRuleContext ctx = result.Should().BeAssignableTo<TestRuleContext>().Subject;
        ctx.Name.Should().Be("hello");
        ctx.Value.Should().Be(42);
    }

    /// <summary>
    /// SEC-03 standalone: MRuleContextJsonRegistry DeserializeContext with unknown type
    /// throws MConfigurationException.
    /// </summary>
    [Fact]
    public void Sec03_Standalone_MRuleContextJsonRegistry_Throws_ForUnknownType()
    {
        // Arrange
        MRuleContextJsonRegistry registry = new(registry: null);

        // Act
        Action act = () => registry.DeserializeContext("UnknownType", "{}");

        // Assert
        act.Should().Throw<MConfigurationException>();
    }

    /// <summary>
    /// SEC-04 standalone: MSecureFileReader.ReadKeyFile with valid temp file, registry=null
    /// returns content successfully, no exception.
    /// </summary>
    [Fact]
    public void Sec04_Standalone_MSecureFileReader_ReadsFile_Successfully()
    {
        // Arrange
        string expectedContent = "test-key-content-12345";
        string tempPath = CreateTempFile(expectedContent);

        // Act
        string content = MSecureFileReader.ReadKeyFile(tempPath, registry: null);

        // Assert
        content.Should().Be(expectedContent);
    }

    /// <summary>
    /// SEC-04 standalone: MSecureFileReader.ReadKeyFile with path traversal attempt, registry=null
    /// throws MConfigurationException.
    /// </summary>
    [Fact]
    public void Sec04_Standalone_MSecureFileReader_Throws_OnPathTraversal()
    {
        // Act
        Action act = () => MSecureFileReader.ReadKeyFile("../malicious", registry: null);

        // Assert
        act.Should().Throw<MConfigurationException>();
    }

    /// <summary>
    /// SEC-01 standalone: HostRoleAndUserCreator with null registry
    /// — Create() does not throw, user seeded with ShouldChangePasswordOnNextLogin = true.
    /// </summary>
    [Fact]
    public void Sec01_Standalone_HostRoleAndUserCreator_Create_DoesNotThrow_WithNullRegistry()
    {
        // Arrange — use in-memory EF Core context via helper
        using EcosystemTestDbContext ctx = CreateTestDbContext();

        IMDateTimeService dateTimeSvc = Mock.Of<IMDateTimeService>(s => s.UtcNow() == DateTime.UtcNow);
        HostRoleAndUserCreator<EcosystemTestDbContext> creator = new(ctx, dateTimeSvc, registry: null);

        // Act — no exception expected
        Action act = () => creator.Create();
        act.Should().NotThrow();

        // Assert user was seeded with ShouldChangePasswordOnNextLogin = true
        MUser? seededUser = ctx.Users.IgnoreQueryFilters()
            .FirstOrDefault(u => u.UserName == StaticRoleAndUserNames.Host.AdminUserName);
        seededUser.Should().NotBeNull("admin user should have been seeded");
        if (seededUser is not null)
        {
            seededUser.ShouldChangePasswordOnNextLogin.Should().BeTrue(
                "admin user must require password change on first login (D-02)");
        }
    }

    // ========================================================================================
    // FULL ECOSYSTEM (all 4 capabilities: Logging + RuleEngine + MultiTenant + Auth) — SEC-01..04
    // ========================================================================================

    /// <summary>
    /// SEC-01 full ecosystem: HostRoleAndUserCreator with all 4 caps
    /// — Console output contains "[Ecosystem] WARN" after seeding.
    /// </summary>
    [Fact]
    public void Sec01_FullEcosystem_HostRoleAndUserCreator_EmitsLoggingWarn()
    {
        // Arrange
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.RuleEngine, MCapability.MultiTenant, MCapability.Auth);

        using EcosystemTestDbContext ctx = CreateTestDbContext();
        IMDateTimeService dateTimeSvc = Mock.Of<IMDateTimeService>(s => s.UtcNow() == DateTime.UtcNow);

        // Set a valid env password (8+ chars) so Auth policy passes
        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", "Test1234!");

        try
        {
            HostRoleAndUserCreator<EcosystemTestDbContext> creator = new(ctx, dateTimeSvc, registry);

            string capturedOutput = CaptureConsoleOutput(() => creator.Create());

            // Assert: Logging warn emitted
            capturedOutput.Should().Contain("[Ecosystem] WARN: Default admin seeded",
                "Logging capability is active — seeding warn must be emitted");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        }
    }

    /// <summary>
    /// SEC-01 full ecosystem + Auth: when password is too short, throw MConfigurationException.
    /// </summary>
    [Fact]
    public void Sec01_FullEcosystem_HostRoleAndUserCreator_ThrowsOnShortPassword()
    {
        // Arrange
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.Auth);

        using EcosystemTestDbContext ctx = CreateTestDbContext();
        IMDateTimeService dateTimeSvc = Mock.Of<IMDateTimeService>(s => s.UtcNow() == DateTime.UtcNow);

        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", "short");

        try
        {
            HostRoleAndUserCreator<EcosystemTestDbContext> creator = new(ctx, dateTimeSvc, registry);

            // Act
            Action act = () => creator.Create();

            // Assert: Auth policy rejects short password
            act.Should().Throw<MConfigurationException>(
                "Auth capability enforces 8+ char password minimum");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        }
    }

    /// <summary>
    /// SEC-02 full ecosystem: BackgroundJobHandler with all 4 caps
    /// — Console output contains "[Ecosystem] BackgroundJobs: Dispatching to".
    /// </summary>
    [Fact]
    public void Sec02_FullEcosystem_BackgroundJobHandler_EmitsDispatchLog()
    {
        // Arrange
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.RuleEngine, MCapability.MultiTenant, MCapability.Auth);

        const JobType testJobType = JobType.Hangfire;
        BackgroundJobHandler.RegisterProvider(testJobType, (svc, _) => svc);

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobs:JobType"] = testJobType.ToString()
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<IMEcosystemRegistry>(registry);

        // Act
        string capturedOutput = CaptureConsoleOutput(() =>
        {
            services.AddBackgroundJobs(config);
        });

        // Assert: dispatch log present
        capturedOutput.Should().Contain("[Ecosystem] BackgroundJobs: Dispatching to",
            "Logging capability is active — dispatch message must be emitted");
    }

    /// <summary>
    /// SEC-03 full ecosystem: MRuleContextJsonRegistry with all 4 caps
    /// — Console output contains audit log on successful deserialization.
    /// </summary>
    [Fact]
    public void Sec03_FullEcosystem_MRuleContextJsonRegistry_EmitsAuditLog()
    {
        // Arrange
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.RuleEngine, MCapability.MultiTenant, MCapability.Auth);

        MRuleContextJsonRegistry contextRegistry = new(registry);
        contextRegistry.AddRuleContext<TestRuleContext>("TestContext");

        string json = "{\"Name\":\"audit\",\"Value\":1}";

        // Act
        string capturedOutput = CaptureConsoleOutput(() =>
        {
            contextRegistry.DeserializeContext("TestContext", json);
        });

        // Assert: audit log present
        capturedOutput.Should().Contain("[Ecosystem] AUDIT:",
            "Logging capability is active — deserialization audit must be logged");
        capturedOutput.Should().Contain("ALLOWED",
            "successful deserialization should log ALLOWED");
    }

    /// <summary>
    /// SEC-04 full ecosystem: MSecureFileReader.ReadKeyFile with all 4 caps
    /// — Console output contains audit log for file access.
    /// </summary>
    [Fact]
    public void Sec04_FullEcosystem_MSecureFileReader_EmitsAuditLog()
    {
        // Arrange
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.RuleEngine, MCapability.MultiTenant, MCapability.Auth);

        string tempPath = CreateTempFile("key-material");

        // Act
        string capturedOutput = CaptureConsoleOutput(() =>
        {
            MSecureFileReader.ReadKeyFile(tempPath, registry: registry);
        });

        // Assert: audit log present
        capturedOutput.Should().Contain("[Ecosystem] AUDIT: Key file accessed:",
            "Logging capability is active — file access must be audited");
    }

    // ========================================================================================
    // PARTIAL ECOSYSTEM (Logging + Auth only) — SEC-01, SEC-03, SEC-04
    // ========================================================================================

    /// <summary>
    /// SEC-01 partial (Logging+Auth): Logging warn present AND Auth policy active.
    /// </summary>
    [Fact]
    public void Sec01_PartialEcosystem_LoggingAndAuth_BothEnhancementsActive()
    {
        // Arrange: Logging + Auth only (no RuleEngine, no MultiTenant)
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.Auth);

        // Verify only the intended capabilities are active
        registry.Has(MCapability.Logging).Should().BeTrue();
        registry.Has(MCapability.Auth).Should().BeTrue();
        registry.Has(MCapability.RuleEngine).Should().BeFalse();
        registry.Has(MCapability.MultiTenant).Should().BeFalse();

        using EcosystemTestDbContext ctx = CreateTestDbContext();
        IMDateTimeService dateTimeSvc = Mock.Of<IMDateTimeService>(s => s.UtcNow() == DateTime.UtcNow);

        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", "ValidPass99!");

        try
        {
            HostRoleAndUserCreator<EcosystemTestDbContext> creator = new(ctx, dateTimeSvc, registry);

            string capturedOutput = CaptureConsoleOutput(() => creator.Create());

            // Assert: Logging warn active
            capturedOutput.Should().Contain("[Ecosystem] WARN: Default admin seeded",
                "Logging capability is active in partial mode");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        }
    }

    /// <summary>
    /// SEC-03 partial (Logging only, no MultiTenant):
    /// Logging audit active, tenant whitelist NOT initialized — type accessible for any tenant.
    /// </summary>
    [Fact]
    public void Sec03_PartialEcosystem_LoggingOnly_AuditActive_NoTenantWhitelist()
    {
        // Arrange: Logging only
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(MCapability.Logging);

        // Verify MultiTenant is NOT active
        registry.Has(MCapability.MultiTenant).Should().BeFalse(
            "only Logging is registered in partial mode");

        MRuleContextJsonRegistry contextRegistry = new(registry);
        contextRegistry.AddRuleContext<TestRuleContext>("TestContext");

        string json = "{\"Name\":\"partial\",\"Value\":99}";
        object? deserializedResult = null;

        // Act
        string capturedOutput = CaptureConsoleOutput(() =>
        {
            // With MultiTenant absent, passing a tenantId has no effect — type is accessible
            deserializedResult = contextRegistry.DeserializeContext("TestContext", json, tenantId: "tenant-A");
        });

        // Assert deserialization succeeded
        deserializedResult.Should().BeOfType<TestRuleContext>();

        // Assert: Logging audit active
        capturedOutput.Should().Contain("[Ecosystem] AUDIT:",
            "Logging capability is active — deserialization audit must be logged");

        // Assert: no tenant whitelist denial (MultiTenant not active)
        capturedOutput.Should().NotContain("whitelist",
            "tenant whitelist should not be active when MultiTenant capability is absent");
    }

    /// <summary>
    /// SEC-04 partial (Logging+Auth, no MultiTenant):
    /// Logging audit active, file content returned, MultiTenant scope validation NOT active.
    /// </summary>
    [Fact]
    public void Sec04_PartialEcosystem_LoggingAndAuth_AuditActive_NoMultiTenantScope()
    {
        // Arrange: Logging + Auth (no MultiTenant)
        MEcosystemRegistry registry = CreateRegistryWithCapabilities(
            MCapability.Logging, MCapability.Auth);

        // Verify MultiTenant is NOT active
        registry.Has(MCapability.MultiTenant).Should().BeFalse(
            "MultiTenant is not registered in partial mode");

        string tempPath = CreateTempFile("partial-key-content");
        string? readContent = null;

        // Act
        string capturedOutput = CaptureConsoleOutput(() =>
        {
            readContent = MSecureFileReader.ReadKeyFile(tempPath, registry: registry);
        });

        // Assert file was read
        readContent.Should().Be("partial-key-content");

        // Assert: Logging audit active
        capturedOutput.Should().Contain("[Ecosystem] AUDIT: Key file accessed:",
            "Logging capability is active — file access must be audited");
    }
}

// ========================================================================================
// Test support types
// ========================================================================================

/// <summary>
/// Test rule context type for use in MRuleContextJsonRegistry tests.
/// </summary>
public sealed class TestRuleContext
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>
/// Minimal MDbContext subclass used by ecosystem security integration tests.
/// </summary>
internal sealed class EcosystemTestDbContext(
    DbContextOptions<EcosystemTestDbContext> options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMDateTimeService? dateTimeService = null)
    : MDbContext(options, mediator, licenseGuard, null, dateTimeService);
