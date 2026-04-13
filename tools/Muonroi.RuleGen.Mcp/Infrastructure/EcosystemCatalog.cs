using Muonroi.RuleGen.Mcp.Models;

namespace Muonroi.RuleGen.Mcp.Infrastructure;

internal static class EcosystemCatalog
{
    public static IReadOnlyList<EcosystemRuleDescriptor> Rules { get; } =
    [
        new(
            "MBB001",
            "Warning",
            "Forbidden use of DateTime.UtcNow / Now / Today outside approved wrappers.",
            ["DateTime.UtcNow", "DateTime.Now", "DateTime.Today"],
            "IMDateTimeService",
            ["UtcNow()", "Now()", "Today()", "UtcToday()", "NowTs()", "UtcNowTs()"],
            "IMDateTimeService dateTimeService",
            ["MDateTimeService.cs", "clock providers"],
            "// MBB001-exempt: reason",
            "_dateTimeService.UtcNow()"),
        new(
            "MBB002",
            "Warning",
            "Forbidden direct JsonSerializer usage outside approved wrappers or static helper exemptions.",
            ["JsonSerializer.Serialize", "JsonSerializer.Deserialize"],
            "IMJsonSerializeService",
            ["Serialize()", "Deserialize<T>()"],
            "IMJsonSerializeService jsonSerializeService",
            ["serialization adapter helpers"],
            "// MBB002-exempt: reason",
            "_jsonSerializeService.Serialize(value)"),
        new(
            "MBB003",
            "Warning",
            "Domain and application DbContexts must inherit MDbContext, not raw DbContext.",
            [": DbContext"],
            "MDbContext",
            ["MDbContext(...)"],
            "derive from MDbContext",
            ["infrastructure persistence bridges"],
            "// MBB003-exempt: infrastructure boundary",
            ": MDbContext"),
        new(
            "MBB004",
            "Warning",
            "AsyncLocal<T> is reserved for Core.Abstractions.Context. Use execution context accessors elsewhere.",
            ["AsyncLocal<"],
            "ISystemExecutionContextAccessor",
            ["Get()", "Push()"],
            "ISystemExecutionContextAccessor contextAccessor",
            ["Muonroi.Core.Abstractions.Context"],
            "// MBB004-exempt: context boundary",
            "_contextAccessor.Get()"),
        new(
            "MBB005",
            "Warning",
            "Abstractions assemblies must not reference infrastructure dependencies such as EF Core, Hangfire, Quartz, MassTransit or Serilog packages.",
            ["EntityFrameworkCore", "Hangfire", "Quartz", "MassTransit", "Serilog", "RabbitMQ.Client", "Confluent.Kafka"],
            "Pure abstractions-only dependencies",
            [],
            "remove illegal PackageReference/ProjectReference",
            ["non-Abstractions assemblies"],
            "// MBB005-exempt: not valid for abstractions assemblies",
            "remove the reference"),
        new(
            "MBB006",
            "Warning",
            "Feature registrations such as AddMassTransit/AddRedis must call EnsureFeatureOrThrow before enabling paid features.",
            ["AddMassTransit", "AddGrpcServer", "AddRedis", "AddMessageBus", "AddRuleEngineStore", "AddObservability"],
            "EnsureFeatureOrThrow",
            ["EnsureFeatureOrThrow(...)"],
            "call the tier guard before feature registration",
            ["free-tier only bootstrap methods"],
            "// MBB006-exempt: tier guard not applicable",
            "EnsureFeatureOrThrow(...)"),
        new(
            "MBB007",
            "Warning",
            "Forbidden direct use of Serilog.Context.LogContext.PushProperty outside approved logging namespaces.",
            ["LogContext.PushProperty"],
            "IMLogContext",
            ["PushProperty()", "PushProperties()"],
            "IMLogContext logContext",
            ["Muonroi.Observability", "Muonroi.Logging"],
            "// MBB007-exempt: logging boundary",
            "_logContext.PushProperty(key, value)")
    ];

    public static WrapperSuggestionResult Suggest(string codeSnippet, string? violationType)
    {
        string text = codeSnippet.Trim();
        string normalized = string.IsNullOrWhiteSpace(violationType) ? text : violationType.Trim();

        if (normalized.Contains("DateTime", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("datetime", StringComparison.OrdinalIgnoreCase))
        {
            return new WrapperSuggestionResult(
                text,
                "_dateTimeService.UtcNow()",
                "IMDateTimeService _dateTimeService",
                "primary constructor: IMDateTimeService dateTimeService",
                false,
                "MBB001",
                null);
        }

        if (normalized.Contains("JsonSerializer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return new WrapperSuggestionResult(
                text,
                "_jsonSerializeService.Serialize(value)",
                "IMJsonSerializeService _jsonSerializeService",
                "primary constructor: IMJsonSerializeService jsonSerializeService",
                true,
                "MBB002",
                "// MBB002-exempt: static helper method - signing data serialization");
        }

        if (normalized.Contains("DbContext", StringComparison.OrdinalIgnoreCase))
        {
            return new WrapperSuggestionResult(
                text,
                ": MDbContext",
                "MDbContext base type",
                "constructor: DbContextOptions options, IMediator mediator, ...",
                true,
                "MBB003",
                "// MBB003-exempt: infrastructure boundary");
        }

        if (normalized.Contains("AsyncLocal", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("context", StringComparison.OrdinalIgnoreCase))
        {
            return new WrapperSuggestionResult(
                text,
                "_contextAccessor.Get()",
                "ISystemExecutionContextAccessor _contextAccessor",
                "primary constructor: ISystemExecutionContextAccessor contextAccessor",
                true,
                "MBB004",
                "// MBB004-exempt: context boundary");
        }

        if (normalized.Contains("LogContext", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("logging", StringComparison.OrdinalIgnoreCase))
        {
            return new WrapperSuggestionResult(
                text,
                "_logContext.PushProperty(key, value)",
                "IMLogContext _logContext",
                "primary constructor: IMLogContext logContext",
                true,
                "MBB007",
                "// MBB007-exempt: logging boundary");
        }

        return new WrapperSuggestionResult(
            text,
            text,
            "None",
            "No suggestion available",
            false,
            "Unknown",
            null);
    }
}
