using System.Text.Json;
using Microsoft.Extensions.Options;
using Muonroi.Experience.Abstractions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Archive;

/// <summary>
/// <see cref="IExperienceArchive"/> backed by a JSON file (<c>archived.json</c>) in the
/// same directory as the active <see cref="Muonroi.Experience.Runtime.File.FileExperienceStore"/>.
/// Archive is append-only and accessed only by the evolution orchestrator (single writer at a time),
/// but a <see cref="SemaphoreSlim"/> ensures thread safety if multiple sweeps overlap.
/// </summary>
public sealed class FileExperienceArchive : IExperienceArchive
{
    private readonly string _archivePath;
    private readonly IMLog<FileExperienceArchive>? _log;
    private readonly SemaphoreSlim _sem = new(1, 1);

    /// <summary>
    /// Initialises a new <see cref="FileExperienceArchive"/> using the same directory
    /// as the active file store.
    /// </summary>
    public FileExperienceArchive(
        IOptions<ExperienceStoreOptions> options,
        IMLog<FileExperienceArchive>? log = null)
    {
        string dir = options.Value.FileDirectoryPath;
        Directory.CreateDirectory(dir);
        _archivePath = Path.Combine(dir, "archived.json");
        _log = log;
    }

    /// <inheritdoc/>
    public async Task ArchiveAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct);
        try
        {
            List<NeuronExperience> existing = await LoadAsync(ct);
            existing.Add(experience);
            await SaveAsync(existing, ct);
            _log?.Debug("Archived entry {Id}", experience.Id);
        }
        finally
        {
            _sem.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<NeuronExperience>> GetArchivedAsync(CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct);
        try
        {
            return await LoadAsync(ct);
        }
        finally
        {
            _sem.Release();
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<List<NeuronExperience>> LoadAsync(CancellationToken ct)
    {
        if (!System.IO.File.Exists(_archivePath))
            return [];

        await using FileStream stream = System.IO.File.OpenRead(_archivePath);
        return await JsonSerializer.DeserializeAsync<List<NeuronExperience>>(stream, cancellationToken: ct)
               ?? [];
    }

    private async Task SaveAsync(List<NeuronExperience> entries, CancellationToken ct)
    {
        await using FileStream stream = System.IO.File.Open(_archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, entries,
            new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}
