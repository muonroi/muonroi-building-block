namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Provides signing credentials for token generation.
/// </summary>
public interface ITokenSigner
{
    /// <summary>
    /// Gets the signing credentials.
    /// </summary>
    /// <returns>The signing credentials.</returns>
    SigningCredentials GetCredentials();
}
