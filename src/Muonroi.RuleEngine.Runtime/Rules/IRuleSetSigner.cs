namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Provides methods to sign and verify ruleset artifacts.
/// </summary>
public interface IRuleSetSigner
{
    /// <summary>Creates a signature for the specified content.</summary>
    string Sign(string content);

    /// <summary>Verifies the signature for the specified content.</summary>
    bool Verify(string content, string signature);
}
