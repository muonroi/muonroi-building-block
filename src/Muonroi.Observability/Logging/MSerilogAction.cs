using Serilog.Formatting.Elasticsearch;
using Serilog.Sinks.Elasticsearch;

namespace Muonroi.Observability.Logging;

/// <summary>
/// Configures Serilog sinks and enrichers for the host.
/// </summary>
public static class MSerilogAction
{
    /// <summary>
    /// Applies Serilog configuration and optional console logging.
    /// </summary>
    /// <param name="context">Host builder context.</param>
    /// <param name="services">Service provider for enrichers.</param>
    /// <param name="loggerConfiguration">Logger configuration to update.</param>
    /// <param name="useConsole">Whether to enable console logging.</param>
    public static void Configure(
        HostBuilderContext context,
        IServiceProvider services,
        LoggerConfiguration loggerConfiguration,
        bool useConsole = true)
    {
        _ = loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.With(services.GetRequiredService<TenantIdEnricher>());

        AddElasticsearchSink(context.Configuration, loggerConfiguration);
        AddFileSink(context.Configuration, loggerConfiguration);

        if (useConsole)
        {
            _ = loggerConfiguration.WriteTo.Console();
        }
    }

    private static void AddElasticsearchSink(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        IConfigurationSection esSection = configuration.GetSection("Serilog:Elasticsearch");
        if (!esSection.Exists())
        {
            return;
        }

        string[]? nodes = esSection.GetSection("nodes").Get<string[]>();
        if (nodes == null || nodes.Length == 0)
        {
            string? uri = esSection["Uri"];
            if (!string.IsNullOrWhiteSpace(uri))
            {
                nodes = [uri];
            }
        }

        if (nodes == null || nodes.Length == 0)
        {
            return;
        }

        ElasticsearchSinkOptions options = new(nodes.Select(n => new Uri(n)))
        {
            AutoRegisterTemplate = true,
            DetectElasticsearchVersion = true
        };

        if (bool.TryParse(esSection["useSniffing"], out bool useSniffing))
        {
            // Note: Modern Elasticsearch sink might not have this property directly
        }

        string? dataStream = esSection["dataStream"];
        if (!string.IsNullOrWhiteSpace(dataStream))
        {
            // Set data stream options if supported
        }

        _ = loggerConfiguration.WriteTo.Elasticsearch(options);
    }

    private static void AddFileSink(IConfiguration configuration, LoggerConfiguration loggerConfiguration)
    {
        IConfigurationSection fileSection = configuration.GetSection("Serilog:File");
        string? path = fileSection["Path"];
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Invalid log file path.", ex);
        }

        loggerConfiguration.WriteTo.File(
            new ElasticsearchJsonFormatter(),
            path,
            rollingInterval: RollingInterval.Infinite);
    }
}
