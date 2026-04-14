using Muonroi.Experience.Abstractions;

namespace Muonroi.Experience.Runtime.Archive;

/// <summary>
/// Long-term archive for experience entries that have aged out of the active store.
/// Archived entries are preserved for recovery — not permanently deleted.
/// </summary>
public interface IExperienceArchive
{
    /// <summary>
    /// Moves an experience entry into the archive.
    /// </summary>
    /// <param name="experience">The entry to archive.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ArchiveAsync(NeuronExperience experience, CancellationToken ct = default);

    /// <summary>
    /// Returns all archived entries.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<NeuronExperience>> GetArchivedAsync(CancellationToken ct = default);
}
