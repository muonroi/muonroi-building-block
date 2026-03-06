# Muonroi RuleGen VSIX

Visual Studio 2022 extension that adds Solution Explorer context menu commands:

- `Muonroi RuleGen - Extract`
- `Muonroi RuleGen - Merge`
- `Muonroi RuleGen - Watch`
- `Muonroi RuleGen - Stop Watch`

Each command opens a single grid dialog (one-shot input) to:
- set input/output paths,
- set common CLI options with defaults,
- append raw additional options,
- then run once.

Default behavior highlights:
- `extract` defaults output to sibling `Rules` folder next to selected class/file.
- `extract` defaults namespace to `<SelectedClassNamespace>.Rules` when resolvable.
- `organize-by-namespace` default is `false` (avoid nested namespace folders unless explicitly enabled).

## Build

```powershell
cd D:\sources\Core\MuonroiBuildingBlock\tools\Muonroi.RuleGen.VisualStudio
.\build-vsix.cmd
```

VSIX output (default):

- `bin\Release\Muonroi.RuleGen.VisualStudio.vsix`

## Install

1. Double click the generated `.vsix`.
2. Close all Visual Studio instances.
3. Install extension.
4. Re-open Visual Studio.

## Configure executable path (optional)

`Tools -> Options -> Muonroi -> RuleGen`

- **Executable Path**: full path to `muonroi-rule.exe` or `Muonroi.RuleGen.dll`.
- If empty, extension auto-detects in this order:
  1. `MUONROI_RULEGEN_EXE` environment variable
  2. current repo `tools/Muonroi.RuleGen/Muonroi.RuleGen.csproj` (run via `dotnet run`)
  3. `dotnet tool run muonroi-rule`

## Merge behavior

`Merge` supports 3 modes via prompt:

- `generated`: merge from `*.g.cs` rule files (`--rules-dir`)
- `attribute`: scan a source folder and merge methods marked `[MExtractAsRule]` (`--source-dir`)
- `json`: merge runtime JSON (`--rules-json`)

Example generated mode:

```text
muonroi-rule merge --rules-dir <Rules> --target <TargetFile> --compile-check
```
