# Muonroi.RuleEngine.Proliferation

> AI-driven neuron scenario generation and execution for the Muonroi RuleEngine — automatically proliferates test and simulation scenarios from your live rule set using Ollama, OpenAI, or Claude.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Proliferation.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Proliferation/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

The Proliferation Engine connects to a running `Muonroi.RuleEngine.Runtime` instance and uses a configurable AI brain to analyse your rule set, generate `NeuronScenario` objects that cover untested paths, and execute them automatically via a `ProliferationWorker` hosted service. Scenarios, results, and rule lineage are persisted through `IProliferationStore` — defaulting to an in-memory store; swap to Postgres by adding `Muonroi.RuleEngine.Proliferation.Persistence`.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Proliferation --prerelease
```

## Quick Start

```csharp
using Muonroi.Integration.Connectors.Registration;
using Muonroi.RuleEngine.Proliferation;

// IServiceTaskConnector is required by ExternalScenarioExecutor.
// AddMBuiltInConnectors() provides it. Register a standalone config
// provider so every scenario routes to the internal executor.
builder.Services.AddMBuiltInConnectors();
builder.Services.AddSingleton<IExternalProjectConfigProvider,
    StandaloneExternalProjectConfigProvider>();

// Register the AI scenario-generation stack:
// brain provider (Ollama / OpenAI / Claude per ProliferationOptions),
// prompt builder, deduplicators, budget allocator, scenario executors,
// and the ProliferationWorker hosted service.
// Defaults to InMemoryProliferationStore.
builder.Services.AddMProliferationEngine(builder.Configuration);
```

`appsettings.json` — brain selection and key tunables:

```json
{
  "Proliferation": {
    "BrainProvider": "ollama",
    "OllamaEndpoint": "http://127.0.0.1:11434",
    "PrimaryModel": "qwen2.5-coder:14b-instruct-q5_K_M",
    "AiTimeoutSeconds": 60,
    "EnableSemanticDedup": false,
    "EnableInfraAwareBudget": false
  }
}
```

Once running, `ProliferationWorker` polls every `WorkerIntervalSeconds` (default 300 s), calls `IRuleProliferationBrain.AnalyzeAsync`, and hands new `NeuronScenario` objects to `IScenarioExecutor`.

## Features

- **Pluggable AI brain** — built-in `OllamaProliferationBrain`, `OpenAiProliferationBrain`, and `ClaudeProliferationBrain`; swap via `BrainProvider` config key.
- **Composite brain mode** — chain multiple providers in `parallel` or `sequential` order via `CompositeBrains` + `CompositeMode`.
- **Failure feedback loop** — `DefaultFailureAnalyzer` drives additional scenario generation from execution failures when `EnableFailureFeedback` is true.
- **Semantic deduplication** — `VectorSemanticDeduplicator` (backed by `OllamaEmbedder`) prevents near-duplicate scenarios when `EnableSemanticDedup` is true.
- **Hash deduplication** — `InputHashDeduplicator` is always active as the base deduplication layer.
- **Chaos mode** — `DefaultChaosScenarioGenerator` generates adversarial edge-case scenarios when `EnableChaosMode` is true.
- **Coverage-weighted budget** — `CoverageWeightedBudgetAllocator` directs generation budget towards uncovered rule paths when `EnableSmartBudget` is true.
- **Infrastructure-aware budget** — `InfraHealthMonitor` dynamically scales the budget based on measured endpoint latency when `EnableInfraAwareBudget` is true.
- **Continuous simulation** — autonomous run loop (interval, batch size, and strategy are configurable) when `EnableContinuousMode` is true.
- **Auto-trigger** — re-proliferates automatically after rule-set changes (debounced) when `EnableAutoTrigger` is true.
- **Multi-tenant simulation** — routes scenarios per configured `SimulatedTenantIds` when `EnableMultiTenantSimulation` is true.
- **External project routing** — `RoutingScenarioExecutor` delegates to `ExternalScenarioExecutor` with mTLS, OAuth2, and API-key auth when a control-plane `IExternalProjectConfigProvider` is present.
- **Natural language rule conversion** — `NaturalLanguageRuleConverter` translates plain-text rule descriptions to structured rule JSON via the configured brain.
- **Regression runner** — `DefaultRegressionRunner` re-executes historical scenarios to guard against regressions.
- **Webhook notifications** — optional `IWebhookNotificationService` delivers lifecycle events to external consumers.
- **Pluggable persistence** — `IProliferationStore` defaults to `InMemoryProliferationStore`; replace with `Muonroi.RuleEngine.Proliferation.Persistence` for Postgres-backed durability.

## Configuration

All options are bound from the `"Proliferation"` configuration section (`ProliferationOptions.SectionName`).

| Key | Default | Description |
|-----|---------|-------------|
| `BrainProvider` | `"ollama"` | AI backend: `ollama`, `openai`, or `claude` |
| `OllamaEndpoint` | `"http://127.0.0.1:11434"` | Ollama base URL |
| `PrimaryModel` | `"qwen2.5-coder:14b-instruct-q5_K_M"` | Primary Ollama model |
| `FallbackModel` | `"qwen2.5-coder:7b-instruct-q5_K_M"` | Fallback Ollama model |
| `OpenAiEndpoint` | `"https://api.openai.com"` | OpenAI base URL |
| `OpenAiApiKey` | `""` | OpenAI API key |
| `OpenAiModel` | `"gpt-4o-mini"` | OpenAI model |
| `ClaudeEndpoint` | `"https://api.anthropic.com"` | Anthropic API base URL |
| `ClaudeApiKey` | `""` | Anthropic API key |
| `ClaudeModel` | `"claude-haiku-4-5-20251001"` | Claude model |
| `CompositeBrains` | `null` | Comma-separated brain names to chain |
| `CompositeMode` | `"parallel"` | `parallel` or `sequential` |
| `MaxScenariosPerRule` | `20` | Scenario cap per seed rule |
| `MaxTotalScenarios` | `500` | Total scenario cap |
| `WorkerIntervalSeconds` | `300` | Worker poll interval |
| `AiTimeoutSeconds` | `120` | Per-request AI timeout |
| `Temperature` | `0.7` | Generative sampling temperature |
| `EnableFailureFeedback` | `true` | Generate additional scenarios from failures |
| `EnableSemanticDedup` | `false` | Vector-based semantic deduplication |
| `SemanticDedupThreshold` | `0.85` | Cosine similarity threshold for dedup |
| `EnableChaosMode` | `false` | Chaos scenario generation |
| `EnableSmartBudget` | `false` | Coverage-weighted budget allocation |
| `EnableInfraAwareBudget` | `false` | Latency-aware dynamic budget scaling |
| `EnableContinuousMode` | `false` | Continuous autonomous simulation loop |
| `EnableAutoTrigger` | `false` | Auto-proliferate on rule-set changes |
| `EnableMultiTenantSimulation` | `false` | Per-tenant scenario routing |

## API Reference

| Type | Purpose |
|------|---------|
| `ProliferationServiceCollectionExtensions.AddMProliferationEngine` | Registers all engine services and the `ProliferationWorker` hosted service |
| `ProliferationOptions` | Bound from `"Proliferation"` section; controls brain selection, limits, and feature flags |
| `IRuleProliferationBrain` | Analyses a rule set and returns a `ProliferationPlan` of generated scenarios |
| `IScenarioExecutor` | Executes a single `NeuronScenario` and returns a `ScenarioResult` |
| `IProliferationStore` | Persists scenarios, results, lineage, and aggregate stats |
| `IExternalProjectConfigProvider` | Resolves per-tenant external execution config; implement for control-plane integration |
| `IProliferationChangeNotifier` | Broadcasts lifecycle events (triggered, scenario completed) — implement for SignalR or webhooks |
| `IWebhookNotificationService` | Delivers HTTP webhook notifications for proliferation events |
| `IRuleProliferationBrain` implementations | `OllamaProliferationBrain`, `OpenAiProliferationBrain`, `ClaudeProliferationBrain` |
| `CompositeProliferationBrain` | Chains multiple brains in parallel or sequential mode |
| `IScenarioDeduplicator` | Deduplication contract; `InputHashDeduplicator` (hash) or `VectorSemanticDeduplicator` (semantic) |
| `ICoverageTracker` / `DefaultCoverageTracker` | Tracks which rule paths have been exercised |
| `IBudgetAllocator` / `CoverageWeightedBudgetAllocator` | Allocates generation budget toward coverage gaps |
| `INaturalLanguageRuleConverter` | Converts plain-text rule descriptions to structured rule JSON via the brain |
| `IChaosScenarioGenerator` / `DefaultChaosScenarioGenerator` | Generates adversarial edge-case scenarios |
| `IRegressionRunner` / `DefaultRegressionRunner` | Re-executes historical scenarios to detect regressions |
| `IAuthStrategyResolver` | Resolves the auth strategy (mTLS, OAuth2, API key) for external project calls |
| `IOAuth2TokenProvider` / `CachingOAuth2TokenProvider` | Fetches and caches OAuth2 tokens for external executor |
| `IMtlsHttpClientFactory` / `MtlsHttpClientFactory` | Creates mTLS-configured `HttpClient` instances |
| `IApiKeyRotationService` / `ApiKeyRotationService` | Rotates API keys used by the external executor |
| `InMemoryProliferationStore` | Default in-process store; no persistence across restarts |
| `ProliferationWorker` | `BackgroundService` that drives the generate → execute → feedback loop |

## Samples

- [Quickstart.RuleEngine.Proliferation](../../samples/Quickstart.RuleEngine.Proliferation/) — end-to-end ASP.NET Core API demonstrating `AddMProliferationEngine` + Postgres persistence via `AddMProliferationPostgres`, with a stats controller reading from `IProliferationStore`.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — contracts shared across the rule engine stack
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — rule execution runtime that the Proliferation Engine generates scenarios against
- [`Muonroi.Integration.Connectors`](../Muonroi.Integration.Connectors/) — provides `IServiceTaskConnector` required by `ExternalScenarioExecutor`

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for terms.
