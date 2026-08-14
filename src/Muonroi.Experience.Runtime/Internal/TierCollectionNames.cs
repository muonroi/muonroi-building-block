namespace Muonroi.Experience.Runtime.Internal;

/// <summary>Maps ExperienceTier values to Qdrant collection name strings.</summary>
internal static class TierCollectionNames
{
    public const string Principle = "experience-principles";
    public const string Behavioral = "experience-behavioral";
    public const string SelfQA = "experience-selfqa";
    public const string Raw = "experience-raw";

    /// <summary>Returns the Qdrant collection name for the given tier.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown tier values.</exception>
    public static string For(ExperienceTier tier) => tier switch
    {
        ExperienceTier.Principle => Principle,
        ExperienceTier.Behavioral => Behavioral,
        ExperienceTier.SelfQA => SelfQA,
        ExperienceTier.RawTrajectory => Raw,
        _ => MGuard.Fail<string>($"Unknown ExperienceTier: {tier}")
    };
}
