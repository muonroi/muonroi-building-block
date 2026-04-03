namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Factory for creating authentication context instances.
/// </summary>
public interface IAuthContextFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="IAuthenticateInfoContext"/>.
    /// </summary>
    /// <returns>A new authentication info context.</returns>
    IAuthenticateInfoContext Create();
}
