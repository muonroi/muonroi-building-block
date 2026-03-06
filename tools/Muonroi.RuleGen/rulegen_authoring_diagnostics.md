# RuleGen Authoring Diagnostics (Visual Studio + VSCode)

This repo now exposes Roslyn diagnostics from `Muonroi.RuleEngine.SourceGenerators` to catch high-risk authoring mistakes in large `[MExtractAsRule]` classes.

## Supported diagnostics

- `MRG001`: duplicate rule code.
- `MRG005`: `DependsOn` references a missing rule code.
- `MRG006`: `Order > 1` but no `DependsOn` (scheduler uses dependency graph, not `Order`).
- `MRG007`: rule reads a `FactBag` key but has no dependency path to any producer rule.
- `MRG008`: nullable value assigned to non-nullable string property (common protobuf `value` null crash).

## Works in both IDEs

These are Roslyn diagnostics, so they show as squiggles and build warnings/errors in:

- Visual Studio 2022+
- VSCode + C# Dev Kit / C# extension

## How to enable in a consuming project

Add package reference:

```xml
<ItemGroup>
  <PackageReference Include="Muonroi.RuleEngine.SourceGenerators" Version="0.1.1" PrivateAssets="all" />
</ItemGroup>
```

(Optional) enforce severities in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.MRG001.severity = error
dotnet_diagnostic.MRG005.severity = warning
dotnet_diagnostic.MRG006.severity = warning
dotnet_diagnostic.MRG007.severity = warning
dotnet_diagnostic.MRG008.severity = warning
```

(Optional) fail CI for selected rules in `Directory.Build.props`:

```xml
<PropertyGroup>
  <WarningsAsErrors>$(WarningsAsErrors);MRG001</WarningsAsErrors>
</PropertyGroup>
```

## Recommended workflow

1. Author rule methods in a large source class.
2. Fix any MRG diagnostics surfaced by IDE.
3. Run RuleGen extract/register.
4. Build and run parity/integration tests.
