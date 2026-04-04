namespace Muonroi.Rules.Rules;

/// <summary>
/// Provides methods to sign and verify ruleset artifacts.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public interface IRuleSetSigner
{
    /// <summary>Creates a signature for the specified content.</summary>
    string Sign(string content);

    /// <summary>Verifies the signature for the specified content.</summary>
    bool Verify(string content, string signature);
}
