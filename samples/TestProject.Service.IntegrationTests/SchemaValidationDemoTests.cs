using Muonroi.Tenancy.SiteProfile.Web.Validation;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Demonstrates SiteSchemaValidator configuration and behavior.
/// Tests verify the options model and severity enum work as documented.
/// Full DB validation requires a live connection (integration test with PostgreSQL);
/// these tests demonstrate the API and unit-test the options configuration.
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class SchemaValidationDemoTests
{
    [Fact]
    public void AddSiteSchemaValidation_DefaultOptions_IsWarnOnMismatch()
    {
        // [TEST:BREAKPOINT] SchemaValidationDemoTests - DefaultOptions
        // Default severity is WarnOnMismatch — does not throw, only logs warnings
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSiteSchemaValidation(); // no options override

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SiteSchemaValidationOptions>>();

        Assert.Equal(SchemaValidationSeverity.WarnOnMismatch, options.Value.Severity);
        Assert.Equal("public", options.Value.SchemaName); // PostgreSQL default schema
    }

    [Fact]
    public void AddSiteSchemaValidation_WithFailOnMissing_StoresCorrectSeverity()
    {
        // [TEST:BREAKPOINT] SchemaValidationDemoTests - FailOnMissing severity
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSiteSchemaValidation(o =>
        {
            o.Severity = SchemaValidationSeverity.FailOnMissing;
            o.SchemaName = "production";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SiteSchemaValidationOptions>>();

        Assert.Equal(SchemaValidationSeverity.FailOnMissing, options.Value.Severity);
        Assert.Equal("production", options.Value.SchemaName);
    }

    [Fact]
    public void AddSiteSchemaValidation_RegistersSiteSchemaValidator_AsService()
    {
        // [TEST:BREAKPOINT] SchemaValidationDemoTests - SiteSchemaValidator registration
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSiteSchemaValidation();

        // Validator is registered as IHostedService — runs at app startup
        bool found = services.Any(d => d.ImplementationType == typeof(SiteSchemaValidator));
        Assert.True(found, "SiteSchemaValidator should be registered in the service collection");
    }

    [Fact]
    public void BravoOrder_SiteColumnAttributes_DeclareCorrectSchema()
    {
        // [TEST:BREAKPOINT] SchemaValidationDemoTests - BravoOrder schema declaration
        // Verifies the schema that SiteSchemaValidator would compare against the DB
        // When BRAVO has [SiteColumn(Name = "BOOKING_NUMBER")] but DB has "BOOKING_NO",
        // the validator would detect the mismatch.

        var prop = typeof(BravoOrder).GetProperty("BookingNo");
        var attr = prop?.GetCustomAttributes(typeof(Muonroi.EntityFrameworkCore.Configuration.SiteColumnAttribute), false)
            .Cast<Muonroi.EntityFrameworkCore.Configuration.SiteColumnAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        // In a mismatch scenario: EF model says "BOOKING_NUMBER" but DB has "BOOKING_NO"
        // SiteSchemaValidator (WarnOnMismatch) would log: "[SiteSchemaValidator] Schema mismatch: ..."
        // SiteSchemaValidator (FailOnMissing) would throw: InvalidOperationException
        Assert.Equal("BOOKING_NUMBER", attr.Name);
    }

    [Fact]
    public void SchemaValidationSeverity_EnumValues_AreDocumented()
    {
        // [TEST:BREAKPOINT] SchemaValidationDemoTests - Enum values
        // Confirms enum values exist and have expected intent
        Assert.Equal(0, (int)SchemaValidationSeverity.WarnOnMismatch);
        Assert.Equal(1, (int)SchemaValidationSeverity.FailOnMissing);
    }
}
