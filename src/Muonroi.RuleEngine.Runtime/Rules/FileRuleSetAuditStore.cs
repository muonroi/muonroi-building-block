using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// File-backed NDJSON audit store for runtime ruleset governance actions.
/// </summary>
public sealed class FileRuleSetAuditStore(string rootPath, IMJsonSerializeService jsonSerializeService)
    : IRuleSetAuditStore
{
    private readonly string _rootPath = Path.GetFullPath(string.IsNullOrWhiteSpace(rootPath)
        ? throw new ArgumentException("Root path must not be empty.", nameof(rootPath))
        : rootPath);

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task AppendAsync(RuleSetAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        string filePath = GetAuditPath(entry.TenantId);
        string line = jsonSerializeService.Serialize(entry);

        SemaphoreSlim gate = FileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.AppendAllTextAsync(filePath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<RuleSetAuditPage> QueryAsync(
        string? workflowName = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        string filePath = GetAuditPath(ResolveTenantId());
        if (!File.Exists(filePath))
        {
            return new RuleSetAuditPage
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                Items = []
            };
        }

        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        List<RuleSetAuditEntry> entries = [];
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                RuleSetAuditEntry? entry = jsonSerializeService.Deserialize<RuleSetAuditEntry>(line);
                if (entry is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(workflowName) &&
                    !string.Equals(entry.WorkflowName, workflowName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries.Add(entry);
            }
            catch (JsonException)
            {
                // Ignore malformed lines to keep audit reads resilient.
            }
        }

        List<RuleSetAuditEntry> ordered = [.. entries
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id, StringComparer.OrdinalIgnoreCase)];
        int total = ordered.Count;
        List<RuleSetAuditEntry> paged = [.. ordered.Skip((page - 1) * pageSize).Take(pageSize)];

        return new RuleSetAuditPage
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = paged
        };
    }

    private string GetAuditPath(string? tenantId)
    {
        string safeTenant = SanitizeSegment(string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId!);
        string path = Path.Combine(_rootPath, safeTenant, "__audit", "ruleset-audit.ndjson");
        return Path.GetFullPath(path);
    }

    private static string ResolveTenantId()
    {
        return string.IsNullOrWhiteSpace(TenantContext.CurrentTenantId)
            ? "default"
            : TenantContext.CurrentTenantId!;
    }

    private static string SanitizeSegment(string value)
    {
        string segment = value.Trim();
        if (!Regex.IsMatch(segment, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException($"Invalid tenant segment: '{segment}'.");
        }

        return segment;
    }
}
