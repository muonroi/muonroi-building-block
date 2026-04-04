using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Stores ruleset JSON files on disk with versioning and rollback support.
/// Optionally signs each artifact to detect tampering.
/// </summary>
public sealed class FileRuleSetStore : IRuleSetStore
{
    private readonly string _rootPath;
    private readonly IRuleSetSigner? _signer;
    private readonly RuleStoreConfigs _configs;
    private readonly ISystemExecutionContextAccessor _executionContext;
    private readonly Regex _segmentRegex;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WorkflowLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates a file-backed ruleset store.</summary>
    /// <param name="rootPath">Root directory for rulesets.</param>
    /// <param name="signer">Optional signer for integrity protection.</param>
    /// <param name="configs">Optional store configuration.</param>
    /// <param name="executionContextAccessor">Optional execution context accessor.</param>
    public FileRuleSetStore(
        string rootPath,
        IRuleSetSigner? signer = null,
        IOptions<RuleStoreConfigs>? configs = null,
        ISystemExecutionContextAccessor? executionContextAccessor = null)
    {
        MGuard.NotEmpty(rootPath);

        _configs = configs?.Value ?? new RuleStoreConfigs();
        MGuard.Against(_configs.MaxRuleSetSizeBytes <= 0, "MaxRuleSetSizeBytes must be greater than zero.");

        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);

        _signer = signer;
        if (_configs.RequireSignature && _signer is null)
            throw new MConfigurationException("RuleStore requires signature but no IRuleSetSigner is configured.", "RequireSignature");

        string pattern = string.IsNullOrWhiteSpace(_configs.AllowedPathSegmentPattern)
            ? "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$"
            : _configs.AllowedPathSegmentPattern;
        _segmentRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        _executionContext = executionContextAccessor ?? new SystemExecutionContextAccessor();
    }

    private string GetTenantDirectory()
    {
        string? tenant = _executionContext.Get().TenantId;
        string tenantSegment = string.IsNullOrWhiteSpace(tenant)
            ? "default"
            : SanitizeSegment(tenant, "TenantId");
        return EnsureUnderRoot(Path.Combine(_rootPath, tenantSegment));
    }

    private string GetWorkflowDirectory(string workflowName)
    {
        string workflowSegment = SanitizeSegment(workflowName, nameof(workflowName));
        return EnsureUnderRoot(Path.Combine(GetTenantDirectory(), workflowSegment));
    }

    /// <summary>Saves a new ruleset version and marks it active.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="json">Ruleset JSON content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsync(string workflowName, string json, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(json);
        EnsureRuleSetSize(json);
        string dir = GetWorkflowDirectory(workflowName);
        Directory.CreateDirectory(dir);

        SemaphoreSlim gate = WorkflowLocks.GetOrAdd(dir, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            int version = GetVersionsInternal(dir).DefaultIfEmpty(0).Max() + 1;
            string jsonPath = EnsureUnderRoot(Path.Combine(dir, $"v{version}.json"));
            await File.WriteAllTextAsync(jsonPath, json, cancellationToken);

            if (_signer is not null)
            {
                string signature = _signer.Sign(json);
                string sigPath = EnsureUnderRoot(Path.Combine(dir, $"v{version}.sig"));
                await File.WriteAllTextAsync(sigPath, signature, cancellationToken);
            }

            string activePath = EnsureUnderRoot(Path.Combine(dir, "active.txt"));
            await File.WriteAllTextAsync(activePath, version.ToString(CultureInfo.InvariantCulture), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Gets a ruleset by workflow and version.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Specific version; when null, uses active version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ruleset JSON or null if not found.</returns>
    public async Task<string?> GetAsync(string workflowName, int? version = null,
        CancellationToken cancellationToken = default)
    {
        string dir = GetWorkflowDirectory(workflowName);
        if (!Directory.Exists(dir)) return null;

        int ver = version ?? await GetActiveVersionFromDirectoryAsync(dir, cancellationToken);
        string path = EnsureUnderRoot(Path.Combine(dir, $"v{ver}.json"));
        if (!File.Exists(path)) return null;

        FileInfo fileInfo = new(path);
        if (fileInfo.Length > _configs.MaxRuleSetSizeBytes)
            throw new MConfigurationException(
                $"Ruleset exceeds MaxRuleSetSizeBytes ({_configs.MaxRuleSetSizeBytes}). Workflow={workflowName}, Version={ver}.");

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        EnsureRuleSetSize(content);

        if (_configs.RequireSignature && _signer is null)
            throw new MConfigurationException("Ruleset signature is required but no signer is configured.", "RequireSignature");

        if (_signer is not null)
        {
            string sigPath = EnsureUnderRoot(Path.Combine(dir, $"v{ver}.sig"));
            if (!File.Exists(sigPath)) throw new MConfigurationException("Signature file missing.");

            string signature = await File.ReadAllTextAsync(sigPath, cancellationToken);
            if (!_signer.Verify(content, signature))
                throw new MConfigurationException("Ruleset signature validation failed.");
        }

        return content;
    }

    /// <summary>Sets the active version for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="version">Version to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SetActiveVersionAsync(string workflowName, int version,
        CancellationToken cancellationToken = default)
    {
        MGuard.Against(version <= 0, "Version must be greater than zero.");

        string dir = GetWorkflowDirectory(workflowName);
        string path = EnsureUnderRoot(Path.Combine(dir, $"v{version}.json"));
        if (!File.Exists(path)) throw new MNotFoundException("RuleSetVersion", path);

        Directory.CreateDirectory(dir);
        SemaphoreSlim gate = WorkflowLocks.GetOrAdd(dir, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            string activePath = EnsureUnderRoot(Path.Combine(dir, "active.txt"));
            await File.WriteAllTextAsync(activePath, version.ToString(CultureInfo.InvariantCulture), cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Gets all versions for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of versions.</returns>
    public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        string dir = GetWorkflowDirectory(workflowName);
        if (!Directory.Exists(dir)) return Task.FromResult(Array.Empty<int>());

        int[] versions = [.. GetVersionsInternal(dir).OrderBy(v => v)];
        return Task.FromResult(versions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(int Version, string Status, bool IsActive, DateTimeOffset CreatedAt)>> GetVersionDetailsAsync(
        string workflowName, int limit = 10, int offset = 0, CancellationToken cancellationToken = default)
    {
        string dir = GetWorkflowDirectory(workflowName);
        if (!Directory.Exists(dir))
            return [];

        int? activeVersion = await TryGetActiveVersionFromDirectoryAsync(dir, cancellationToken);
        var items = GetVersionsInternal(dir)
            .OrderByDescending(v => v)
            .Skip(offset)
            .Take(limit)
            .Select(v =>
            {
                string filePath = EnsureUnderRoot(Path.Combine(dir, $"v{v}.json"));
                DateTimeOffset createdAt = File.Exists(filePath) ? new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero) : DateTimeOffset.UtcNow;
                bool isActive = activeVersion.HasValue && activeVersion.Value == v;
                string status = isActive ? "Active" : "Superseded";
                return (v, status, isActive, createdAt);
            })
            .ToList();
        return items;
    }

    /// <summary>Gets the active version for a workflow.</summary>
    /// <param name="workflowName">Workflow name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active version or null if none exists.</returns>
    public async Task<int?> GetActiveVersionAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        string dir = GetWorkflowDirectory(workflowName);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        int? active = await TryGetActiveVersionFromDirectoryAsync(dir, cancellationToken);
        if (active.HasValue)
        {
            return active;
        }

        int[] versions = [.. GetVersionsInternal(dir).OrderBy(v => v)];
        return versions.Length == 0 ? null : versions[^1];
    }

    /// <summary>Lists workflows available for the current tenant.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of workflow names.</returns>
    public Task<IReadOnlyList<string>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        string tenantDir = GetTenantDirectory();
        if (!Directory.Exists(tenantDir))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        string[] workflows = [.. Directory.EnumerateDirectories(tenantDir)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => name is not null && _segmentRegex.IsMatch(name))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

        return Task.FromResult<IReadOnlyList<string>>(workflows);
    }

    private static IEnumerable<int> GetVersionsInternal(string workflowDir)
    {
        foreach (string file in Directory.EnumerateFiles(workflowDir, "v*.json"))
        {
            if (TryParseVersionFromFileName(file, out int version))
            {
                yield return version;
            }
        }
    }

    private static bool TryParseVersionFromFileName(string filePath, out int version)
    {
        version = 0;
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length <= 1 || fileName[0] != 'v')
            return false;

        return int.TryParse(fileName.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out version) &&
               version > 0;
    }

    private async Task<int> GetActiveVersionFromDirectoryAsync(string workflowDir, CancellationToken cancellationToken)
    {
        int? active = await TryGetActiveVersionFromDirectoryAsync(workflowDir, cancellationToken);
        if (active.HasValue)
        {
            return active.Value;
        }

        return GetVersionsInternal(workflowDir).DefaultIfEmpty(1).Max();
    }

    private async Task<int?> TryGetActiveVersionFromDirectoryAsync(string workflowDir, CancellationToken cancellationToken)
    {
        string activePath = EnsureUnderRoot(Path.Combine(workflowDir, "active.txt"));
        if (!File.Exists(activePath))
        {
            return null;
        }

        string content = await File.ReadAllTextAsync(activePath, cancellationToken);
        if (int.TryParse(content, NumberStyles.None, CultureInfo.InvariantCulture, out int version) && version > 0)
        {
            return version;
        }

        return null;
    }

    private void EnsureRuleSetSize(string json)
    {
        int size = Encoding.UTF8.GetByteCount(json);
        if (size > _configs.MaxRuleSetSizeBytes)
            throw new MConfigurationException(
                $"Ruleset size ({size} bytes) exceeds MaxRuleSetSizeBytes ({_configs.MaxRuleSetSizeBytes}).");
    }

    private string SanitizeSegment(string segment, string paramName)
    {
        if (string.IsNullOrWhiteSpace(segment))
            throw new MConfigurationException($"{paramName} cannot be empty.");

        if (!_segmentRegex.IsMatch(segment))
            throw new MConfigurationException($"{paramName} contains invalid characters: '{segment}'.");

        return segment;
    }

    private string EnsureUnderRoot(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string rootWithSeparator = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, _rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new MConfigurationException("Resolved path escapes ruleset root path.");
        }

        return fullPath;
    }
}
