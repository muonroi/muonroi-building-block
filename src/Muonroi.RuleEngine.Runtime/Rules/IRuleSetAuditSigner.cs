namespace Muonroi.RuleEngine.Runtime.Rules;

public interface IRuleSetAuditSigner
{
    string KeyId { get; }

    string SignatureAlgorithm { get; }

    string Sign(string payload);

    bool Verify(string payload, string signature);
}

