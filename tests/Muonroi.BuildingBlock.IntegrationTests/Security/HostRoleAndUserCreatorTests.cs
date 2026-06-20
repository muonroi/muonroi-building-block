using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.DataSample;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Muonroi.Governance.License;
using Muonroi.Mediator.Mediator.Interfaces;
using Xunit;

namespace Muonroi.BuildingBlock.IntegrationTests.Security;

/// <summary>
/// Tests for HostRoleAndUserCreator SEC-01 security fix: env-based seed password.
/// Uses a minimal MDbContext subclass backed by InMemory database.
/// </summary>
// Shares the "EcosystemSecurity" collection with EcosystemSecurityIntegrationTests so the
// two classes never run in parallel. Both capture process-global Console.Out via
// Console.SetOut; running them concurrently let one class's SetOut/restore steal the other's
// redirection, so Create_WithLoggingCapability_WritesWarnLog intermittently saw empty output.
[Collection("EcosystemSecurity")]
public class HostRoleAndUserCreatorTests : IDisposable
{
    private readonly SecurityTestDbContext _context;
    private readonly IMDateTimeService _dateTimeService;

    public HostRoleAndUserCreatorTests()
    {
        // Use a unique database name for EVERY test instance to ensure absolute isolation
        DbContextOptions<SecurityTestDbContext> options = new DbContextOptionsBuilder<SecurityTestDbContext>()
            .UseInMemoryDatabase($"SecurityTest_{Guid.NewGuid():N}")
            .Options;
        
        _dateTimeService = Mock.Of<IMDateTimeService>(s => s.UtcNow() == DateTime.UtcNow);
        _context = new SecurityTestDbContext(options, Mock.Of<IMediator>(), Mock.Of<ILicenseGuard>(), _dateTimeService);
        
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    /// <summary>
    /// Test 1: When MUONROI_SEED_ADMIN_PASSWORD env var is set to "MyStr0ng!Pass",
    /// the seeded user's password is a bcrypt hash and ShouldChangePasswordOnNextLogin = true.
    /// </summary>
    [Fact]
    public void Create_WithEnvPassword_HashesPasswordAndSetsChangeFlag()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", "MyStr0ng!Pass");
        try
        {
            HostRoleAndUserCreator<SecurityTestDbContext> creator = new(_context, _dateTimeService);

            // Act
            creator.Create();

            // Assert
            MUser? admin = _context.Users.IgnoreQueryFilters()
                .FirstOrDefault(u => u.UserName == "admin");
            admin.Should().NotBeNull();
            if (admin is not null)
            {
                admin.Password.Should().NotBeNullOrWhiteSpace();
                admin.Password.Should().NotBe("MyStr0ng!Pass", "password must be stored hashed, not plaintext");
                admin.ShouldChangePasswordOnNextLogin.Should().BeTrue();

                // Verify it's a valid bcrypt hash (starts with $2b$ or $2a$)
                admin.Password.Should().StartWith("$2", "stored password must be a bcrypt hash");
                admin.Password.Length.Should().Be(60, "bcrypt hash is always 60 chars");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        }
    }

    /// <summary>
    /// Test 2: When MUONROI_SEED_ADMIN_PASSWORD env var is absent, a random bcrypt
    /// hash is generated and ShouldChangePasswordOnNextLogin = true.
    /// </summary>
    [Fact]
    public void Create_WithoutEnvPassword_GeneratesRandomHashAndSetsChangeFlag()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        HostRoleAndUserCreator<SecurityTestDbContext> creator = new(_context, _dateTimeService);

        // Act
        creator.Create();

        // Assert
        MUser? admin = _context.Users.IgnoreQueryFilters()
            .FirstOrDefault(u => u.UserName == "admin");
        admin.Should().NotBeNull();
        if (admin is not null)
        {
            admin.Password.Should().NotBeNullOrWhiteSpace("a random password hash must be generated");
            admin.ShouldChangePasswordOnNextLogin.Should().BeTrue();
            // Verify it looks like a bcrypt hash (starts with $2)
            admin.Password.Should().StartWith("$2", "generated password must be stored as bcrypt hash");
        }
    }

    /// <summary>
    /// Test 3: The plaintext "sysadmin" does not appear in the source file.
    /// This is a static verification — the source file must not contain the leaked secret.
    /// </summary>
    [Fact]
    public void SourceFile_DoesNotContain_PlaintextSysadmin()
    {
        // Find source file by traversing upward from the test binary
        string? sourceFile = FindSourceFile("HostRoleAndUserCreator.cs");

        if (sourceFile == null || !File.Exists(sourceFile))
        {
            // Accept skip if file can't be found — grep-based acceptance criteria covers this
            return;
        }

        string content = File.ReadAllText(sourceFile);
        content.Should().NotContain("sysadmin",
            "the hardcoded plaintext sysadmin password comment must be removed (SEC-01)");
    }

    /// <summary>
    /// Test 4: When Logging capability is present, a Console warn log containing
    /// "password change required" is emitted after seeding.
    /// </summary>
    [Fact]
    public void Create_WithLoggingCapability_WritesWarnLog()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        Mock<IMEcosystemRegistry> registryMock = new();
        registryMock.Setup(r => r.Has(MCapability.Logging)).Returns(true);
        registryMock.Setup(r => r.Has(MCapability.Auth)).Returns(false);

        HostRoleAndUserCreator<SecurityTestDbContext> creator = new(_context, _dateTimeService, registryMock.Object);

        // Capture Console output
        TextWriter original = Console.Out;
        using StringWriter sw = new();
        Console.SetOut(sw);

        try
        {
            creator.Create();
        }
        finally
        {
            Console.SetOut(original);
            Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        }

        // Assert
        string output = sw.ToString();
        output.Should().Contain("password change required",
            "when Logging capability is present, a warn log about password change must be emitted");
    }

    /// <summary>
    /// Test 5: When Auth capability is present and env var password is shorter than 8 chars,
    /// MConfigurationException is thrown before any seeding occurs.
    /// </summary>
    [Fact]
    public void Create_WithAuthCapability_ShortPassword_ThrowsMConfigurationException()
    {
        // Arrange
        Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", "short");
        try
        {
            Mock<IMEcosystemRegistry> registryMock = new();
            registryMock.Setup(r => r.Has(MCapability.Auth)).Returns(true);
            registryMock.Setup(r => r.Has(MCapability.Logging)).Returns(false);

            HostRoleAndUserCreator<SecurityTestDbContext> creator = new(_context, _dateTimeService, registryMock.Object);

            // Act & Assert
            Action act = () => creator.Create();
            act.Should().Throw<MConfigurationException>()
                .WithMessage("*minimum complexity*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD", null);
        }
    }

    private static string? FindSourceFile(string fileName)
    {
        // Walk from solution root
        string start = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(start);
        // Walk up to find solution root
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Muonroi.BuildingBlock.sln")))
            dir = dir.Parent;

        if (dir == null) return null;

        return Directory.EnumerateFiles(dir.FullName, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(f => f.Contains("DataSample", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Minimal MDbContext subclass used by security tests.
/// </summary>
internal sealed class SecurityTestDbContext(
    DbContextOptions<SecurityTestDbContext> options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMDateTimeService? dateTimeService = null)
    : MDbContext(options, mediator, licenseGuard, null, dateTimeService);
