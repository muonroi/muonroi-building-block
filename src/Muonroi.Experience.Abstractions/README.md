# Muonroi.Experience.Abstractions

> Contracts and data shapes for the Muonroi Experience Engine — enable AI agents to learn from session trajectories.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Experience.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Experience.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is the contract layer for the Muonroi Experience Engine — a self-learning subsystem that extracts structured `NeuronExperience` entries from agent session logs, stores them in a four-tier hierarchy (Principle → Behavioral → SelfQA → RawTrajectory), and injects relevant past experience into the agent context before each tool call via a PreToolUse hook.

This package ships **contracts only** — interfaces, records, and enums. There is no runtime behavior here. The default implementations (file-based store, Claude/Ollama brains, interception pipeline) live in [`Muonroi.Experience.Runtime`](../Muonroi.Experience.Runtime/).

## Installation

```bash
dotnet add package Muonroi.Experience.Abstractions --prerelease
```

## Quick Start

Reference this package when writing a custom implementation of any Experience Engine contract. The example below shows a minimal `IExperienceStore` implementation and a custom `IExperienceInterceptor`:

```csharp
using Muonroi.Experience.Abstractions;

// Custom store (e.g., backed by a relational database)
public sealed class MyExperienceStore : IExperienceStore
{
    public Task<bool> StoreAsync(NeuronExperience experience, CancellationToken ct = default)
    {
        // Persist experience to your DB; return false to reject duplicates
        throw new NotImplementedException();
    }

    public Task<IEnumerable<ExperienceSearchResult>> FindRelevantAsync(
        string query, int topK = 5, CancellationToken ct = default)
    {
        // Retrieve top-K entries by semantic or keyword relevance
        throw new NotImplementedException();
    }

    public Task<NeuronExperience> PromoteAsync(NeuronExperience experience, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<NeuronExperience> DemoteAsync(NeuronExperience experience, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<NeuronExperience> ClusterAndAbstractAsync(
        IEnumerable<NeuronExperience> tier2Entries, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IEnumerable<NeuronExperience>> FindAllInTierAsync(
        ExperienceTier tier, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(string id, CancellationToken ct = default)
        => throw new NotImplementedException();
}

// Custom interceptor — injects relevant experience into the agent prompt
public sealed class MyExperienceInterceptor : IExperienceInterceptor
{
    private readonly IExperienceStore _store;
    public MyExperienceInterceptor(IExperienceStore store) => _store = store;

    public async Task<string> InterceptAsync(
        string toolName, string toolContext, CancellationToken ct = default)
    {
        var hits = await _store.FindRelevantAsync($"{toolName}: {toolContext}", topK: 3, ct);
        return string.Join("\n", hits.Select(h => $"[{h.Experience.Tier}] {h.Experience.Solution}"));
    }
}
```

Register your implementations in DI:

```csharp
services.AddSingleton<IExperienceStore, MyExperienceStore>();
services.AddSingleton<IExperienceInterceptor, MyExperienceInterceptor>();
```

For a working end-to-end example using the default file-based store and extraction pipeline, see the [Muonroi.Experience.Sample](../../samples/Muonroi.Experience.Sample/).

## Features

- `IExperienceStore` — persist, retrieve, promote, demote, cluster, and delete `NeuronExperience` entries across the four-tier hierarchy
- `IExperienceBrain` — plug in an LLM-powered or rule-based extraction strategy: `ExtractAsync` (session log → entries) and `AbstractAsync` (cluster → principle)
- `IExperienceExtractor` — detect mistake signals in session logs (retry loops, user corrections, test failures) and emit structured `SelfQA` entries
- `IExperienceInterceptor` — query the store before a tool action executes and return an annotated context string to prepend to the agent prompt
- `NeuronExperience` — immutable record capturing `Trigger`, `Question`, `Reasoning[]`, `Solution`, `Principle`, `Confidence`, `HitCount`, `Tier`, `CreatedFrom`, and `CreatedAt`
- `ExperienceTier` — four-level enum: `Principle (0)`, `Behavioral (1)`, `SelfQA (2)`, `RawTrajectory (3)`
- `ExperienceBudgetConfig` — token budget and dedup settings per tier (dedup threshold, promotion hit threshold, archival days)
- `ExperienceSearchResult` — search hit record pairing a `NeuronExperience` with its `RelevanceScore`

## API Reference

| Type | Kind | Purpose |
|------|------|---------|
| `IExperienceStore` | Interface | Persist and retrieve `NeuronExperience` entries; promote/demote tiers; cluster to abstractions |
| `IExperienceBrain` | Interface | Extract entries from a session log; abstract a cluster of entries into a single principle |
| `IExperienceExtractor` | Interface | Detect mistake signals and emit `SelfQA`-tier entries via `ExtractQAAsync` |
| `IExperienceInterceptor` | Interface | Return context to inject before a tool call executes (`InterceptAsync`) |
| `NeuronExperience` | Sealed record | Single unit of learning: trigger, question, reasoning chain, solution, confidence, tier |
| `ExperienceTier` | Enum | Principle (0) → Behavioral (1) → SelfQA (2) → RawTrajectory (3) |
| `ExperienceBudgetConfig` | Sealed record | Token budgets per tier, `DedupThreshold` (default 0.85), `PromotionHitThreshold` (default 3), `ArchivalDaysThreshold` (default 90) |
| `ExperienceSearchResult` | Sealed record | `(NeuronExperience Experience, float RelevanceScore)` returned by `FindRelevantAsync` |

## Configuration

`ExperienceBudgetConfig` holds all tuning knobs. Pass it to your store or orchestrator via `IOptions<ExperienceBudgetConfig>`:

```csharp
services.Configure<ExperienceBudgetConfig>(cfg =>
{
    cfg.DedupThreshold         = 0.85f;   // cosine similarity above which an entry is a duplicate
    cfg.PromotionHitThreshold  = 3;       // confirmed hits before SelfQA → Behavioral
    cfg.ArchivalDaysThreshold  = 90;      // days of zero hits before archival
    cfg.PrincipleBudget        = 400;     // max tokens for Tier 0
    cfg.BehavioralBudget       = 600;     // max tokens for Tier 1
    cfg.SelfQABudget           = 500;     // max tokens for Tier 2
    cfg.InitialConfidenceMin   = 0.4f;
    cfg.InitialConfidenceMax   = 0.6f;
});
```

## Samples

- [Muonroi.Experience.Sample](../../samples/Muonroi.Experience.Sample/) — end-to-end demo: mistake detection, mock brain, file store, interception, tier evolution, and compression ratio report

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Experience.Runtime`](../Muonroi.Experience.Runtime/) — default implementations: `FileExperienceStore`, `QdrantExperienceStore`, `ClaudeExperienceBrain`, `OllamaExperienceBrain`, `CompositeExperienceBrain`, `MistakeDetector`, `DefaultExperienceInterceptor`, and the full extraction pipeline

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
