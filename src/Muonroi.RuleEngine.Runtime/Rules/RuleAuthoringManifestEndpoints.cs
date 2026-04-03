using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Muonroi.RuleEngine.Abstractions.Authoring;

namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Exposes endpoints used by remote authoring tools to pull rule authoring manifests.
/// </summary>
public static class RuleAuthoringManifestEndpoints
{
    /// <summary>
    /// Maps the authoring manifest endpoint for the current application.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <param name="path">The endpoint path.</param>
    /// <returns>The endpoint builder.</returns>
    public static IEndpointRouteBuilder MapRuleAuthoringManifestEndpoint(
        this IEndpointRouteBuilder endpoints,
        string path = "/api/internal/rule-authoring/manifest")
    {
        endpoints.MapGet(path, (MRuleAuthoringManifestRegistry registry) =>
            {
                MRuleAuthoringManifest? merged = MergeManifests(registry.GetManifests());
                return merged is null ? Results.NotFound() : Results.Ok(merged);
            })
            .WithName("RuleEngine_GetAuthoringManifest")
            .AllowAnonymous();
        return endpoints;
    }

    /// <summary>
    /// Merges multiple manifests into one authoring document ordered by rule order and code.
    /// </summary>
    /// <param name="manifests">The manifests to merge.</param>
    /// <returns>The merged manifest or <see langword="null"/> when no manifest is available.</returns>
    public static MRuleAuthoringManifest? MergeManifests(IReadOnlyList<MRuleAuthoringManifest> manifests)
    {
        if (manifests.Count == 0)
        {
            return null;
        }

        List<MRuleAuthoringEntry> rules = [.. manifests
            .SelectMany(manifest => manifest.Rules)
            .GroupBy(rule => rule.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.Code, StringComparer.OrdinalIgnoreCase)];
        if (rules.Count == 0)
        {
            return null;
        }

        MRuleAuthoringManifest first = manifests[0];
        return new MRuleAuthoringManifest
        {
            AssemblyName = first.AssemblyName,
            AssemblyVersion = first.AssemblyVersion,
            Rules = rules
        };
    }
}
