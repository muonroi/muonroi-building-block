namespace Muonroi.RuleEngine.Abstractions.Authoring;

/// <summary>
/// Provides rule authoring manifest metadata.
/// </summary>
public interface IRuleAuthoringManifestProvider
{
    /// <summary>Returns the rule authoring manifest.</summary>
    MRuleAuthoringManifest GetManifest();
}
