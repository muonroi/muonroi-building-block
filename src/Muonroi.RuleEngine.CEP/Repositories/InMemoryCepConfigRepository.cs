using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.CEP.Abstractions;
using Muonroi.RuleEngine.CEP.Observability;

namespace Muonroi.RuleEngine.CEP.Repositories;

/// <summary>
/// In-memory CEP configuration store intended for development and local testing.
/// </summary>
public sealed class InMemoryCepConfigRepository(
    IMDateTimeService dateTimeService,
    ISystemExecutionContextAccessor contextAccessor) : ICepConfigRepository
{
    private readonly ConcurrentDictionary<string, CepConfig> _configs = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<CepConfig>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = ResolveTenantId();
        CepMetrics.ConfigReads.Add(1, CreateTags("list", tenantId));

        IReadOnlyList<CepConfig> items = _configs.Values
            .Where(x => string.Equals(x.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToArray();

        return Task.FromResult(items);
    }

    public Task<CepConfig?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = ResolveTenantId();
        CepMetrics.ConfigReads.Add(1, CreateTags("get", tenantId));

        _configs.TryGetValue(BuildKey(tenantId, id), out CepConfig? config);
        return Task.FromResult(config is null ? null : Clone(config));
    }

    public Task<CepConfig> SaveAsync(CepConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = NormalizeTenantId(config.TenantId);
        if (string.Equals(tenantId, "_global", StringComparison.OrdinalIgnoreCase))
        {
            tenantId = ResolveTenantId();
        }

        DateTime now = dateTimeService.UtcNow();
        CepMetrics.ConfigWrites.Add(1, CreateTags("save", tenantId));

        string storageKey = BuildKey(tenantId, config.Id);
        CepConfig saved = _configs.AddOrUpdate(
            storageKey,
            _ => Prepare(config, tenantId, now, createdAtUtc: now),
            (_, existing) => Prepare(config, tenantId, now, existing.CreatedAtUtc));

        return Task.FromResult(Clone(saved));
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string tenantId = ResolveTenantId();
        CepMetrics.ConfigWrites.Add(1, CreateTags("delete", tenantId));

        bool removed = _configs.TryRemove(BuildKey(tenantId, id), out _);
        return Task.FromResult(removed);
    }

    private CepConfig Prepare(CepConfig config, string tenantId, DateTime updatedAtUtc, DateTime createdAtUtc)
    {
        return new CepConfig
        {
            Id = NormalizeRequired(config.Id, nameof(config.Id)),
            TenantId = tenantId,
            Name = NormalizeRequired(config.Name, nameof(config.Name)),
            Description = NormalizeOptional(config.Description),
            WindowType = config.WindowType,
            WindowSize = config.WindowSize,
            TimeToLive = config.TimeToLive,
            CorrelationKey = NormalizeOptional(config.CorrelationKey) ?? "default",
            Metadata = CloneMetadata(config.Metadata),
            CreatedAtUtc = EnsureUtc(createdAtUtc),
            UpdatedAtUtc = EnsureUtc(updatedAtUtc)
        };
    }

    private string ResolveTenantId()
    {
        return NormalizeTenantId(contextAccessor.Get().TenantId);
    }

    private static CepConfig Clone(CepConfig config)
    {
        return config with
        {
            Metadata = CloneMetadata(config.Metadata)
        };
    }

    private static Dictionary<string, string> CloneMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        return new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
    }

    private static TagList CreateTags(string operation, string tenantId)
    {
        return new TagList
        {
            { "cep.operation", operation },
            { "tenant.id", tenantId }
        };
    }

    private static string BuildKey(string tenantId, string id)
    {
        return $"{NormalizeTenantId(tenantId)}:{NormalizeRequired(id, nameof(id))}";
    }

    private static string NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? "_global" : tenantId.Trim();
    }

    private static string NormalizeRequired(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}
