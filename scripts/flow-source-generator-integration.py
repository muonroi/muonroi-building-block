import argparse
from pathlib import Path

from _flow_common import (
    add_case,
    find_latest_local_package_version,
    iso_utc_now,
    resolve_path,
    run_command,
    timestamp_slug,
    write_json_file,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Source generator integration supplement flow for G21-G24.")
    parser.add_argument(
        "--source-generator-project",
        default=r"D:\sources\Core\MuonroiBuildingBlock\src\Muonroi.RuleEngine.SourceGenerators\Muonroi.RuleEngine.SourceGenerators.csproj",
    )
    parser.add_argument("--local-nuget-path", default=r"D:\sources\Core\LocalNuget")
    parser.add_argument("--output-path")
    return parser.parse_args()


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def main() -> int:
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    workspace_root = script_dir.parent

    source_generator_project = resolve_path(args.source_generator_project)
    if source_generator_project is None or not source_generator_project.exists():
        raise RuntimeError(f"SourceGeneratorProject not found: {args.source_generator_project}")

    local_nuget = resolve_path(args.local_nuget_path)
    if local_nuget is None or not local_nuget.exists():
        raise RuntimeError(f"LocalNugetPath not found: {local_nuget}")

    if args.output_path:
        output_path = resolve_path(args.output_path)
        if output_path is None:
            raise RuntimeError("OutputPath is invalid.")
    else:
        output_path = workspace_root / "_tmp" / f"source_generator_integration_{timestamp_slug()}.json"

    temp_root = workspace_root / "_tmp" / f"source_generator_integration_{timestamp_slug()}"
    temp_root.mkdir(parents=True, exist_ok=True)
    package_version = f"0.1.1-local.{timestamp_slug().replace('_', '')}"

    cases: list[dict] = []

    pack_result = run_command(
        [
            "dotnet",
            "pack",
            str(source_generator_project),
            "-c",
            "Release",
            "-o",
            str(local_nuget),
            f"-p:PackageVersion={package_version}",
        ],
        source_generator_project.parent,
    )
    pack_pass = pack_result["ExitCode"] == 0
    add_case(
        cases,
        "PREP_PACK",
        "Pack SourceGenerator",
        pack_pass,
        "dotnet pack exit code 0",
        f"ExitCode={pack_result['ExitCode']}",
        pack_result,
    )
    if not pack_pass:
        raise RuntimeError("dotnet pack failed for source generator.")

    abstraction_version = find_latest_local_package_version(local_nuget, "Muonroi.RuleEngine.Abstractions") or "0.1.1"
    source_generator_version = package_version

    nuget_config = f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="{local_nuget}" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"""
    (temp_root / "NuGet.config").write_text(nuget_config, encoding="utf-8")

    probe_dir = temp_root / "SourceGeneratorProbe"
    new_proj = run_command(
        ["dotnet", "new", "classlib", "-n", "SourceGeneratorProbe", "-f", "net9.0", "-o", str(probe_dir)],
        temp_root,
    )
    if new_proj["ExitCode"] != 0:
        raise RuntimeError("Cannot create source generator probe project.")

    probe_csproj = probe_dir / "SourceGeneratorProbe.csproj"
    probe_csproj.write_text(
        f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Muonroi.RuleEngine.Abstractions" Version="{abstraction_version}" />
    <PackageReference Include="Muonroi.RuleEngine.SourceGenerators" Version="{source_generator_version}">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.8" />
  </ItemGroup>
</Project>
""",
        encoding="utf-8",
    )

    sample_path = probe_dir / "SampleHandler.cs"
    sample_path.write_text(
        """using System.Threading;
using System.Threading.Tasks;
using Muonroi.RuleEngine.Abstractions;

public sealed class SgContext
{
    public int Amount { get; set; }
}

public partial class SampleHandler
{
    [MExtractAsRule("TEST_RULE", Order = 1, HookPoint = HookPoint.BeforeRule)]
    public Task<RuleResult> HandleA(SgContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["ok"] = ctx.Amount > 0;
        return Task.FromResult(RuleResult.Passed());
    }
}

public sealed class ManualRule : IRule<SgContext>
{
    public string Code => "MANUAL_RULE";
}
""",
        encoding="utf-8",
    )

    build_clean = run_command(["dotnet", "build", str(probe_csproj), "-c", "Debug"], temp_root)
    g21_pass = build_clean["ExitCode"] == 0
    add_case(
        cases,
        "G21",
        "Source Generator compile",
        g21_pass,
        "Probe project builds with 0 errors",
        f"ExitCode={build_clean['ExitCode']}",
        build_clean,
    )

    generated_root = probe_dir / "obj" / "Generated"
    generated_files = list(generated_root.rglob("*.g.cs")) if generated_root.exists() else []
    contains_generated_rule = False
    contains_registration = False
    for file_path in generated_files:
        text = _read_text(file_path)
        if "IRule<" in text and "TEST_RULE" in text:
            contains_generated_rule = True
        if "AddGeneratedRules(" in text:
            contains_registration = True

    g23_pass = g21_pass and contains_generated_rule
    add_case(
        cases,
        "G23",
        "Generated rule IntelliSense artifact",
        g23_pass,
        "Generated .g.cs contains IRule< and TEST_RULE",
        f"GeneratedFiles={len(generated_files)}; ContainsGeneratedRule={contains_generated_rule}",
        [str(path) for path in generated_files],
    )

    g24_pass = g21_pass and contains_registration
    add_case(
        cases,
        "G24",
        "RuleRegistrationGenerator AddGeneratedRules",
        g24_pass,
        "Generated code contains AddGeneratedRules extension method",
        f"ContainsRegistration={contains_registration}",
        [str(path) for path in generated_files],
    )

    with sample_path.open("a", encoding="utf-8") as append_file:
        append_file.write(
            """

public partial class SampleHandler
{
    [MExtractAsRule("TEST_RULE", Order = 2, HookPoint = HookPoint.BeforeRule)]
    public Task<RuleResult> HandleB(SgContext ctx, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }
}
"""
        )

    build_dup = run_command(["dotnet", "build", str(probe_csproj), "-c", "Debug"], temp_root)
    contains_mrg001 = "MRG001" in build_dup["Output"]
    g22_pass = contains_mrg001
    add_case(
        cases,
        "G22",
        "MRG001 diagnostic duplicate code",
        g22_pass,
        "Build output contains MRG001 for duplicate rule code",
        f"ExitCode={build_dup['ExitCode']}; ContainsMRG001={contains_mrg001}",
        build_dup,
    )

    overall_pass = all(case["Pass"] for case in cases)
    evidence = {
        "OverallStatus": "PASS" if overall_pass else "FAIL",
        "SourceGeneratorProject": str(source_generator_project),
        "LocalNugetPath": str(local_nuget),
        "TempRoot": str(temp_root),
        "ProbeProject": str(probe_csproj),
        "Cases": cases,
        "CompletedAtUtc": iso_utc_now(),
    }
    write_json_file(output_path, evidence)

    if overall_pass:
        print("Source generator integration flow: PASS")
    else:
        print("Source generator integration flow: FAIL")
    print(f"Evidence: {output_path}")
    return 0 if overall_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
