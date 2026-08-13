using Muonroi.Core.Abstractions.Exceptions;
using Serilog.Sinks.OpenTelemetry;

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
            .Enrich.With(services.GetRequiredService<TenantIdEnricher>())
            .Enrich.With(services.GetRequiredService<ServiceContextEnricher>());

        loggerConfiguration.WriteTo.Async(a =>
        {
            AddOpenTelemetrySink(context.Configuration, a);
            AddFileSink(context.Configuration, a);

            if (useConsole)
            {
                _ = a.Console();
            }
        });
    }

    private static void AddOpenTelemetrySink(IConfiguration configuration, Serilog.Configuration.LoggerSinkConfiguration sinkConfig)
    {
        IConfigurationSection otelSection = configuration.GetSection("Serilog:OpenTelemetry");
        if (!otelSection.Exists())
        {
            return;
        }

        string? endpoint = otelSection["Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }

        string protocol = otelSection["Protocol"]?.ToLowerInvariant() switch
        {
            "http" or "httpprotobuf" => "HttpProtobuf",
            _ => "Grpc"
        };

        sinkConfig.OpenTelemetry(options =>
        {
            options.Endpoint = endpoint;
            options.Protocol = Enum.Parse<OtlpProtocol>(protocol);

            string? resourceAttributes = otelSection["ResourceAttributes"];
            if (!string.IsNullOrWhiteSpace(resourceAttributes))
            {
                foreach (string pair in resourceAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] kv = pair.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        options.ResourceAttributes[kv[0].Trim()] = kv[1].Trim();
                    }
                }
            }
        });
    }

    private static void AddFileSink(IConfiguration configuration, Serilog.Configuration.LoggerSinkConfiguration sinkConfig)
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
        catch (Exception)
        {
            throw new MConfigurationException("Invalid log file path.", "Serilog:LogFilePath");
        }

        sinkConfig.File(
            new Serilog.Formatting.Json.JsonFormatter(),
            path,
            rollingInterval: RollingInterval.Infinite);
    }
}
