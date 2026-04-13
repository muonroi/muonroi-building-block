import argparse
import json
from pathlib import Path

from _flow_common import (
    add_case,
    build_auth_headers,
    http_request,
    iso_utc_now,
    resolve_path,
    run_command,
    timestamp_slug,
    write_json_file,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Runtime ruleset export -> RuleGen merge --compile-check -> optional parity.")
    parser.add_argument("--workflow", required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:7310")
    parser.add_argument("--rulegen-project", default=r"D:\sources\Core\MuonroiBuildingBlock\tools\Muonroi.RuleGen\Muonroi.RuleGen.csproj")
    parser.add_argument("--target-handler", required=True)
    parser.add_argument("--handler-class", required=True)
    parser.add_argument("--context-type", required=True)
    parser.add_argument("--compile-target", required=True)
    parser.add_argument("--token")
    parser.add_argument("--tenant-id")
    parser.add_argument("--actor")
    parser.add_argument("--parity-runtime-url")
    parser.add_argument("--parity-code-url")
    parser.add_argument("--parity-payload-path")
    parser.add_argument("--ignore-parity-paths", nargs="*", default=[])
    parser.add_argument("--output-path")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    workspace_root = script_dir.parent
    tmp_root = workspace_root / "_tmp"
    tmp_root.mkdir(parents=True, exist_ok=True)

    rulegen_project = resolve_path(args.rulegen_project)
    target_handler = resolve_path(args.target_handler)
    compile_target = resolve_path(args.compile_target)
    if rulegen_project is None or not rulegen_project.exists():
        raise RuntimeError(f"RuleGenProject not found: {args.rulegen_project}")
    if target_handler is None or not target_handler.exists():
        raise RuntimeError(f"TargetHandler not found: {args.target_handler}")
    if compile_target is None or not compile_target.exists():
        raise RuntimeError(f"CompileTarget not found: {args.compile_target}")

    if args.output_path:
        output_path = resolve_path(args.output_path)
        if output_path is None:
            raise RuntimeError("OutputPath is invalid.")
    else:
        output_path = tmp_root / f"runtime_roundtrip_{args.workflow}_{timestamp_slug()}.json"

    headers = build_auth_headers(token=args.token, tenant_id=args.tenant_id, actor=args.actor)
    cases: list[dict] = []

    export_url = f"{args.base_url.rstrip('/')}/api/v1/rule-engine/rulesets/{args.workflow}/export"
    export_result = http_request(export_url, method="GET", headers=headers)
    export_ok = export_result["StatusCode"] == 200 and export_result["Json"] is not None
    add_case(
        cases,
        "R1",
        "Export active runtime ruleset",
        export_ok,
        "GET export endpoint returns 200 + JSON",
        f"StatusCode={export_result['StatusCode']}",
        export_result,
    )
    if not export_ok:
        evidence = {
            "OverallStatus": "FAIL",
            "Workflow": args.workflow,
            "CompletedAtUtc": iso_utc_now(),
            "Cases": cases,
        }
        write_json_file(output_path, evidence)
        print(f"Flow status: FAIL")
        print(f"Evidence: {output_path}")
        return 1

    export_json = export_result["Json"] or {}
    ruleset_json = export_json.get("RuleSetJson") if isinstance(export_json, dict) else None
    if not isinstance(ruleset_json, str) or not ruleset_json.strip():
        ruleset_json = export_result["Body"]

    runtime_json_path = tmp_root / f"runtime_export_{args.workflow}_{timestamp_slug()}.json"
    runtime_json_path.write_text(ruleset_json, encoding="utf-8")
    add_case(
        cases,
        "R2",
        "Persist runtime export to temp file",
        runtime_json_path.exists(),
        "Exported ruleset written to disk",
        str(runtime_json_path),
        {},
    )

    merge_cmd = [
        "dotnet",
        "run",
        "--project",
        str(rulegen_project),
        "--",
        "merge",
        "--rules-json",
        str(runtime_json_path),
        "--target",
        str(target_handler),
        "--class",
        args.handler_class,
        "--context",
        args.context_type,
        "--compile-check",
        "true",
        "--compile-target",
        str(compile_target),
    ]
    merge_result = run_command(merge_cmd, workspace_root)
    merge_ok = merge_result["ExitCode"] == 0
    add_case(
        cases,
        "R3",
        "RuleGen merge --compile-check",
        merge_ok,
        "Merge succeeds and compile-check passes",
        f"ExitCode={merge_result['ExitCode']}",
        merge_result,
    )

    build_result = run_command(["dotnet", "build", str(compile_target), "-c", "Release"], compile_target.parent)
    build_ok = build_result["ExitCode"] == 0
    add_case(
        cases,
        "R4",
        "Build compile-target after merge",
        build_ok,
        "dotnet build returns 0",
        f"ExitCode={build_result['ExitCode']}",
        build_result,
    )

    parity_ok = True
    parity_result: dict | None = None
    if args.parity_runtime_url and args.parity_code_url and args.parity_payload_path:
        parity_payload = resolve_path(args.parity_payload_path)
        if parity_payload is None or not parity_payload.exists():
            parity_ok = False
            parity_result = {"Error": f"Parity payload not found: {args.parity_payload_path}"}
        else:
            parity_cmd = [
                "py",
                "-3",
                str(script_dir / "check-runtime-parity.py"),
                "--runtime-url",
                args.parity_runtime_url,
                "--code-url",
                args.parity_code_url,
                "--payload-path",
                str(parity_payload),
            ]
            if args.token:
                parity_cmd.extend(["--runtime-token", args.token, "--code-token", args.token])
            if args.tenant_id:
                parity_cmd.extend(["--tenant-id", args.tenant_id])
            if args.ignore_parity_paths:
                parity_cmd.extend(["--ignore-paths", *args.ignore_parity_paths])

            parity_exec = run_command(parity_cmd, workspace_root)
            parity_ok = parity_exec["ExitCode"] == 0
            parity_result = parity_exec

        add_case(
            cases,
            "R5",
            "Parity check runtime vs code",
            parity_ok,
            "Parity script returns exit code 0",
            "PASS" if parity_ok else "FAIL",
            parity_result or {},
        )

    overall_pass = all(case["Pass"] for case in cases)
    evidence = {
        "OverallStatus": "PASS" if overall_pass else "FAIL",
        "Workflow": args.workflow,
        "RuntimeExportPath": str(runtime_json_path),
        "CompletedAtUtc": iso_utc_now(),
        "Cases": cases,
    }
    write_json_file(output_path, evidence)
    print(f"Flow status: {evidence['OverallStatus']}")
    print(f"Evidence: {output_path}")
    return 0 if overall_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
