using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.Enterprise.ServerValidation;

/// <summary>
/// File-backed <see cref="IFailedChainSubmissionStore"/>. Each pending submission is one JSON file
/// under <c>&lt;chain-file-dir&gt;/pending-chain-submissions/</c> (or content-root if no chain path),
/// so individual entries can be removed independently once accepted. Writes are atomic
/// (temp file + move) so a concurrent <see cref="ListPendingAsync"/> never observes a partial file.
/// </summary>
public sealed class FileFailedChainSubmissionStore(
    IHostEnvironment? environment,
    LicenseConfigs configs,
    IMJsonSerializeService jsonSerializeService,
    IMLog<FileFailedChainSubmissionStore>? logger = null) : IFailedChainSubmissionStore
{
    private const string FolderName = "pending-chain-submissions";

    /// <inheritdoc/>
    public async Task EnqueueAsync(PendingChainSubmission pending, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(pending);
        if (string.IsNullOrWhiteSpace(pending.Id))
        {
            pending.Id = Guid.NewGuid().ToString("N");
        }

        await WriteAsync(pending, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(PendingChainSubmission pending, CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(pending);
        return WriteAsync(pending, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingChainSubmission>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        string folder = ResolveFolder();
        if (!Directory.Exists(folder))
        {
            return [];
        }

        List<PendingChainSubmission> result = [];
        foreach (string file in Directory.EnumerateFiles(folder, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                string json = await File.ReadAllTextAsync(file, cancellationToken);
                PendingChainSubmission? pending = jsonSerializeService.Deserialize<PendingChainSubmission>(json);
                if (pending is not null)
                {
                    result.Add(pending);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.Warn("[License] Skipping unreadable pending chain submission '{File}': {Error}", file, ex.Message);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(id);
        string path = Path.Combine(ResolveFolder(), $"{SanitizeId(id)}.json");
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[License] Failed to remove pending chain submission '{Id}'.", id);
        }

        return Task.CompletedTask;
    }

    private async Task WriteAsync(PendingChainSubmission pending, CancellationToken cancellationToken)
    {
        string folder = ResolveFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"{SanitizeId(pending.Id)}.json");
        string tmp = path + ".tmp";

        string json = jsonSerializeService.Serialize(pending);
        await File.WriteAllTextAsync(tmp, json, cancellationToken);
        File.Move(tmp, path, overwrite: true); // atomic publish so ListPendingAsync never sees a partial file
    }

    private string ResolveFolder()
    {
        string root = !string.IsNullOrWhiteSpace(environment?.ContentRootPath)
            ? environment.ContentRootPath
            : AppContext.BaseDirectory;

        string? chainPath = configs.ChainFilePath;
        if (!string.IsNullOrWhiteSpace(chainPath))
        {
            string resolvedChain = Path.IsPathRooted(chainPath)
                ? chainPath
                : Path.GetFullPath(Path.Combine(root, chainPath));
            string? dir = Path.GetDirectoryName(resolvedChain);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return Path.Combine(dir, FolderName);
            }
        }

        return Path.Combine(root, FolderName);
    }

    private static string SanitizeId(string id)
    {
        Span<char> buffer = stackalloc char[id.Length];
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            buffer[i] = char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_';
        }

        return new string(buffer);
    }
}
