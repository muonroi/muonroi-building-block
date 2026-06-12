using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Muonroi.Pdf.Extensions;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// SC5 / DI-03: proves <c>PdfConfigs</c> validation runs at host startup via
/// <c>ValidateOnStart()</c>. A non-positive limit fails fast before any render.
/// </summary>
public sealed class ConfigValidationTests
{
    private static IHost BuildHost(IConfiguration config)
    {
        // Feed our limits into the host's own IConfiguration so BindConfiguration (which resolves
        // IConfiguration from DI) sees them — the host pre-registers IConfiguration, so a
        // TryAddSingleton of a separate instance would be ignored.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(config);
        builder.Services.AddTestDoubles();
        builder.Services.AddPdf(builder.Configuration);
        return builder.Build();
    }

    [Fact]
    public async Task AddPdf_MaxPagesZero_ThrowsAtStartup()
    {
        IConfiguration config = PdfServiceTestHarness.ValidConfig(new Dictionary<string, string?>
        {
            ["PdfConfigs:Limits:MaxPages"] = "0",
        });

        using IHost host = BuildHost(config);

        // ValidateOnStart() runs the eager validator during host start, failing fast BEFORE any
        // IMPdfService is resolved or a render is attempted.
        Func<Task> act = async () => await host.StartAsync();

        await act.Should().ThrowAsync<OptionsValidationException>()
            .WithMessage("*limits must be positive*");
    }

    [Fact]
    public async Task AddPdf_ValidLimits_StartsSuccessfully()
    {
        IConfiguration config = PdfServiceTestHarness.ValidConfig();

        using IHost host = BuildHost(config);

        Func<Task> act = async () => await host.StartAsync();

        await act.Should().NotThrowAsync();
        await host.StopAsync();
    }
}
