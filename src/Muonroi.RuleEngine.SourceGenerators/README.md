# Muonroi.RuleEngine.SourceGenerators

> Roslyn source generator and analyzer pack that turns annotated C# methods into
> fully-wired `IRule<TContext>` classes at compile time — no hand-written boilerplate,
> no runtime reflection for class discovery.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.SourceGenerators.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.SourceGenerators/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is a **Roslyn analyzer/source-generator** component — it ships no
runtime API. It plugs into the C# compiler and does two things: (1) generates
concrete rule classes from methods annotated with `[MExtractAsRule]`, and
(2) enforces Muonroi ecosystem coding rules (MBB001–MBB010) across any project
that references it.

## Installation

Add the reference as an **analyzer**, not a regular assembly reference:

```xml
<!-- YourProject.csproj -->
<ItemGroup>
  <PackageReference Include="Muonroi.RuleEngine.SourceGenerators" Version="1.0.0-alpha.15"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

```bash
dotnet add package Muonroi.RuleEngine.SourceGenerators --prerelease
```

> After adding the package, set `OutputItemType="Analyzer"` and
> `ReferenceOutputAssembly="false"` in the csproj entry — this is the same
> pattern used in the `RuleSourceGen` sample's csproj.

## Quick Start

Mark ordinary methods with `[MExtractAsRule]`. The generator reads the method body
and emits a `{Code}Rule.g.cs` file containing a sealed class that implements
`IRule<TContext>`.

```csharp
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Core.Abstractions.Guards;

public sealed class ValidationRules
{
    // Generates: DISCOUNT_VALIDATERule.g.cs
    [MExtractAsRule("DISCOUNT_VALIDATE", Order = 0)]
    public RuleResult Validate(DiscountRequest context)
    {
        MGuard.NotNull(context, nameof(context));
        if (string.IsNullOrWhiteSpace(context.CustomerType))
            return RuleResult.Failure("CustomerType is required.");
        if (context.Subtotal <= 0m)
            return RuleResult.Failure("Subtotal must be greater than zero.");
        return RuleResult.Passed();
    }
}

public sealed class DiscountRules
{
    // Generates: DISCOUNT_PREMIUMRule.g.cs — runs after DISCOUNT_VALIDATE
    [MExtractAsRule("DISCOUNT_PREMIUM", Order = 1, DependsOn = ["DISCOUNT_VALIDATE"])]
    public decimal ApplyPremiumDiscount(DiscountRequest context)
    {
        MGuard.NotNull(context, nameof(context));
        if (!string.Equals(context.CustomerType, "premium", StringComparison.OrdinalIgnoreCase))
            return 0m;
        return context.Subtotal >= 500m ? 0.15m : 0.10m;
    }
}
```

Register the runtime engine normally — the generated classes implement `IRule<T>`
and are picked up by `AddRulesFromAssemblies`:

```csharp
// Program.cs
builder.Services.AddRuleEngine<DiscountRequest>();
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);
```

The generator also emits:

- `RuleEngineRegistrationExtensions.g.cs` — a `AddGeneratedRules(this IServiceCollection)` extension (via `RuleRegistrationGenerator`) that registers each discovered `IRule<T>` as `Transient`.
- `RuleCatalogRegistration.g.cs` — a build-time manifest provider consumed by the authoring registry (via `RuleCatalogRegistrationGenerator`).

## Features

- **Zero-boilerplate rule extraction** — annotate a method, get a sealed `IRule<TContext>` class with constructor DI, `Code`, `Order`, `DependsOn`, `HookPoint`, `RuleType`, and `EvaluateAsync` wired automatically.
- **Dependency injection wiring** — interface fields referenced by the annotated method are auto-promoted to constructor parameters in the generated class.
- **Private helper method inlining** — private methods called from the annotated method body are copied verbatim into the generated class as `private static` helpers.
- **FEEL expression support** — set `Expression = "subtotal > 100"` to use a FEEL predicate instead of a C# body; the generator validates syntax at compile time (MRG010) and emits a `BuildFeelVariables` helper.
- **FactBag-aware mode** — set `UseFactBagAware = true` to generate a class extending `MFactBagAwareRule<TContext>` with `ReadFact`/`WriteFact` instead of raw `FactBag` parameter access.
- **DependsOn graph validation** — MRG005 fires when a declared dependency code has no matching rule; MRG006 warns when `Order` is set without `DependsOn`.
- **Ecosystem analyzers (MBB001–MBB010)** — enforced across every project that references this package (see Diagnostics section).
- **Code fixes** — auto-fix providers for MBB001, MBB002, MBB008, MBB009, MBB010.
- **Opt-in diagnostics-only mode** — set MSBuild property `MuonroiRuleGenDiagnosticsOnly=true` to run analyzers without emitting generated source.

## Configuration

### `[MExtractAsRule]` attribute properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `code` (ctor) | `string` | method name | Unique rule identifier, becomes the class name prefix. |
| `Order` | `int` | `0` | Execution hint; orchestrator sorts by `DependsOn` graph. |
| `HookPoint` | `HookPoint` enum | `BeforeRule` | Lifecycle phase: `BeforeRule`, `AfterRule`, `BeforeValidation`, `AfterValidation`. |
| `DependsOn` | `string[]` | `[]` | Rule codes this rule depends on. |
| `Expression` | `string` | `null` | FEEL expression used instead of C# body. |
| `UseFactBagAware` | `bool` | `false` | Generate a `MFactBagAwareRule<T>` subclass. |

### MSBuild property

```xml
<PropertyGroup>
  <!-- Emit diagnostics only — skip source generation -->
  <MuonroiRuleGenDiagnosticsOnly>true</MuonroiRuleGenDiagnosticsOnly>
</PropertyGroup>
```

## Diagnostics

### Rule generation (MRG series)

| ID | Severity | Meaning |
|----|----------|---------|
| `MRG001` | Error | Duplicate rule code in the assembly. |
| `MRG002` | Error | Invalid `HookPoint` value. |
| `MRG003` | Warning | Dependency field is not an interface — use interface types for DI. |
| `MRG004` | Warning | Private helper method could not be extracted. |
| `MRG005` | Warning | `DependsOn` references a code with no matching rule. |
| `MRG006` | Warning | `Order` set without `DependsOn` — orchestrator sorts by graph, not `Order`. |
| `MRG007` | Warning | `FactBag` key read without a dependency path to its producer. |
| `MRG008` | Warning | Nullable value assigned to non-nullable `string` — add `?? string.Empty`. |
| `MRG009` | Warning | `throw InvalidOperationException` for a missing fact key — prefer `RuleResult.Failure`. |
| `MRG010` | Error | FEEL expression failed compile-time syntax validation. |

### Ecosystem analyzers (MBB series)

| ID | Severity | Meaning | Code fix |
|----|----------|---------|---------|
| `MBB001` | Warning | `DateTime.Now` / `DateTime.UtcNow` used directly — use `IMDateTimeService`. | Yes |
| `MBB002` | Warning | `JsonSerializer` member called directly — use `IMJsonSerializeService`. | Yes |
| `MBB003` | Warning | `DbContext` subclass does not inherit `MDbContext`. | No |
| `MBB004` | Warning | `AsyncLocal` used outside the context package — use `ISystemExecutionContextAccessor`. | No |
| `MBB005` | Warning | Abstractions assembly references an infrastructure dependency. | No |
| `MBB006` | Warning | Registration method missing `EnsureFeatureOrThrow` tier guard. | No |
| `MBB007` | Warning | `Serilog.Context.LogContext` used directly — use `IMLogContext.PushProperty()`. | No |
| `MBB008` | Warning | Cross-capability reference without `IMEcosystemRegistry.Has(MCapability.X)` guard. | Yes |
| `MBB009` | Warning | Raw exception type thrown in a Muonroi namespace — use the `M`-prefixed equivalent. | Yes |
| `MBB010` | Warning | Public method parameter lacks `MGuard.NotNull()` guard. | Yes |

## API Reference

This package has no runtime API surface. All public types below are Roslyn extension
points visible only to the compiler.

| Type | Kind | Purpose |
|------|------|---------|
| `ExtractAsRuleGenerator` | `IIncrementalGenerator` | Reads `[MExtractAsRule]` methods and emits `{Code}Rule.g.cs` per rule. |
| `RuleRegistrationGenerator` | `IIncrementalGenerator` | Scans all `IRule<T>` implementors and emits `RuleEngineRegistrationExtensions.g.cs`. |
| `RuleCatalogRegistrationGenerator` | `IIncrementalGenerator` | Emits `RuleCatalogRegistration.g.cs` for the authoring manifest registry. |
| `Mbb001_ForbiddenDateTimeAnalyzer` … `Mbb010_MissingGuardAnalyzer` | `DiagnosticAnalyzer` | Ecosystem closure analyzers (MBB001–MBB010). |
| `FeelExpressionSyntaxValidator` | internal | Validates FEEL expression syntax at generation time. |

## Samples

- [RuleSourceGen](../../samples/RuleSourceGen/) — end-to-end discount-calculation API demonstrating `[MExtractAsRule]` with `DependsOn`, generator-emitted DI registration, and xUnit tests against generated rule classes.

## Compatibility

- Target framework: `netstandard2.0` (Roslyn requirement — consumed by any .NET SDK that supports Roslyn analyzers)
- Host project: .NET 6 or later (tested against net8.0 in sample)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — defines `IRule<TContext>`, `MExtractAsRuleAttribute`, `FactBag`, `RuleResult`, and all contracts that generated classes implement.
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — provides `AddRuleEngine<T>()`, `AddRulesFromAssemblies()`, and the orchestrator that executes the generated rules at runtime.
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — persistence, canary rollout, FEEL runtime compiler, and the authoring catalog consumed by `RuleCatalogRegistration.g.cs`.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
