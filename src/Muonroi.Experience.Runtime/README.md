# Muonroi.Experience.Runtime

> Runtime store implementations for the Muonroi Experience Engine — Qdrant and file-based backends with token budget enforcement.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Experience.Runtime.svg)](https://www.nuget.org/packages/Muonroi.Experience.Runtime/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

The Experience Engine records agent learnings (`NeuronExperience`) across four tiers — Principle, Behavioral, SelfQA, and RawTrajectory — and retrieves them by semantic similarity at inference time. This package provides the DI wiring, concrete store backends (Qdrant vector database and a local JSON file store), the AI brain layer (Claude + Ollama with composite fallback), the extraction pipeline that detects mistakes from session transcripts, and the evolution orchestrator that clusters Tier 2 entries into reusable Tier 0 principles.

## Installation

```bash
dotnet add package Muonroi.Experience.Runtime --prerelease
```

## Quick Start

Register all three subsystems in order — store first, then brain, then evolution:

```csharp
using Muonroi.Experience.Runtime;
using Muonroi.Experience.Runtime.Brain;
using Muonroi.Experience.Runtime.Evolution;

var builder = Host.CreateApplicationBuilder(args);

// 1. Storage backend (file-based by default; set StoreType = Qdrant for vector search)
builder.Services.AddExperienceStore(o =>
{
    o.StoreType = ExperienceStoreType.File;
    o.FileDirectoryPath = "./experience-store";
    o.Budget = new ExperienceBudgetConfig();  // defaults: DedupThreshold=0.85
});

// 2. AI brain (Claude primary, Ollama fallback)
builder.Services.AddExperienceBrain(o =>
{
    o.ClaudeApiKey  = builder.Configuration["ExperienceBrain:ClaudeApiKey"]!;
    o.ClaudeModel   = "claude-haiku-4-5-20251001";
    o.OllamaEndpoint = "http://127.0.0.1:11434";
});

// 3. Evolution engine (background sweep opt-in)
builder.Services.AddExperienceEvolution(o =>
{
    o.MinClusterSize           = 3;
    o.ClusterSimilarityThreshold = 0.7f;
    o.EnableBackgroundService  = true;  // registers IHostedService
});

var app = builder.Build();

// Store and retrieve an experience
var orchestrator = app.Services.GetRequiredService<ExperienceStoreOrchestrator>();

var entry = new NeuronExperience
{
    Id        = Guid.NewGuid().ToString(),
    Tier      = ExperienceTier.Behavioral,
    Trigger   = "git push",
    Question  = "How do I push without breaking tests?",
    Solution  = "Run dotnet test before every push.",
    Confidence = 0.8f
};

await orchestrator.RouteAndStoreAsync(entry);

IEnumerable<ExperienceSearchResult> results =
    await orchestrator.FindRelevantAsync("push tests", topK: 5);
```

## Features

- **Dual storage backends**: `FileExperienceStore` (JSON per tier, zero infrastructure) and `QdrantExperienceStore` (vector similarity search via gRPC)
- **Four-tier hierarchy**: Principle (T0) → Behavioral (T1) → SelfQA (T2) → RawTrajectory (T3); tier routing handled automatically by `ExperienceStoreOrchestrator`
- **Token budget enforcement**: `ExperienceBudgetConfig` clamps dedup threshold and confidence range to prevent store bloat
- **Composite AI brain**: `CompositeExperienceBrain` tries `ClaudeExperienceBrain` first; falls back to `OllamaExperienceBrain` on empty response or exception
- **Transcript-based mistake detection**: `MistakeDetector.DetectAsync` scans raw session JSONL for four signal types — `retry_loop`, `user_correction`, `git_revert`, `test_red_green`
- **Extraction pipeline**: `ExperienceExtractionPipeline` converts `MistakeSignal` batches into `NeuronExperience` entries using the registered brain
- **Evolution orchestrator**: `ExperienceEvolutionOrchestrator` clusters Tier 2 entries by Jaccard similarity and abstracts clusters into Tier 0 principles; configurable via `EvolutionOptions`
- **Optional background evolution**: set `EvolutionOptions.EnableBackgroundService = true` to register `EvolutionBackgroundService` as `IHostedService` for weekly automated sweeps
- **Archive support**: `FileExperienceArchive` and `QdrantExperienceArchive` persist promoted/demoted entries; selected automatically based on `ExperienceStoreOptions.StoreType`

## Configuration

### `ExperienceStoreOptions` (section `"ExperienceStore"`)

| Property | Default | Description |
|---|---|---|
| `StoreType` | `ExperienceStoreType.File` | `File` or `Qdrant` |
| `FileDirectoryPath` | `./experience-store` | Directory for the file-based store |
| `QdrantUrl` | `http://localhost:6334` | Qdrant gRPC endpoint |
| `VectorSize` | `0` | **Required for Qdrant** — must match embedding model output dimension |
| `Budget` | `ExperienceBudgetConfig()` | `DedupThreshold=0.85`, `InitialConfidenceMin=0.4`, `InitialConfidenceMax=0.6` |

### `ExperienceBrainOptions` (section `"ExperienceBrain"`)

| Property | Default | Description |
|---|---|---|
| `ClaudeEndpoint` | `https://api.anthropic.com` | Anthropic API base |
| `ClaudeApiKey` | `""` | Anthropic API key |
| `ClaudeModel` | `claude-haiku-4-5-20251001` | Model for experience extraction |
| `OllamaEndpoint` | `http://127.0.0.1:11434` | Ollama base URL |
| `OllamaPrimaryModel` | `qwen2.5-coder:14b-instruct-q5_K_M` | Primary local model |
| `OllamaFallbackModel` | `qwen2.5-coder:7b-instruct-q5_K_M` | Fallback local model |
| `AiTimeoutSeconds` | `120` | HTTP timeout for AI calls |
| `MaxTokens` | `800` | Max tokens in AI response |
| `Temperature` | `0.3` | Sampling temperature |

### `EvolutionOptions`

| Property | Default | Description |
|---|---|---|
| `ClusterSimilarityThreshold` | `0.7` | Jaccard similarity above which two Tier 2 entries are grouped |
| `MinClusterSize` | `3` | Minimum cluster size before abstraction fires |
| `EnableBackgroundService` | `false` | Registers `EvolutionBackgroundService` as `IHostedService` |

## API Reference

| Type | Purpose |
|---|---|
| `AddExperienceStore(Action<ExperienceStoreOptions>?)` | Registers `IExperienceStore` (File or Qdrant) + `ExperienceStoreOrchestrator` |
| `AddExperienceBrain(Action<ExperienceBrainOptions>?)` | Registers `IExperienceBrain` (composite Claude+Ollama), `MistakeDetector`, `IExperienceExtractor` |
| `AddExperienceEvolution(Action<EvolutionOptions>?)` | Registers `IExperienceArchive`, `ExperienceEvolutionOrchestrator`, optional `EvolutionBackgroundService` |
| `ExperienceStoreOrchestrator` | Named entry point: `RouteAndStoreAsync`, `FindRelevantAsync`, `PromoteAsync`, `DemoteAsync`, `ClusterAndAbstractAsync`, `FindAllInTierAsync`, `DeleteAsync` |
| `ExperienceStoreOptions` | Store backend selection, paths, vector size, budget config |
| `ExperienceBrainOptions` | Claude and Ollama endpoint/model/timeout config |
| `EvolutionOptions` | Clustering thresholds and background service toggle |
| `FileExperienceStore` | JSON-file backed `IExperienceStore` (one file per tier) |
| `QdrantExperienceStore` | Qdrant vector backed `IExperienceStore`; validates `VectorSize > 0` at startup |
| `CompositeExperienceBrain` | `IExperienceBrain` with Claude primary and Ollama fallback |
| `ClaudeExperienceBrain` | Posts to Anthropic `/v1/messages`; returns `NeuronExperience` at tier `SelfQA` |
| `OllamaExperienceBrain` | Posts to Ollama `/api/generate` with streaming NDJSON accumulation |
| `MistakeDetector` | Scans raw session JSONL; emits `MistakeSignal` for retry loops, user corrections, git reverts, test red-green cycles |
| `ExperienceExtractionPipeline` | `IExperienceExtractor`; converts `MistakeSignal` batches into `NeuronExperience` via the registered brain |
| `ExperienceEvolutionOrchestrator` | Runs `RunEvolutionAsync`: promotes high-hit entries, archives low-confidence entries, clusters Tier 2 into Tier 0 principles |
| `FileExperienceArchive` / `QdrantExperienceArchive` | `IExperienceArchive` implementations selected by `StoreType` |
| `IQdrantClientWrapper` | Thin abstraction over `QdrantClient` for testability |

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Experience.Abstractions`](../Muonroi.Experience.Abstractions/) — contracts: `IExperienceStore`, `IExperienceBrain`, `IExperienceExtractor`, `IExperienceArchive`, `NeuronExperience`, `ExperienceTier`, `ExperienceBudgetConfig`
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — `IMLog<T>` structured logging abstraction used throughout
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — `MGuard` and core utilities

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
