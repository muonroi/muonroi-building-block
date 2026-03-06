import argparse
import shutil
import subprocess
import time
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
    parser = argparse.ArgumentParser(description="RuleGen CLI v2 supplement flow for G13-G20.")
    parser.add_argument(
        "--rulegen-project",
        default=r"D:\sources\Core\MuonroiBuildingBlock\tools\Muonroi.RuleGen\Muonroi.RuleGen.csproj",
    )
    parser.add_argument("--working-dir")
    parser.add_argument("--local-nuget-path", default=r"D:\sources\Core\LocalNuget")
    parser.add_argument("--output-path")
    return parser.parse_args()


def wait_for_glob(root: Path, pattern: str, timeout_seconds: int = 30) -> list[Path]:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        files = list(root.rglob(pattern))
        if files:
            return files
        time.sleep(0.4)
    return []


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def main() -> int:
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    workspace_root = script_dir.parent

    rulegen_project = resolve_path(args.rulegen_project)
    if rulegen_project is None or not rulegen_project.exists():
        raise RuntimeError(f"RuleGenProject not found: {args.rulegen_project}")

    working_dir = resolve_path(args.working_dir) if args.working_dir else rulegen_project.parent
    if working_dir is None or not working_dir.exists():
        raise RuntimeError(f"WorkingDir not found: {working_dir}")

    local_nuget = resolve_path(args.local_nuget_path)
    if local_nuget is None or not local_nuget.exists():
        raise RuntimeError(f"LocalNugetPath not found: {local_nuget}")

    if args.output_path:
        output_path = resolve_path(args.output_path)
        if output_path is None:
            raise RuntimeError("OutputPath is invalid.")
    else:
        output_path = workspace_root / "_tmp" / f"{rulegen_project.stem}_v2_behaviors_{timestamp_slug()}.json"

    temp_root = workspace_root / "_tmp" / f"rulegen_cli_v2_{timestamp_slug()}"
    temp_root.mkdir(parents=True, exist_ok=True)

    cases: list[dict] = []
    watch_proc = None
    watch_stdout_handle = None
    watch_stderr_handle = None

    try:
        build_rulegen = run_command(["dotnet", "build", str(rulegen_project), "-c", "Release"], working_dir)
        prep_pass = build_rulegen["ExitCode"] == 0
        add_case(cases, "PREP", "Build RuleGen CLI", prep_pass, "dotnet build exit code 0", f"ExitCode={build_rulegen['ExitCode']}", build_rulegen)
        if not prep_pass:
            raise RuntimeError("RuleGen build failed.")

        abstraction_version = find_latest_local_package_version(local_nuget, "Muonroi.RuleEngine.Abstractions") or "0.1.1"

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

        probe_dir = temp_root / "RuleGenCliProbe"
        new_proj = run_command(
            ["dotnet", "new", "classlib", "-n", "RuleGenCliProbe", "-f", "net9.0", "-o", str(probe_dir)],
            temp_root,
        )
        if new_proj["ExitCode"] != 0:
            raise RuntimeError("Cannot create probe project.")

        probe_csproj = probe_dir / "RuleGenCliProbe.csproj"
        probe_csproj.write_text(
            f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Muonroi.RuleEngine.Abstractions" Version="{abstraction_version}" />
  </ItemGroup>
</Project>
""",
            encoding="utf-8",
        )

        sample_handler_path = probe_dir / "SampleHandler.cs"
        sample_handler_path.write_text(
            """using System.Threading;
using System.Threading.Tasks;
using Muonroi.RuleEngine.Abstractions;

public static class AppConsts
{
    public const string RULE_CODE = "RULE_CONST";
    public const string NAMEOF_CODE = "RULE_NAMEOF";
}

public sealed class SampleContext
{
    public int Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public partial class SampleHandler
{
    [MExtractAsRule(nameof(AppConsts.NAMEOF_CODE), Order = 1, HookPoint = HookPoint.BeforeRule)]
    public async Task<RuleResult> RuleByNameof(SampleContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["result.nameof"] = ValidateHelper(ctx);
        return RuleResult.Passed();
    }

    [MExtractAsRule(AppConsts.RULE_CODE, Order = 2, HookPoint = HookPoint.BeforeRule)]
    public async Task<RuleResult> RuleByConst(SampleContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["result.const"] = ctx.Amount > 0;
        return RuleResult.Passed();
    }

    private static bool ValidateHelper(SampleContext ctx)
    {
        return ctx.Amount > 0;
    }
}
""",
            encoding="utf-8",
        )

        duplicate_path = temp_root / "DuplicateHandler.cs"
        duplicate_path.write_text(
            """using System.Threading;
using System.Threading.Tasks;
using Muonroi.RuleEngine.Abstractions;

public sealed class DuplicateContext
{
    public int Value { get; set; }
}

public partial class DuplicateHandler
{
    [MExtractAsRule("SAME_CODE", Order = 1)]
    public async Task<RuleResult> A(DuplicateContext ctx, FactBag facts, CancellationToken ct)
        => RuleResult.Passed();

    [MExtractAsRule("SAME_CODE", Order = 2)]
    public async Task<RuleResult> B(DuplicateContext ctx, FactBag facts, CancellationToken ct)
        => RuleResult.Passed();
}
""",
            encoding="utf-8",
        )

        merge_target_path = probe_dir / "MergeTarget.cs"
        merge_target_path.write_text(
            """using System.Threading;
using System.Threading.Tasks;
using Muonroi.RuleEngine.Abstractions;

public partial class MergeTarget
{
    [MExtractAsRule("BASE_RULE", Order = 1)]
    public async Task<RuleResult> BaseRule(SampleContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["base"] = true;
        return RuleResult.Passed();
    }
}
""",
            encoding="utf-8",
        )

        extract_out_dir = temp_root / "generated"
        extract_result = run_command(
            [
                "dotnet",
                "run",
                "--project",
                str(rulegen_project),
                "--",
                "extract",
                "--source",
                str(sample_handler_path),
                "--output",
                str(extract_out_dir),
                "--namespace",
                "Probe.Generated",
                "--context",
                "SampleContext",
            ],
            temp_root,
        )
        generated_files = list(extract_out_dir.rglob("*.g.cs"))
        generated_text = "\n".join(_read_text(f) for f in generated_files)
        g13_pass = extract_result["ExitCode"] == 0 and "nameof(" not in generated_text and "AppConsts." not in generated_text
        add_case(
            cases,
            "G13",
            "extract + SemanticModel nameof/const",
            g13_pass,
            "Generated file does not contain nameof(...) or AppConsts.",
            f"ExitCode={extract_result['ExitCode']}; Files={len(generated_files)}",
            extract_result,
        )

        contains_helper = "ValidateHelper(" in generated_text
        g14_pass = extract_result["ExitCode"] == 0 and contains_helper
        add_case(
            cases,
            "G14",
            "extract + helper method",
            g14_pass,
            "Generated rule includes ValidateHelper",
            f"ContainsHelper={contains_helper}",
            [str(p) for p in generated_files],
        )

        dup_out_dir = temp_root / "dup_generated"
        dup_result = run_command(
            [
                "dotnet",
                "run",
                "--project",
                str(rulegen_project),
                "--",
                "extract",
                "--source",
                str(duplicate_path),
                "--output",
                str(dup_out_dir),
                "--namespace",
                "Probe.Generated",
                "--context",
                "DuplicateContext",
            ],
            temp_root,
        )
        dup_output_lower = dup_result["Output"].lower()
        g15_pass = dup_result["ExitCode"] != 0 and ("duplicate" in dup_output_lower or "same key" in dup_output_lower)
        add_case(
            cases,
            "G15",
            "verify duplicate detection",
            g15_pass,
            "Duplicate code produces non-zero exit and duplicate/same key message",
            f"ExitCode={dup_result['ExitCode']}",
            dup_result,
        )

        runtime_invalid_path = temp_root / "runtime-invalid.json"
        runtime_invalid_path.write_text(
            """{
  "workflowName": "merge-test",
  "version": 1,
  "rules": [
    {
      "code": "BROKEN_RULE",
      "name": "BROKEN_RULE",
      "order": 1,
      "hookPoint": "BeforeRule",
      "dependsOn": [],
      "condition": "order.amount >",
      "action": "facts['broken'] = true",
      "type": "Validation"
    }
  ]
}
""",
            encoding="utf-8",
        )

        merge_result = run_command(
            [
                "dotnet",
                "run",
                "--project",
                str(rulegen_project),
                "--",
                "merge",
                "--rules-json",
                str(runtime_invalid_path),
                "--target",
                str(merge_target_path),
                "--class",
                "MergeTarget",
                "--context",
                "SampleContext",
                "--compile-check",
                "true",
                "--compile-target",
                str(probe_csproj),
            ],
            temp_root,
        )
        merge_generated_path = probe_dir / "MergeTarget.Generated.cs"
        merge_target_text = _read_text(merge_target_path)
        g16_pass = (
            merge_result["ExitCode"] != 0
            and not merge_generated_path.exists()
            and "BROKEN_RULE" not in merge_target_text
            and "BASE_RULE" in merge_target_text
        )
        add_case(
            cases,
            "G16",
            "merge compile-check rollback",
            g16_pass,
            "Merge fails; target remains semantic base; no generated file",
            f"ExitCode={merge_result['ExitCode']}; GeneratedExists={merge_generated_path.exists()}",
            merge_result,
        )

        split_out_dir = temp_root / "split_generated"
        runtime_export_path = temp_root / "runtime-export.json"
        split_result = run_command(
            [
                "dotnet",
                "run",
                "--project",
                str(rulegen_project),
                "--",
                "split",
                "--source",
                str(sample_handler_path),
                "--output-dir",
                str(split_out_dir),
                "--export-json",
                str(runtime_export_path),
                "--namespace",
                "Probe.Generated",
                "--context",
                "SampleContext",
            ],
            temp_root,
        )
        runtime_text = _read_text(runtime_export_path) if runtime_export_path.exists() else ""
        g17_pass = split_result["ExitCode"] == 0 and runtime_export_path.exists() and '"rules"' in runtime_text and '"condition"' in runtime_text
        add_case(
            cases,
            "G17",
            "split -> runtime JSON",
            g17_pass,
            "runtime.json exists with rules[] and condition fields",
            f"ExitCode={split_result['ExitCode']}; RuntimeExists={runtime_export_path.exists()}",
            split_result,
        )

        watch_out_dir = temp_root / "watch_generated"
        watch_stdout = temp_root / "watch.out.log"
        watch_stderr = temp_root / "watch.err.log"
        watch_stdout_handle = watch_stdout.open("w", encoding="utf-8")
        watch_stderr_handle = watch_stderr.open("w", encoding="utf-8")
        watch_proc = subprocess.Popen(
            [
                "dotnet",
                "run",
                "--project",
                str(rulegen_project),
                "--",
                "watch",
                "--source",
                str(probe_dir),
                "--output",
                str(watch_out_dir),
                "--namespace",
                "Probe.Generated",
                "--context",
                "SampleContext",
            ],
            cwd=str(temp_root),
            stdout=watch_stdout_handle,
            stderr=watch_stderr_handle,
            text=True,
        )
        time.sleep(3)
        watch_files = wait_for_glob(watch_out_dir, "*.g.cs", timeout_seconds=30)
        old_mtime = {f: f.stat().st_mtime for f in watch_files}
        with sample_handler_path.open("a", encoding="utf-8") as append_file:
            append_file.write(f"\n// watch-touch-{timestamp_slug()}\n")

        changed = False
        deadline = time.time() + 30
        while time.time() < deadline:
            if watch_proc.poll() is not None:
                break
            current_files = list(watch_out_dir.rglob("*.g.cs"))
            for file_path in current_files:
                current_mtime = file_path.stat().st_mtime
                previous_mtime = old_mtime.get(file_path)
                if previous_mtime is None or current_mtime > previous_mtime:
                    changed = True
                    break
            if changed:
                break
            time.sleep(0.5)

        if watch_proc.poll() is None:
            watch_proc.kill()
            watch_proc.wait(timeout=10)
        watch_proc = None

        g18_pass = bool(watch_files) and changed
        add_case(
            cases,
            "G18",
            "watch detects changes",
            g18_pass,
            "watch updates generated .g.cs after file touch",
            f"FileReady={bool(watch_files)}; Changed={changed}",
            {"WatchOut": str(watch_stdout), "WatchErr": str(watch_stderr)},
        )

        tests_out_dir = temp_root / "generated_tests"
        gen_tests_result = run_command(
            [
                "dotnet",
                "run",
                "--project",
                str(rulegen_project),
                "--",
                "generate-tests",
                "--rules",
                str(extract_out_dir),
                "--output",
                str(tests_out_dir),
                "--namespace",
                "Probe.Generated",
            ],
            temp_root,
        )

        compile_dir = temp_root / "generated_compile"
        compile_dir.mkdir(parents=True, exist_ok=True)
        compile_csproj = compile_dir / "GeneratedCompile.csproj"
        compile_csproj.write_text(
            f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Muonroi.RuleEngine.Abstractions" Version="{abstraction_version}" />
    <PackageReference Include="xunit" Version="2.9.2" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="..\\RuleGenCliProbe\\SampleHandler.cs" Link="SampleHandler.cs" />
    <Compile Include="..\\generated\\**\\*.g.cs" />
    <Compile Include="..\\generated_tests\\*.cs" />
  </ItemGroup>
</Project>
""",
            encoding="utf-8",
        )
        compile_result = run_command(["dotnet", "build", str(compile_csproj), "-c", "Debug"], temp_root)
        g19_pass = gen_tests_result["ExitCode"] == 0 and compile_result["ExitCode"] == 0
        add_case(
            cases,
            "G19",
            "generate-tests compilation",
            g19_pass,
            "generate-tests succeeds and compiled generated tests with 0 errors",
            f"GenerateExit={gen_tests_result['ExitCode']}; BuildExit={compile_result['ExitCode']}",
            {"Generate": gen_tests_result, "Build": compile_result},
        )

        found_summary = "Rule Extraction Summary" in extract_result["Output"]
        found_total = "Total:" in extract_result["Output"]
        g20_pass = extract_result["ExitCode"] == 0 and (found_summary or found_total)
        add_case(
            cases,
            "G20",
            "Spectre.Console output",
            g20_pass,
            "extract output contains Spectre summary/progress text",
            f"FoundSummary={found_summary}; FoundTotal={found_total}",
            extract_result["Output"],
        )

        overall_pass = all(case["Pass"] for case in cases)
        evidence = {
            "OverallStatus": "PASS" if overall_pass else "FAIL",
            "RuleGenProject": str(rulegen_project),
            "TempRoot": str(temp_root),
            "OutputPath": str(output_path),
            "Cases": cases,
            "CompletedAtUtc": iso_utc_now(),
        }
        write_json_file(output_path, evidence)
        if overall_pass:
            print("RuleGen CLI v2 supplement flow: PASS")
        else:
            print("RuleGen CLI v2 supplement flow: FAIL")
        print(f"Evidence: {output_path}")
        return 0 if overall_pass else 1
    finally:
        if watch_proc is not None and watch_proc.poll() is None:
            watch_proc.kill()
            try:
                watch_proc.wait(timeout=10)
            except subprocess.TimeoutExpired:
                pass
        if watch_stdout_handle is not None:
            watch_stdout_handle.close()
        if watch_stderr_handle is not None:
            watch_stderr_handle.close()
        if temp_root.exists() and not any(case["Id"] == "PREP" and case["Pass"] for case in cases):
            shutil.rmtree(temp_root, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
