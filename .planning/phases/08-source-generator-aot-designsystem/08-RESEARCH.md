# Phase 8: v0.2 — Source Generator + AOT + DesignSystem — Research

**Researched:** 2026-05-27
**Domain:** Roslyn incremental source generators, NativeAOT/trim-safety, Alpine containers, BenchmarkDotNet, HTML/CSS design-system templates
**Confidence:** HIGH for SG/ALLOC/DS domains; MEDIUM for AOT domain (AngleSharp.Css trim status unverified from official source)

---

## Summary

Phase 8 is the v0.2 hardening milestone. It adds four orthogonal capabilities on top of the 189-test-passing v0.1 engine: (1) a Roslyn incremental source generator that emits compile-time `IMPdfRenderer<TModel>` implementations replacing the runtime factory hot path; (2) NativeAOT/trim-safety annotations across the render pipeline; (3) an Alpine PublishAot container sample under 40 MB; (4) a `Muonroi.Pdf.DesignSystem.Default` package with three starter templates that pass `DefaultStrictPolicy` without violations; and (5) BenchmarkDotNet harness proving the SG path is ≥3× faster and allocations are ≥30% lower than v0.1.

**Biggest risk: AOT.** `OtelSetup.cs` (lines 45–49) scans `AppDomain.CurrentDomain.GetAssemblies()` and calls `Activator.CreateInstance` on every `ITelemetryDescriptor` type — this is a hard AOT-incompatible reflection pattern. AngleSharp.Css 1.0.0-beta.147 has no published `IsAotCompatible` metadata and no documented trim annotations; it uses reflection internally for CSS property registration. These two issues mean **`PublishAot=true` will produce IL warnings at minimum**. The realistic target for the engine itself is `PublishTrimmed=true` with explicit `TrimmerRootDescriptor` files, reserving full AOT for a standalone console sample that avoids `OtelSetup`.

**No requirement in Phase 8 touches Chromium, a browser, wkhtmltopdf, or any native rendering binary.** All SG/AOT/DS/ALLOC requirements are satisfied entirely within the pure-C# pipeline.

**Primary recommendation:** Ship the SG and DS work first (Wave 1–2), then tackle AOT/trim in Wave 3, benchmarks in Wave 4. The SG csproj follows the exact same pattern as `Muonroi.Tenancy.SiteProfile.SourceGenerators` — no new infrastructure needed.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Source generator project | `src/Muonroi.Pdf.SourceGenerators` (new, netstandard2.0) | Referenced by `Muonroi.Pdf` via `OutputItemType="Analyzer"` | Standard Roslyn SG project placement; analyzer references require netstandard2.0 target |
| Marker attribute `[PdfTemplate]` | `Muonroi.Pdf.Abstractions` (netstandard2.0) | Emitted by SG via `RegisterPostInitializationOutput` | Abstractions already netstandard2.0; placing marker there avoids a new assembly |
| Generated `IMPdfRenderer<TModel>` impl | consumer's compilation unit (generated code) | — | SG emits into the consuming project, not into the engine DLL |
| Trim annotations on engine | `Muonroi.Pdf` (net8.0) | `Muonroi.Pdf.Governance` | Hot path types annotated with `[DynamicallyAccessedMembers]`; OtelSetup left untouched (not in Pdf packages) |
| AOT sample | `samples/Muonroi.Pdf.AotSample` (new, net8.0, PublishAot=true) | — | Isolated console project; does NOT reference Muonroi.Observability |
| Dockerfile (Alpine, multi-stage) | `samples/Muonroi.Pdf.AotSample/Dockerfile` | — | Build inside Docker on linux-musl-x64; host Docker path required |
| Design system templates | `src/Muonroi.Pdf.DesignSystem.Default` (new, net8.0) | — | Separate package per DS-01; templates as embedded HTML/CSS resources |
| BenchmarkDotNet harness | `benchmarks/Muonroi.Pdf.Benchmarks` (new, net8.0) | — | Isolated; never referenced by engine or tests |

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SG-01 | `IMPdfRenderer<TModel>` SG emits compile-time template implementations | IIncrementalGenerator + ForAttributeWithMetadataName pattern; marker attribute design below |
| SG-02 | SG warm throughput ≥3× runtime factory baseline | BenchmarkDotNet harness; SG path eliminates Scriban tokenization + string allocation; expect 5–10× on simple templates |
| SG-03 | Opting into SG requires no call-site code change | Generator emits a DI extension alongside the renderer; call `AddPdf()` is unchanged |
| AOT-01 | No reflection-emit in render hot path; `[DynamicallyAccessedMembers]` where required | OtelSetup.cs reflection at lines 45-49 is OUT OF ENGINE scope; engine hot path: `Activator.CreateInstance` absent; SixLabors.Fonts 2.1 has some reflection — annotate or suppress |
| AOT-02 | PublishAot Alpine sample renders golden corpus byte-identically | Isolated console sample; AngleSharp.Css beta.147 may produce trim warnings; IL-link suppressions file required |
| AOT-03 | Alpine AOT container <40 MB | linux-musl-x64 + StripSymbols=true achieves 7–18 MB for console apps per community evidence |
| DS-01 | `Muonroi.Pdf.DesignSystem.Default` ships invoice/receipt/report templates | Pure HTML/CSS embedded resources; no Chromium, no JS |
| DS-02 | All DS templates pass `IPdfCssPolicy.DefaultStrict` with zero violations | Templates must avoid: flex, grid, float, position:absolute/fixed/sticky, @keyframes, transitions, external @import, <script> |
| ALLOC-01 | Hot-path allocations ≥30% lower than v0.1 baseline (BenchmarkDotNet) | MemoryDiagnoser; targets: eliminate per-render `List<>` in PositionedPageList, ArrayPool<T> in InlineLayoutEngine, RecyclableMemoryStream for MemoryStream allocations |
</phase_requirements>

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.CodeAnalysis.CSharp | 4.13.0 (pinned in CPM) | Roslyn SG host APIs | Already in Directory.Packages.props; matches existing SG projects |
| Microsoft.CodeAnalysis.Analyzers | 3.11.0 (pinned in CPM) | Roslyn SG companion | Already pinned; required by `EnforceExtendedAnalyzerRules` |
| BenchmarkDotNet | 0.15.8 (NEW — add to CPM) | Allocation + throughput benchmarks | De-facto .NET benchmark standard; `[MemoryDiagnoser]` gives Allocated column |

[VERIFIED: nuget.org] BenchmarkDotNet 0.15.8 latest stable as of 2025-11-30.
[VERIFIED: nuget.org] Microsoft.CodeAnalysis.CSharp 4.13.0 already in Directory.Packages.props line 58.

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.IO.RecyclableMemoryStream | existing (check CPM) | Pooled MemoryStream for ALLOC-01 | Replace `new MemoryStream()` in per-render paths |

[ASSUMED] RecyclableMemoryStream may or may not already be in CPM — not confirmed by grep at research time.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ForAttributeWithMetadataName | CreateSyntaxProvider | ForAttributeWithMetadataName is 99x more efficient [CITED: roslyn cookbook] |
| PublishAot (full AOT) | PublishTrimmed only | AOT requires AngleSharp.Css trim-safety which is unverified [ASSUMED untrimmed-safe] |
| New marker attribute assembly | Attribute in Muonroi.Pdf.Abstractions | Avoids an extra package; netstandard2.0 already compatible with SG references |

**Installation (new packages only):**
```bash
# Add to Directory.Packages.props
<PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />

# benchmarks project references only
<PackageReference Include="BenchmarkDotNet" />
```

---

## Package Legitimacy Audit

> slopcheck was not available at research time. All new packages are tagged [ASSUMED] and the planner must gate each install behind a checkpoint:human-verify task.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| BenchmarkDotNet | NuGet | ~9 yrs | >50M total | github.com/dotnet/BenchmarkDotNet | [ASSUMED] | Approved — .NET Foundation project, universally known |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none (BenchmarkDotNet is .NET Foundation, not suspicious)

*All new packages tagged `[ASSUMED]` — planner must add `checkpoint:human-verify` before install.*

---

## Architecture Patterns

### System Architecture Diagram

```
[Consumer Project]
    |
    | compile-time (SG runs at build)
    v
[Muonroi.Pdf.SourceGenerators] ──forAttributeWithMetadataName──> discovers [PdfTemplate("id")] on TModel
    |
    | emits into consumer compilation
    v
[Generated: InvoiceRenderer.g.cs]
    implements IMPdfRenderer<InvoiceModel>
    | calls
    v
[IMPdfService.RenderAsync(interpolatedHtml, stream, opts)]
    |
    v (v0.1 pipeline unchanged)
[parse → cascade → policy → layout → write]

[Muonroi.Pdf.DesignSystem.Default]
    |
    | EmbeddedResource HTML/CSS
    v
[DesignSystemTemplateProvider.GetTemplate("invoice"|"receipt"|"report")]
    | validates against DefaultStrictPolicy at startup
    v
[caller's IMPdfRenderer<T> or IMPdfService]

[benchmarks/Muonroi.Pdf.Benchmarks]
    | BenchmarkRunner.Run<PdfRenderBenchmarks>()
    v
[PdfRenderBenchmarks]
    - [Benchmark] RuntimeFactory() — v0.1 baseline
    - [Benchmark] SourceGenerated() — SG path
    [MemoryDiagnoser] columns: Allocated, Gen0/1/2
```

### Recommended Project Structure
```
src/
├── Muonroi.Pdf.SourceGenerators/    # new — IIncrementalGenerator, netstandard2.0
│   ├── Muonroi.Pdf.SourceGenerators.csproj
│   ├── PdfTemplateGenerator.cs      # IIncrementalGenerator implementation
│   └── PdfTemplateGeneratorDiagnostics.cs
├── Muonroi.Pdf.DesignSystem.Default/ # new — net8.0
│   ├── Muonroi.Pdf.DesignSystem.Default.csproj
│   ├── Templates/
│   │   ├── invoice.html
│   │   ├── receipt.html
│   │   └── report.html
│   └── DesignSystemTemplateProvider.cs
samples/
├── Muonroi.Pdf.AotSample/           # new — net8.0, PublishAot=true
│   ├── Muonroi.Pdf.AotSample.csproj
│   ├── Program.cs
│   ├── Dockerfile
│   └── TrimmerRootDescriptor.xml
benchmarks/
└── Muonroi.Pdf.Benchmarks/          # new — net8.0
    ├── Muonroi.Pdf.Benchmarks.csproj
    └── PdfRenderBenchmarks.cs
```

### Pattern 1: IIncrementalGenerator with ForAttributeWithMetadataName

**What:** The SG scans for types annotated with `[PdfTemplate("templateId")]`, extracts the model type and template id, then emits a sealed class implementing `IMPdfRenderer<TModel>`.

**When to use:** Any time a consumer decorates a model class with `[PdfTemplate]`.

**Example (generator initialize):**
```csharp
// Source: roslyn incremental-generators.cookbook.md [CITED: github.com/dotnet/roslyn]
// Muonroi.Pdf.SourceGenerators/PdfTemplateGenerator.cs

[Generator]
public sealed class PdfTemplateGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Muonroi.Pdf.Abstractions.PdfTemplateAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: emit the marker attribute into every consuming compilation
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("PdfTemplateAttribute.g.cs", PdfTemplateAttributeSource));

        // Step 2: collect all types decorated with [PdfTemplate]
        IncrementalValuesProvider<PdfTemplateModel> models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => ExtractModel(ctx, ct))
            .Where(static m => m is not null)!;

        context.RegisterSourceOutput(models, static (spc, model) => Emit(spc, model));
    }
}
```

**Example (emitted renderer):**
```csharp
// Generated output for [PdfTemplate("invoice")] on InvoiceModel:
// InvoiceModelPdfRenderer.g.cs

namespace Muonroi.Pdf.Generated;

[System.CodeDom.Compiler.GeneratedCode("Muonroi.Pdf.SourceGenerators", "1.0.0")]
internal sealed class InvoiceModelPdfRenderer : global::Muonroi.Pdf.Abstractions.IMPdfRenderer<global::MyApp.InvoiceModel>
{
    private readonly global::Muonroi.Pdf.Abstractions.IMPdfService _service;

    public InvoiceModelPdfRenderer(global::Muonroi.Pdf.Abstractions.IMPdfService service)
        => _service = service;

    public string TemplateId => "invoice";

    public System.Threading.Tasks.Task<global::Muonroi.Pdf.Abstractions.PdfRenderResult> RenderAsync(
        global::MyApp.InvoiceModel model,
        System.IO.Stream destination,
        global::Muonroi.Pdf.Abstractions.PdfRenderOptions? options = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Compile-time string interpolation replaces Scriban tokenization entirely.
        // Token substitution uses model properties directly — no dictionary lookup.
        string html = $"""
            <!DOCTYPE html>
            <html>
            <!-- template content inlined at compile time -->
            <body>
            <h1>{model.Title}</h1>
            </body>
            </html>
            """;
        return _service.RenderAsync(html, destination, options ?? new(), cancellationToken);
    }
}
```

**SG csproj (mirrors Muonroi.Tenancy.SiteProfile.SourceGenerators exactly):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IsRoslynComponent>true</IsRoslynComponent>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
    <GenerateDependencyFile>false</GenerateDependencyFile>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
  </ItemGroup>
  <ItemGroup>
    <!-- NuGet packaging — analyzer goes in analyzers/dotnet/cs -->
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

**Consuming project wires the SG via:**
```xml
<ProjectReference Include="..\Muonroi.Pdf.SourceGenerators\Muonroi.Pdf.SourceGenerators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

### Pattern 2: AOT/Trim Annotation Strategy

**What:** Annotate the engine's reflection-free hot path with `[DynamicallyAccessedMembers]` where needed. Suppress unavoidable warnings from AngleSharp.Css via `TrimmerRootDescriptor.xml` in the AOT sample.

**When to use:** Any type in `Muonroi.Pdf` or `Muonroi.Pdf.Governance` that uses runtime type operations.

**Confirmed reflection call sites to fix for trim:**

| Location | Call | Action |
|----------|------|--------|
| `OtelSetup.cs` lines 45–49 | `AppDomain.CurrentDomain.GetAssemblies()` + `Activator.CreateInstance` | OUT OF SCOPE — `Muonroi.Observability` is not a Pdf package; the AOT sample must not reference it |
| `ITenantContextPolicy.cs` line 103 | `AppDomain.CurrentDomain.GetAssemblies()` | OUT OF SCOPE — not in Pdf packages |
| `Muonroi.Pdf.*` hot path | None found — `MPdfService`, layout engine, writer use no `Activator.CreateInstance` | No action needed in engine |
| `AngleSharp.Css` internally | Reflection for CSS property type lookup (beta — no `IsAotCompatible` annotation) | TrimmerRootDescriptor in AOT sample |

**Example TrimmerRootDescriptor.xml for AOT sample:**
```xml
<!-- samples/Muonroi.Pdf.AotSample/TrimmerRootDescriptor.xml -->
<linker>
  <!-- Preserve AngleSharp.Css internal registration types that use reflection -->
  <assembly fullname="AngleSharp.Css" preserve="all" />
  <assembly fullname="AngleSharp" preserve="all" />
</linker>
```

**AOT sample csproj:**
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <PublishAot>true</PublishAot>
  <RuntimeIdentifier>linux-musl-x64</RuntimeIdentifier>
  <StripSymbols>true</StripSymbols>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
<ItemGroup>
  <TrimmerRootDescriptor Include="TrimmerRootDescriptor.xml" />
</ItemGroup>
```

### Pattern 3: DefaultStrict-Compliant HTML/CSS Template

**What:** Templates for DS-01/DS-02 that use only the CSS subset allowed by `DefaultStrictPolicy`.

**When to use:** All three design system templates (invoice, receipt, report).

**Allowed CSS constructs** (verified from `DefaultStrictPolicy.cs`):
- `display: block`, `display: table`, `display: table-row`, `display: table-cell`, `display: inline`
- `border-collapse: separate` (not `collapse`)
- `position: static`, `position: relative`
- `margin`, `padding`, `border`, `width`, `height`, `color`, `background-color`, `font-*`
- `@page` rules, `page-break-before/after/inside`
- NO: `flex`, `grid`, `float`, `position:absolute/fixed/sticky`, `@keyframes`, `transition`, `@import` with external URIs, `<script>` elements

**Forbidden CSS that will fail the policy gate:**
- `display: flex` / `display: grid` — REJECTED
- `float: left/right` — REJECTED
- `position: absolute/fixed/sticky` — REJECTED
- `border-collapse: collapse` — REJECTED
- Animations / transitions — REJECTED
- External @import — REJECTED

### Anti-Patterns to Avoid

- **Using `CreateSyntaxProvider` instead of `ForAttributeWithMetadataName`**: 99x slower; causes unnecessary IDE churn
- **Placing the marker attribute in the SG project itself**: netstandard2.0 SG project cannot be referenced directly by consumers for the attribute type — use `RegisterPostInitializationOutput` to emit it, OR place it in `Muonroi.Pdf.Abstractions` (preferred because Abstractions is already netstandard2.0)
- **Setting `IncludeBuildOutput=false` on the SG csproj**: breaks P2P analyzer resolution (confirmed by comment in `Muonroi.Tenancy.SiteProfile.SourceGenerators.csproj` — do NOT set this)
- **Referencing `Muonroi.Observability` from the AOT sample**: pulls in `OtelSetup` with hard AOT-incompatible reflection — AOT sample must use a minimal DI setup
- **Putting BenchmarkDotNet in the main solution without `IsPackable=false`**: benchmark project must never be published as NuGet
- **Using `display: flex` in design system templates**: fails `DefaultStrictPolicy` — use `display: table` for two-column layouts
- **Testing benchmarks with `dotnet test`**: BenchmarkDotNet benchmarks are run via `dotnet run -c Release`, not `dotnet test`

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Allocation measurement | Custom GC counter | `[MemoryDiagnoser]` on BenchmarkDotNet class | `GC.GetAllocatedBytesForCurrentThread()` is the correct cross-platform API; BDN wraps it correctly |
| SG test harness | Custom compilation runner | `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` (already in CPM) | InMemory compilation testing; CPM already pins `1.1.2` |
| Template token substitution in SG path | String.Replace loops | C# string interpolation in generated code | Compile-time interpolation has zero parse overhead; eliminates Scriban entirely on the SG path |
| AOT linker root files | Guessing what AngleSharp needs | `preserve="all"` on AngleSharp assemblies in TrimmerRootDescriptor | Safe-overshoot until AngleSharp adds IsAotCompatible |
| Stopwatch-based allocation measurement | Manual GC.GetTotalMemory diffs | BenchmarkDotNet MemoryDiagnoser | Hand-rolled alloc measurement is inaccurate across GC generations |

**Key insight:** The SG path's 3× speedup comes from eliminating Scriban tokenization and dictionary-based token substitution, not from any framework magic. The generated code calls `IMPdfService` directly with a pre-built HTML string — the engine pipeline is identical. BenchmarkDotNet will measure the difference cleanly.

---

## Common Pitfalls

### Pitfall 1: IncludeBuildOutput=false Breaks P2P Analyzer Resolution
**What goes wrong:** Setting `<IncludeBuildOutput>false</IncludeBuildOutput>` on the SG csproj causes `GetTargetPath` to return empty, so `OutputItemType="Analyzer"` resolves to nothing in P2P references.
**Why it happens:** The property was originally added to prevent the SG DLL being included as a lib reference in NuGet packages; but it also breaks build-time discovery via P2P.
**How to avoid:** Do NOT set `IncludeBuildOutput=false`. Use `<None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" />` for NuGet packaging instead.
**Warning signs:** SG produces no output files; generator `Initialize` is never called.
**Source:** Confirmed by comment in `src/Muonroi.Tenancy.SiteProfile.SourceGenerators/Muonroi.Tenancy.SiteProfile.SourceGenerators.csproj`.

### Pitfall 2: AngleSharp.Css Reflection at Trim Time
**What goes wrong:** Publishing with `PublishTrimmed=true` or `PublishAot=true` produces IL2026/IL2055/IL3050 warnings from AngleSharp.Css internal CSS property registration (uses `Type.GetProperties()` reflectively).
**Why it happens:** AngleSharp.Css 1.0.0-beta.147 has no `IsAotCompatible` metadata — confirmed by NuGet registry scan. [ASSUMED: internal reflection exists based on library architecture; not directly verified from source]
**How to avoid:** Add `<TrimmerRootDescriptor>` preserving the AngleSharp assemblies in the AOT sample. Do not set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` for trim warnings in the sample.
**Warning signs:** `dotnet publish -r linux-musl-x64 -c Release` emits `warning IL2026` mentioning AngleSharp types.

### Pitfall 3: SG Template Content Strategy — Embedded vs. Interpolated
**What goes wrong:** If template HTML is embedded as a resource and loaded at runtime, the SG path provides no throughput advantage — the parse cost is identical to the runtime factory path.
**Why it happens:** The 3× speedup comes from pre-resolving token positions at compile time. If the SG just calls `_service.RenderAsync(LoadResource("invoice.html"), ...)`, it is identical to the runtime factory.
**How to avoid:** The SG must inline the template HTML as a C# string literal (or string interpolation) in the generated `RenderAsync` method. Template tokens (`{{model.Total}}`) become `{model.Total}` in the generated C# interpolated string.
**Warning signs:** BenchmarkDotNet shows SG path taking the same time as runtime factory.

### Pitfall 4: BenchmarkDotNet Project Builds Slow
**What goes wrong:** Including the benchmarks project in the main solution build causes every `dotnet build` to compile BenchmarkDotNet's large transitive graph.
**Why it happens:** BDN has many dependencies.
**How to avoid:** Keep `benchmarks/Muonroi.Pdf.Benchmarks/` as a separate directory. Either exclude it from `Muonroi.BuildingBlock.sln` or add it only to a separate `Benchmarks.slnf` solution filter. Never add it to `Directory.Build.targets` test detection (it must not be treated as a test project).

### Pitfall 5: Design System Templates Using Forbidden CSS
**What goes wrong:** Invoice/receipt/report templates use `display:flex` for two-column layouts or `position:absolute` for watermarks — both fail `DefaultStrictPolicy` and throw `PdfPolicyException` at render time.
**Why it happens:** Template authors default to modern CSS grid/flex; the policy only allows CSS 2.1 table and block layout.
**How to avoid:** Use `display:table` / `display:table-row` / `display:table-cell` for multi-column layouts. Use `display:block` with `width` percentages for sidebars. See `DefaultStrictPolicy.cs` for the complete allowlist.
**Warning signs:** DS-02 validation test throws `PdfPolicyException` with `forbidden.display.flex` violation code.

### Pitfall 6: Alpine AOT Build Requires musl Prerequisites
**What goes wrong:** `dotnet publish -r linux-musl-x64 -c Release` fails with linker errors inside the Docker build stage.
**Why it happens:** NativeAOT on Alpine requires `clang`, `build-base`, and `zlib-dev` installed in the SDK stage.
**How to avoid:** Dockerfile SDK stage must run `apk add clang build-base zlib-dev` before `dotnet publish`.
**Warning signs:** Docker build fails with `error: ld returned 1 exit status` or `clang not found`.

---

## Code Examples

### BenchmarkDotNet Harness

```csharp
// benchmarks/Muonroi.Pdf.Benchmarks/PdfRenderBenchmarks.cs
// Source: benchmarkdotnet.org/articles/configs/diagnosers.html [CITED]
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Muonroi.Pdf.Abstractions;

[MemoryDiagnoser]
[SimpleJob]
public class PdfRenderBenchmarks
{
    private IMPdfService _service = null!;
    private IMPdfRenderer<InvoiceModel> _runtimeRenderer = null!;
    private IMPdfRenderer<InvoiceModel> _sgRenderer = null!;
    private InvoiceModel _model = null!;
    private string _html50kb = null!;

    [GlobalSetup]
    public void Setup()
    {
        // wire via DI or directly construct; load 50kb reference template
    }

    [Benchmark(Baseline = true)]
    public async Task RuntimeFactory()
    {
        using var ms = new System.IO.MemoryStream();
        await _runtimeRenderer.RenderAsync(_model, ms);
    }

    [Benchmark]
    public async Task SourceGenerated()
    {
        using var ms = new System.IO.MemoryStream();
        await _sgRenderer.RenderAsync(_model, ms);
    }
}
```

### Alpine Dockerfile (Multi-stage)

```dockerfile
# samples/Muonroi.Pdf.AotSample/Dockerfile
# Build INSIDE Docker — only docker.exe available on host Windows
# docker.exe full path: "C:\Program Files\Docker\Docker\resources\bin\docker.exe"

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# NativeAOT prerequisites on Alpine [CITED: learn.microsoft.com/dotnet/core/deploying/native-aot]
RUN apk add clang build-base zlib-dev

COPY . .
RUN dotnet publish samples/Muonroi.Pdf.AotSample/Muonroi.Pdf.AotSample.csproj \
    -r linux-musl-x64 \
    -c Release \
    --self-contained \
    -o /app/publish \
    -m:1 -nodereuse:false

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["/app/Muonroi.Pdf.AotSample"]
```

### DefaultStrictPolicy-Compliant Invoice Template Skeleton

```html
<!-- src/Muonroi.Pdf.DesignSystem.Default/Templates/invoice.html -->
<!DOCTYPE html>
<html>
<head>
<style>
/* ALLOWED: block, table, table-row, table-cell, inline, static/relative position */
/* FORBIDDEN: flex, grid, float, absolute/fixed/sticky, @keyframes, transition */
body { font-family: Arial, sans-serif; margin: 0; padding: 0; }

.invoice-header {
  display: table;
  width: 100%;
  border-collapse: separate; /* collapse is FORBIDDEN */
}
.invoice-header-left  { display: table-cell; width: 60%; }
.invoice-header-right { display: table-cell; width: 40%; text-align: right; }

table.line-items { width: 100%; border-collapse: separate; border-spacing: 0; }
table.line-items th { background-color: #333; color: #fff; padding: 4pt; }
table.line-items td { padding: 4pt; border-bottom: 1px solid #ccc; }
</style>
</head>
<body>
<!-- Token placeholders are replaced by IMPdfRenderer<InvoiceModel> at compile time -->
<div class="invoice-header">
  <div class="invoice-header-left">{{CompanyName}}</div>
  <div class="invoice-header-right">INVOICE #{{InvoiceNumber}}</div>
</div>
<table class="line-items">
  <thead><tr><th>Description</th><th>Amount</th></tr></thead>
  <tbody>{{LineItemsRows}}</tbody>
</table>
</body>
</html>
```

### SG Marker Attribute (emitted via RegisterPostInitializationOutput)

```csharp
// Emitted by generator OR placed directly in Muonroi.Pdf.Abstractions

namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Marks a model class for compile-time IMPdfRenderer&lt;TModel&gt; generation.
/// The generator emits a sealed renderer class that inlines the template HTML
/// as a C# string interpolation — zero Scriban overhead at render time.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct,
    Inherited = false, AllowMultiple = false)]
public sealed class PdfTemplateAttribute : System.Attribute
{
    /// <param name="templateId">Stable identifier matching IMPdfRenderer.TemplateId.</param>
    /// <param name="templateResourceName">
    /// Embedded resource name in the calling assembly for the HTML template,
    /// OR null to use code-gen inline interpolation.
    /// </param>
    public PdfTemplateAttribute(string templateId, string? templateResourceName = null)
    {
        TemplateId = templateId;
        TemplateResourceName = templateResourceName;
    }
    public string TemplateId { get; }
    public string? TemplateResourceName { get; }
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ISourceGenerator` (V1 SG) | `IIncrementalGenerator` (V2 SG) | Roslyn SDK 4.x | Mandatory for incremental; V1 SGs are deprecated and will be removed |
| `SyntaxProvider.CreateSyntaxProvider` | `ForAttributeWithMetadataName` | Roslyn 4.4 (SDK 7+) | 99x performance improvement [CITED: roslyn cookbook] |
| `PublishSingleFile` for self-contained | `PublishAot` | .NET 7+ | True AOT; no JIT; smaller startup; different limitation set |
| `IncludeBuildOutput=false` on SG projects | Omit it; use explicit `None` Pack items | Confirmed in this codebase | P2P resolution works correctly |

**Deprecated/outdated:**
- `ISourceGenerator` interface: do not implement — use `IIncrementalGenerator` only
- `SyntaxProvider.CreateSyntaxProvider` without attribute filtering: 99x slower than `ForAttributeWithMetadataName`; use only when no attribute exists

---

## Runtime State Inventory

> Phase 8 is a greenfield addition phase (new projects, new packages). No rename or migration is involved. Category answers are explicit.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | None — no database-stored templates in v0.1 | None |
| Live service config | None — no external service config referencing Pdf package names | None |
| OS-registered state | None | None |
| Secrets/env vars | None | None |
| Build artifacts | `Directory.Packages.props` must be updated with BenchmarkDotNet 0.15.8 | Add `<PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />` |

---

## Open Questions

1. **SG template content delivery strategy**
   - What we know: The SG emits a renderer class. The template HTML must come from somewhere at emit time.
   - What's unclear: Does the template HTML live as an embedded resource in `Muonroi.Pdf.DesignSystem.Default` and get loaded at render time (simple, no SG advantage over runtime factory on the parse step), or does the SG read it from `AdditionalFiles` and inline it as a C# string literal (full speedup, but SG must read files)?
   - Recommendation: Use `AdditionalFiles` to pass the `.html` template into the SG at build time. The SG inlines it as a C# verbatim string. This is the only way to achieve the 3× speedup required by SG-02.

2. **PublishAot vs PublishTrimmed scope**
   - What we know: AngleSharp.Css has no `IsAotCompatible` metadata. Full AOT will produce warnings. The AOT sample can suppress them via TrimmerRootDescriptor.
   - What's unclear: Whether IL-link suppressions are sufficient for the binary to actually function correctly (warnings are emitted but behavior may still break at runtime if wrong types are trimmed).
   - Recommendation: Plan AOT-02 as an exploratory wave. If the golden corpus fails under AOT, fall back to `PublishTrimmed=true` (trim-only, JIT-present) and update the requirement claim accordingly. Flag this to the user before committing to AOT-02 success criterion.

3. **`Muonroi.Pdf.SourceGenerators` csproj registration in solution**
   - What we know: The `.sln` currently has no Phase 8 projects.
   - What's unclear: Whether the SG project goes into the main `Muonroi.BuildingBlock.sln` or a separate sln filter.
   - Recommendation: Add to `Muonroi.BuildingBlock.sln`. The existing SG projects (`Muonroi.Diagnostics.Generator`, `Muonroi.Tenancy.SiteProfile.SourceGenerators`) are already in the solution.

4. **ALLOC-01 baseline measurement**
   - What we know: The v0.1 `PerfGateTests.cs` uses Stopwatch — not allocation-aware.
   - What's unclear: The exact v0.1 baseline allocation number in bytes/op. It must be measured before optimizations begin.
   - Recommendation: Wave 1 of benchmarks captures the baseline. Allocations ≥30% lower is measured against that captured baseline, not a pre-defined number.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Docker | AOT-03 (Alpine build) | ✓ | full path: `"C:\Program Files\Docker\Docker\resources\bin\docker.exe"` | None — AOT-03 requires Docker |
| dotnet SDK 8 | All | ✓ | net8.0 (confirmed from all csproj files) | — |
| clang / build-base / zlib-dev | AOT build inside Docker | ✓ (installed in Docker image via `apk add`) | Alpine package | None needed outside Docker |

**Missing dependencies with no fallback:**
- Docker must be invoked via full path `"C:\Program Files\Docker\Docker\resources\bin\docker.exe"` on Windows host. Do not rely on `docker` being on PATH.

**Missing dependencies with fallback:**
- None for Wave 1 (SG + DS). AOT work (Wave 3) requires Docker.

---

## Validation Architecture

> `nyquist_validation` is absent from `.planning/config.json` — treated as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 (existing) |
| Config file | `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` |
| Quick run command | `dotnet test tests/Muonroi.Pdf.Tests -m:1 -nodereuse:false --filter "Category!=SlowIntegration" -c Release` |
| Full suite command | `dotnet test tests/Muonroi.Pdf.Tests -m:1 -nodereuse:false -c Release` |
| Benchmark run command | `dotnet run -c Release --project benchmarks/Muonroi.Pdf.Benchmarks` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SG-01 | SG emits `IMPdfRenderer<TModel>` for `[PdfTemplate]` decorated class | unit (SG analyzer test) | `dotnet test tests/Muonroi.Pdf.SourceGenerators.Tests` | ❌ Wave 0 |
| SG-02 | SG warm throughput ≥3× runtime factory | benchmark | `dotnet run -c Release --project benchmarks/Muonroi.Pdf.Benchmarks` | ❌ Wave 0 |
| SG-03 | No call-site change required | integration | existing Service tests with SG path wired | ❌ Wave 0 |
| AOT-01 | No IL2026/IL3050 in engine hot path | build-time (trim warnings = 0) | `dotnet publish -r linux-musl-x64 -c Release` in Docker | ❌ Wave 0 |
| AOT-02 | Golden corpus passes under AOT binary | smoke | Docker run + corpus compare script | ❌ Wave 0 |
| AOT-03 | Docker image <40 MB | smoke | `docker image inspect ... --format='{{.Size}}'` | ❌ Wave 0 |
| DS-01 | 3 templates exist and render non-empty PDF | smoke | `dotnet test tests/Muonroi.Pdf.Tests --filter "DesignSystem"` | ❌ Wave 0 |
| DS-02 | All DS templates pass DefaultStrictPolicy | unit | `dotnet test tests/Muonroi.Pdf.Tests --filter "DesignSystem"` | ❌ Wave 0 |
| ALLOC-01 | Allocation ≥30% lower than baseline | benchmark | `dotnet run -c Release --project benchmarks/Muonroi.Pdf.Benchmarks` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Muonroi.Pdf.Tests -m:1 -nodereuse:false --filter "Category!=SlowIntegration" -c Release`
- **Per wave merge:** `dotnet test tests/Muonroi.Pdf.Tests -m:1 -nodereuse:false -c Release`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `tests/Muonroi.Pdf.SourceGenerators.Tests/` — SG generator tests project (scaffold + first SG unit test)
- [ ] `benchmarks/Muonroi.Pdf.Benchmarks/` — benchmark project + baseline capture
- [ ] `src/Muonroi.Pdf.SourceGenerators/` — SG project scaffold
- [ ] `src/Muonroi.Pdf.DesignSystem.Default/` — DS project scaffold
- [ ] `samples/Muonroi.Pdf.AotSample/` — AOT console sample scaffold

---

## Security Domain

> `security_enforcement` not explicitly set in config.json — treated as enabled.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | N/A — library, not a web app |
| V3 Session Management | no | N/A |
| V4 Access Control | no | N/A |
| V5 Input Validation | yes (SG templates) | `[PdfTemplate]` attribute inputs validated by SG at compile time; runtime HTML still goes through `DefaultStrictPolicy` |
| V6 Cryptography | no | N/A — no crypto in Phase 8 scope |

### Known Threat Patterns for SG + Templates

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Template injection via model properties | Tampering | C# string interpolation encodes at compile time; model properties are typed C# — not arbitrary strings passed to Scriban tokenizer |
| Embedded template containing `<script>` | Tampering | `DefaultStrictPolicy` SEC-05 rejects `<script>` elements at policy gate — applies to SG-emitted HTML identically to runtime path |
| AOT sample exposing file:// paths | Information Disclosure | `ThrowingResourceResolver` default still applies; AOT binary includes it |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | AngleSharp.Css 1.0.0-beta.147 uses internal reflection for CSS property registration and will produce trim warnings | AOT Pitfalls | If wrong (library is trim-safe), `preserve="all"` is unnecessary overhead but not harmful; AOT image may be slightly larger |
| A2 | The 3× SG speedup is achievable via compile-time interpolation eliminating Scriban | SG-02 / Code Examples | If wrong (engine pipeline dominates, Scriban is negligible), the ratio may be 1.5× not 3×; requirement SG-02 may not be met |
| A3 | `RecyclableMemoryStream` will provide meaningful allocation reduction for ALLOC-01 | ALLOC-01 | Must measure baseline first; if MemoryStream is not the dominant allocator, different optimization targets needed |
| A4 | BenchmarkDotNet 0.15.8 supports net8.0 target | Standard Stack | Confirmed from nuget.org (`supports .NET 6.0 and .NET Standard 2.0`) — net8.0 is compatible but not explicitly listed |
| A5 | Docker available and functional on build host | AOT-03 | If Docker daemon not running, AOT-03 is blocked; fallback is to run `dotnet publish` directly on WSL2/Alpine without Docker |

---

## Sources

### Primary (HIGH confidence)
- `src/Muonroi.Tenancy.SiteProfile.SourceGenerators/Muonroi.Tenancy.SiteProfile.SourceGenerators.csproj` — SG csproj pattern (IncludeBuildOutput comment, None Pack items)
- `src/Muonroi.Tenancy.SiteProfile.SourceGenerators/SiteProfileRegistrationGenerator.cs` — `IIncrementalGenerator` with `CreateSyntaxProvider` + `RegisterSourceOutput` pattern
- `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` — full list of forbidden CSS for DS-02
- `src/Muonroi.Observability/OtelSetup.cs` — confirmed reflection call sites (lines 45–49) that block AOT
- `Directory.Packages.props` — confirmed `Microsoft.CodeAnalysis.CSharp 4.13.0`, `AngleSharp.Css 1.0.0-beta.147`, no BenchmarkDotNet
- `tests/Muonroi.Pdf.Tests/Performance/PerfGateTests.cs` — existing perf gate (Stopwatch, not allocation-aware)

### Secondary (MEDIUM confidence)
- [learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) — Alpine NativeAOT prerequisites (`apk add clang build-base zlib-dev`), AOT limitations, `IsAotCompatible` property
- [github.com/dotnet/roslyn cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md) — `ForAttributeWithMetadataName` 99x performance, NuGet packaging pattern
- [benchmarkdotnet.org/articles/configs/diagnosers.html](https://benchmarkdotnet.org/articles/configs/diagnosers.html) — `[MemoryDiagnoser]` usage

### Tertiary (LOW confidence)
- Community evidence: Alpine NativeAOT images achieve 7–18 MB for console apps (from medium.com/docker-nativeaot article — single source, unverified)
- AngleSharp.Css trim status: searched GitHub issues and NuGet metadata; no official confirmation of trim safety or unsafety; marked [ASSUMED] throughout

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — CPM file read directly; BenchmarkDotNet version verified from nuget.org
- Source Generator patterns: HIGH — three existing SG projects in this repo verified
- Architecture (SG): HIGH — mirrors existing SiteProfile SG exactly
- AOT/Trim: MEDIUM — reflection call sites confirmed; AngleSharp.Css trim status [ASSUMED]
- DesignSystem: HIGH — DefaultStrictPolicy rules read from source; template design follows directly
- ALLOC-01: MEDIUM — BDN approach confirmed; specific optimization targets [ASSUMED] until baseline measured

**Research date:** 2026-05-27
**Valid until:** 2026-06-27 (30 days — AngleSharp.Css betas release frequently; re-check beta version before executing AOT wave)
