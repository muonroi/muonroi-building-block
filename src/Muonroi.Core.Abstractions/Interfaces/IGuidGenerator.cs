namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Generates unique identifiers (GUIDs).
/// </summary>
public interface IGuidGenerator
{
    /// <summary>
    /// Creates a new <see cref="Guid"/>.
    /// </summary>
    /// <returns>A new GUID.</returns>
    Guid Create();
}
