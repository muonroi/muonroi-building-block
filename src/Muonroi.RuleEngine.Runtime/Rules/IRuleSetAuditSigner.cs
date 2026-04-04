namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Signs and verifies audit records for ruleset changes.
/// </summary>
public interface IRuleSetAuditSigner
{
    /// <summary>Gets the signer key identifier.</summary>
    string KeyId { get; }

    /// <summary>Gets the signature algorithm name.</summary>
    string SignatureAlgorithm { get; }

    /// <summary>Signs an audit payload.</summary>
    /// <param name="payload">Payload to sign.</param>
    /// <returns>Signature string.</returns>
    string Sign(string payload);

    /// <summary>Verifies an audit payload signature.</summary>
    /// <param name="payload">Payload to verify.</param>
    /// <param name="signature">Signature string.</param>
    /// <returns><c>true</c> when the signature is valid.</returns>
    bool Verify(string payload, string signature);
}

