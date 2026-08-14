namespace Muonroi.Observability.Logging;

/// <summary>
/// Enriches Serilog events with standard Elastic Common Schema (ECS) service and host properties.
/// </summary>
public sealed class ServiceContextEnricher(IHostEnvironment environment, IConfiguration configuration) : ILogEventEnricher
{
    private readonly string _environmentName = environment.EnvironmentName;
    private readonly string _applicationName = environment.ApplicationName;
    private readonly string _machineName = Environment.MachineName;
    private readonly string _appVersion = configuration["AppVersion"] ?? "1.0.0";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // ECS service properties
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("service.name", _applicationName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("service.environment", _environmentName));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("service.version", _appVersion));
        
        // ECS host properties
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("host.hostname", _machineName));
    }
}
