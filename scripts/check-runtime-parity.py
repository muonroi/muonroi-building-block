import argparse
import json
from pathlib import Path
from typing import Any

from _flow_common import (
    build_auth_headers,
    http_request,
    iso_utc_now,
    resolve_path,
    timestamp_slug,
    write_json_file,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Compare runtime-rules endpoint result with code-generated endpoint result.")
    parser.add_argument("--runtime-url", required=True)
    parser.add_argument("--code-url", required=True)
    parser.add_argument("--method", default="POST")
    parser.add_argument("--payload-path", required=True)
    parser.add_argument("--runtime-token")
    parser.add_argument("--code-token")
    parser.add_argument("--tenant-id")
    parser.add_argument("--ignore-paths", nargs="*", default=[])
    parser.add_argument("--output-path")
    return parser.parse_args()


def _drop_path(root: Any, path: str) -> None:
    parts = [part for part in path.split(".") if part]
    if not parts:
        return
    node = root
    for part in parts[:-1]:
        if isinstance(node, dict) and part in node:
            node = node[part]
            continue
        return
    if isinstance(node, dict):
        node.pop(parts[-1], None)


def _normalize(data: Any, ignore_paths: list[str]) -> Any:
    if data is None:
        return None
    normalized = json.loads(json.dumps(data))
    for path in ignore_paths:
        _drop_path(normalized, path)
    return normalized


def main() -> int:
    args = parse_args()
    payload_path = resolve_path(args.payload_path)
    if payload_path is None or not payload_path.exists():
        raise RuntimeError(f"PayloadPath not found: {args.payload_path}")

    payload_text = payload_path.read_text(encoding="utf-8")
    try:
        payload_json = json.loads(payload_text)
    except json.JSONDecodeError as ex:
        raise RuntimeError(f"PayloadPath is not valid JSON: {ex}") from ex

    runtime_headers = build_auth_headers(token=args.runtime_token, tenant_id=args.tenant_id)
    code_headers = build_auth_headers(token=args.code_token, tenant_id=args.tenant_id)

    runtime_result = http_request(args.runtime_url, method=args.method, headers=runtime_headers, body=payload_json)
    code_result = http_request(args.code_url, method=args.method, headers=code_headers, body=payload_json)

    runtime_ok = runtime_result["StatusCode"] in (200, 201)
    code_ok = code_result["StatusCode"] in (200, 201)

    runtime_json = _normalize(runtime_result["Json"], args.ignore_paths)
    code_json = _normalize(code_result["Json"], args.ignore_paths)
    parity_pass = runtime_ok and code_ok and runtime_json == code_json

    evidence = {
        "OverallStatus": "PASS" if parity_pass else "FAIL",
        "TimestampUtc": iso_utc_now(),
        "RuntimeUrl": args.runtime_url,
        "CodeUrl": args.code_url,
        "Method": args.method.upper(),
        "IgnorePaths": args.ignore_paths,
        "RuntimeStatusCode": runtime_result["StatusCode"],
        "CodeStatusCode": code_result["StatusCode"],
        "RuntimeJson": runtime_json,
        "CodeJson": code_json,
        "RuntimeRaw": runtime_result["Body"],
        "CodeRaw": code_result["Body"],
    }

    if args.output_path:
        output_path = resolve_path(args.output_path)
        if output_path is None:
            raise RuntimeError("OutputPath is invalid.")
    else:
        output_path = Path(__file__).resolve().parent.parent / "_tmp" / f"runtime_parity_{timestamp_slug()}.json"

    write_json_file(output_path, evidence)
    print(f"Parity status: {evidence['OverallStatus']}")
    print(f"Evidence: {output_path}")
    return 0 if parity_pass else 1


if __name__ == "__main__":
    raise SystemExit(main())
