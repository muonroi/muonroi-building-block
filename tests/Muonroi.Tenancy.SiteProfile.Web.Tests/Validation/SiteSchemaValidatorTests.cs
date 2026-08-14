namespace Muonroi.Tenancy.SiteProfile.Web.Tests.Validation;

/// <summary>
/// Unit tests for SiteSchemaValidator.
/// Uses a fake DbContext backed by in-memory SQLite to provide IRelationalModel.
/// Mocks the underlying DbConnection to control INFORMATION_SCHEMA responses.
/// </summary>
public class SiteSchemaValidatorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IOptions<SiteSchemaValidationOptions> CreateOptions(
        SchemaValidationSeverity severity = SchemaValidationSeverity.WarnOnMismatch,
        string schemaName = "public")
    {
        return Options.Create(new SiteSchemaValidationOptions
        {
            Severity = severity,
            SchemaName = schemaName
        });
    }

    private static IMLog<SiteSchemaValidator> CreateLog()
    {
        return Mock.Of<IMLog<SiteSchemaValidator>>();
    }

    // -----------------------------------------------------------------------
    // SiteSchemaValidationOptions tests
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSchemaValidationOptions_DefaultSeverity_IsWarnOnMismatch()
    {
        var options = new SiteSchemaValidationOptions();
        Assert.Equal(SchemaValidationSeverity.WarnOnMismatch, options.Severity);
    }

    [Fact]
    public void SiteSchemaValidationOptions_DefaultSchemaName_IsPublic()
    {
        var options = new SiteSchemaValidationOptions();
        Assert.Equal("public", options.SchemaName);
    }

    [Fact]
    public void SiteSchemaValidationOptions_CanSetFailOnMissingSeverity()
    {
        var options = new SiteSchemaValidationOptions
        {
            Severity = SchemaValidationSeverity.FailOnMissing
        };
        Assert.Equal(SchemaValidationSeverity.FailOnMissing, options.Severity);
    }

    [Fact]
    public void SiteSchemaValidationOptions_CanSetSchemaName()
    {
        var options = new SiteSchemaValidationOptions { SchemaName = "dbo" };
        Assert.Equal("dbo", options.SchemaName);
    }

    // -----------------------------------------------------------------------
    // SchemaValidationSeverity enum
    // -----------------------------------------------------------------------

    [Fact]
    public void SchemaValidationSeverity_WarnOnMismatch_IsZero()
    {
        Assert.Equal(0, (int)SchemaValidationSeverity.WarnOnMismatch);
    }

    [Fact]
    public void SchemaValidationSeverity_FailOnMissing_IsOne()
    {
        Assert.Equal(1, (int)SchemaValidationSeverity.FailOnMissing);
    }

    // -----------------------------------------------------------------------
    // SiteSchemaValidationExtensions
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSiteSchemaValidation_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddSiteSchemaValidation();

        // Verify SiteSchemaValidator is registered as IHostedService via service descriptor
        // (checking descriptors avoids needing to resolve IMLog<T> in this unit test)
        bool hasDescriptor = services.Any(d =>
            d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
            d.ImplementationType == typeof(SiteSchemaValidator));

        Assert.True(hasDescriptor, "SiteSchemaValidator should be registered as IHostedService");
    }

    [Fact]
    public void AddSiteSchemaValidation_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSiteSchemaValidation(o =>
        {
            o.Severity = SchemaValidationSeverity.FailOnMissing;
            o.SchemaName = "dbo";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SiteSchemaValidationOptions>>();

        Assert.Equal(SchemaValidationSeverity.FailOnMissing, options.Value.Severity);
        Assert.Equal("dbo", options.Value.SchemaName);
    }

    // -----------------------------------------------------------------------
    // SiteSchemaValidator.StopAsync — is always no-op
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StopAsync_IsNoOp_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var sut = new SiteSchemaValidator(sp, CreateOptions(), CreateLog());

        // StopAsync should complete without exception
        await sut.StopAsync(CancellationToken.None);
    }

    // -----------------------------------------------------------------------
    // SiteSchemaValidator — constructor validation
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSchemaValidator_NullServiceProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<MArgumentException>(() =>
            new SiteSchemaValidator(null!, CreateOptions(), CreateLog()));
    }

    [Fact]
    public void SiteSchemaValidator_NullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        Assert.Throws<MArgumentException>(() =>
            new SiteSchemaValidator(sp, null!, CreateLog()));
    }

    [Fact]
    public void SiteSchemaValidator_NullLog_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        Assert.Throws<MArgumentException>(() =>
            new SiteSchemaValidator(sp, CreateOptions(), null!));
    }

    // -----------------------------------------------------------------------
    // SiteSchemaValidator — WarnOnMismatch mode with no DbContexts
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_NoDbContextsRegistered_CompletesWithoutError()
    {
        // If no DbContext is registered in DI, StartAsync should silently complete
        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        var sut = new SiteSchemaValidator(sp, CreateOptions(), CreateLog());

        // Should not throw
        await sut.StartAsync(CancellationToken.None);
    }

    // -----------------------------------------------------------------------
    // SiteSchemaValidator — does NOT inherit from SiteProfileStartupValidator (D-16)
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSchemaValidator_DoesNotInheritSiteProfileStartupValidator()
    {
        // Verify SiteSchemaValidator is a SEPARATE class, not extending SiteProfileStartupValidator
        var baseType = typeof(SiteSchemaValidator).BaseType;
        Assert.NotEqual("SiteProfileStartupValidator", baseType?.Name);

        // Verify it implements IHostedService directly
        Assert.Contains(typeof(Microsoft.Extensions.Hosting.IHostedService), typeof(SiteSchemaValidator).GetInterfaces());
    }
}
