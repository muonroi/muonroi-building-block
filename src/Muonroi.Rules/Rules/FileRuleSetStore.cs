using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Tenancy.Core;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Stores ruleset JSON files on disk with versioning and rollback support.
/// Optionally signs each artifact to detect tampering.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public sealed class FileRuleSetStore : IRuleSetStore
{
    private readonly string _rootPath;
    private readonly IRuleSetSigner? _signer;
    private readonly RuleStoreConfigs _configs;
    private readonly Regex _segmentRegex;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> WorkflowLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileRuleSetStore"/> class.
    /// </summary>
    /// <param name="rootPath">The root directory where rulesets are stored.</param>
    /// <param name="signer">Optional signer for ruleset artifacts.</param>
    /// <param name="configs">Optional configuration for the ruleset store.</param>
    public FileRuleSetStore(string rootPath, IRuleSetSigner? signer = null, RuleStoreConfigs? configs = null)
    {
        MGuard.NotEmpty(rootPath);

        _configs = configs ?? new RuleStoreConfigs();
        MGuard.Against(_configs.MaxRuleSetSizeBytes <= 0, "MaxRuleSetSizeBytes must be greater than zero.");

        _rootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_rootPath);

        _signer = signer;
        if (_configs.RequireSignature && _signer is null)
        {
            MGuard.Fail<object>("RuleStore requires signature but no IRuleSetSigner is configured.", "RequireSignature");
        }

        string pattern = string.IsNullOrWhiteSpace(_configs.AllowedPathSegmentPattern)
            ? "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}$"
            : _configs.AllowedPathSegmentPattern;
        _segmentRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private string GetTenantDirectory()
    {
        string? tenant = TenantContext.CurrentTenantId;
        string tenantSegment = string.IsNullOrWhiteSpace(tenant)
            ? "default"
            : SanitizeSegment(tenant, nameof(TenantContext.CurrentTenantId));
        return EnsureUnderRoot(Path.Combine(_rootPath, tenantSegment));
    }

    private string GetWorkflowDirectory(string workflowName)
    {
        string workflowSegment = SanitizeSegment(workflowName, nameof(workflowName));
        return EnsureUnderRoot(Path.Combine(GetTenantDirectory(), workflowSegment));
    }

    /// <summary>
    /// Saves a new version of a ruleset.
    /// </summary>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="json">The JSON content of the ruleset.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
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

    /// <summary>
    /// Retrieves a ruleset by its name and optional version.
    /// </summary>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="version">The optional version to retrieve. If not specified, the active version is used.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous retrieval operation, containing the JSON content of the ruleset, or null if not found.</returns>
    public async Task<string?> GetAsync(string workflowName, int? version = null,
        CancellationToken cancellationToken = default)
    {
        string dir = GetWorkflowDirectory(workflowName);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        int ver = version ?? await GetActiveVersionAsync(dir, cancellationToken);
        string path = EnsureUnderRoot(Path.Combine(dir, $"v{ver}.json"));
        if (!File.Exists(path))
        {
            return null;
        }

        FileInfo fileInfo = new(path);
        if (fileInfo.Length > _configs.MaxRuleSetSizeBytes)
        {
            MGuard.Fail<object>(
                $"Ruleset exceeds MaxRuleSetSizeBytes ({_configs.MaxRuleSetSizeBytes}). Workflow={workflowName}, Version={ver}.");
        }

        string content = await File.ReadAllTextAsync(path, cancellationToken);
        EnsureRuleSetSize(content);

        if (_configs.RequireSignature && _signer is null)
        {
            MGuard.Fail<object>("Ruleset signature is required but no signer is configured.");
        }

        if (_signer is not null)
        {
            string sigPath = EnsureUnderRoot(Path.Combine(dir, $"v{ver}.sig"));
            if (!File.Exists(sigPath))
            {
                MGuard.Fail<object>("Signature file missing.");
            }

            string signature = await File.ReadAllTextAsync(sigPath, cancellationToken);
            if (!_signer.Verify(content, signature))
            {
                MGuard.Fail<object>("Ruleset signature validation failed.");
            }
        }

        return content;
    }

    /// <summary>
    /// Sets the active version of a ruleset.
    /// </summary>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="version">The version to set as active.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetActiveVersionAsync(string workflowName, int version,
        CancellationToken cancellationToken = default)
    {
        MGuard.Against(version <= 0, "Version must be greater than zero.");

        string dir = GetWorkflowDirectory(workflowName);
        string path = EnsureUnderRoot(Path.Combine(dir, $"v{version}.json"));
        if (!File.Exists(path))
        {
            MGuard.Fail<object>("RuleSetVersion", path);
        }

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

    /// <summary>
    /// Retrieves all available versions of a ruleset.
    /// </summary>
    /// <param name="workflowName">The name of the workflow.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation, containing an array of available versions.</returns>
    public Task<int[]> GetVersionsAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        string dir = GetWorkflowDirectory(workflowName);
        if (!Directory.Exists(dir))
        {
            return Task.FromResult(Array.Empty<int>());
        }

        int[] versions = [.. GetVersionsInternal(dir).OrderBy(v => v)];
        return Task.FromResult(versions);
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
        {
            return false;
        }

        return int.TryParse(fileName.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out version) &&
               version > 0;
    }

    private async Task<int> GetActiveVersionAsync(string workflowDir, CancellationToken cancellationToken)
    {
        string activePath = EnsureUnderRoot(Path.Combine(workflowDir, "active.txt"));
        if (File.Exists(activePath))
        {
            string content = await File.ReadAllTextAsync(activePath, cancellationToken);
            if (int.TryParse(content, NumberStyles.None, CultureInfo.InvariantCulture, out int v) && v > 0)
            {
                return v;
            }
        }

        return GetVersionsInternal(workflowDir).DefaultIfEmpty(1).Max();
    }

    private void EnsureRuleSetSize(string json)
    {
        int size = Encoding.UTF8.GetByteCount(json);
        if (size > _configs.MaxRuleSetSizeBytes)
        {
            MGuard.Fail<object>(
                $"Ruleset size ({size} bytes) exceeds MaxRuleSetSizeBytes ({_configs.MaxRuleSetSizeBytes}).");
        }
    }

    private string SanitizeSegment(string segment, string paramName)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            MGuard.Fail<object>($"{paramName} cannot be empty.");
        }

        if (!_segmentRegex.IsMatch(segment))
        {
            MGuard.Fail<object>($"{paramName} contains invalid characters: '{segment}'.");
        }

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
            MGuard.Fail<object>("Resolved path escapes ruleset root path.");
        }

        return fullPath;
    }
}
