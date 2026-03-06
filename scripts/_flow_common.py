import json
import os
import re
import socket
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from typing import Any


def resolve_path(path_value: str | None) -> Path | None:
    if path_value is None:
        return None
    text = str(path_value).strip()
    if not text:
        return None
    return Path(text).expanduser().resolve()


def find_property_recursive(input_obj: Any, property_name: str, depth: int = 0) -> Any:
    if input_obj is None or depth > 30:
        return None

    if isinstance(input_obj, dict):
        for key, value in input_obj.items():
            if str(key).lower() == property_name.lower():
                return value
        for value in input_obj.values():
            found = find_property_recursive(value, property_name, depth + 1)
            if found is not None:
                return found
        return None

    if isinstance(input_obj, list):
        for item in input_obj:
            found = find_property_recursive(item, property_name, depth + 1)
            if found is not None:
                return found
        return None

    return None


def collection_count(value: Any) -> int:
    if value is None:
        return 0
    if isinstance(value, dict):
        return len(value)
    if isinstance(value, (list, tuple, set)):
        return len(value)
    return 1


def add_case(
    cases: list[dict[str, Any]],
    case_id: str,
    name: str,
    passed: bool,
    expected: str,
    actual: str,
    detail: Any,
) -> None:
    cases.append(
        {
            "Id": case_id,
            "Name": name,
            "Pass": bool(passed),
            "Expected": expected,
            "Actual": actual,
            "Detail": detail,
        }
    )


def run_command(
    command: list[str],
    workdir: Path,
    env: dict[str, str] | None = None,
    timeout_seconds: int | None = None,
) -> dict[str, Any]:
    completed = subprocess.run(
        command,
        cwd=str(workdir),
        env=env,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout_seconds,
        check=False,
    )
    output = (completed.stdout or "") + (("\n" + completed.stderr) if completed.stderr else "")
    return {
        "ExitCode": int(completed.returncode),
        "Output": output,
        "Arguments": " ".join(command[1:]) if command and command[0].lower() == "dotnet" else " ".join(command),
    }


def normalize_bearer_token(token: str | None) -> str | None:
    if token is None:
        return None
    value = str(token).strip()
    if not value:
        return None
    if value.lower().startswith("bearer "):
        value = value[7:].strip()
    return value or None


def build_auth_headers(
    token: str | None = None,
    tenant_id: str | None = None,
    actor: str | None = None,
    extra_headers: dict[str, str] | None = None,
) -> dict[str, str]:
    headers: dict[str, str] = {"Accept": "application/json"}
    normalized_token = normalize_bearer_token(token)
    if normalized_token:
        headers["Authorization"] = f"Bearer {normalized_token}"
    if tenant_id and str(tenant_id).strip():
        tenant_value = str(tenant_id).strip()
        # Send both header names because some APIs bind TenantId while others bind x-tenant-id.
        headers["TenantId"] = tenant_value
        headers["x-tenant-id"] = tenant_value
    if actor and str(actor).strip():
        headers["x-actor"] = str(actor).strip()
    if extra_headers:
        headers.update(extra_headers)
    return headers


def http_request(
    url: str,
    method: str = "GET",
    headers: dict[str, str] | None = None,
    body: str | dict[str, Any] | None = None,
    timeout_seconds: int = 20,
) -> dict[str, Any]:
    req_headers = headers.copy() if headers else {}
    data: bytes | None = None
    if body is not None:
        if isinstance(body, dict):
            body_text = json.dumps(body)
        else:
            body_text = body
        data = body_text.encode("utf-8")
        req_headers.setdefault("Content-Type", "application/json")

    request = urllib.request.Request(url=url, method=method.upper(), headers=req_headers, data=data)
    try:
        with urllib.request.urlopen(request, timeout=timeout_seconds) as response:
            content = response.read().decode("utf-8", errors="replace")
            parsed = None
            if content.strip():
                try:
                    parsed = json.loads(content)
                except json.JSONDecodeError:
                    parsed = None
            return {"StatusCode": int(response.status), "Body": content, "Json": parsed, "Error": None}
    except urllib.error.HTTPError as ex:
        content = ex.read().decode("utf-8", errors="replace") if ex.fp else ""
        parsed = None
        if content.strip():
            try:
                parsed = json.loads(content)
            except json.JSONDecodeError:
                parsed = None
        return {"StatusCode": int(ex.code), "Body": content, "Json": parsed, "Error": None}
    except Exception as ex:  # noqa: BLE001
        return {"StatusCode": -1, "Body": "", "Json": None, "Error": str(ex)}


def wait_for_tcp_ready(base_url: str, timeout_seconds: int, process: subprocess.Popen[Any], display_name: str) -> None:
    parsed = urllib.parse.urlparse(base_url)
    host = parsed.hostname or "127.0.0.1"
    port = parsed.port or (443 if parsed.scheme == "https" else 80)
    deadline = time.time() + timeout_seconds

    while time.time() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f"{display_name} exited early with code {process.returncode}.")
        try:
            with socket.create_connection((host, port), timeout=1):
                return
        except OSError:
            time.sleep(0.5)
    raise RuntimeError(f"Timed out waiting for {display_name} at {base_url}.")


def stop_process_safe(process: subprocess.Popen[Any] | None) -> None:
    if process is None:
        return
    if process.poll() is not None:
        return
    process.kill()
    try:
        process.wait(timeout=10)
    except subprocess.TimeoutExpired:
        pass


def write_json_file(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")


def iso_utc_now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def timestamp_slug() -> str:
    return time.strftime("%Y%m%d_%H%M%S", time.localtime())


def start_dotnet_process(
    project_path: Path,
    host_port: str,
    stdout_log: Path,
    stderr_log: Path,
    env_overrides: dict[str, str],
) -> tuple[subprocess.Popen[Any], Any, Any]:
    run_env = os.environ.copy()
    run_env.update(env_overrides)
    stdout_log.parent.mkdir(parents=True, exist_ok=True)
    stderr_log.parent.mkdir(parents=True, exist_ok=True)
    stdout_handle = stdout_log.open("w", encoding="utf-8")
    stderr_handle = stderr_log.open("w", encoding="utf-8")
    proc = subprocess.Popen(
        ["dotnet", "run", "--project", str(project_path), "--urls", host_port],
        cwd=str(project_path.parent),
        stdout=stdout_handle,
        stderr=stderr_handle,
        env=run_env,
        text=True,
    )
    return proc, stdout_handle, stderr_handle


def close_handle_silent(handle: Any) -> None:
    try:
        if handle is not None:
            handle.close()
    except Exception:  # noqa: BLE001
        pass


def to_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    if isinstance(value, str):
        return value.strip().lower() in {"true", "1", "yes", "y"}
    return False


def _semver_sort_key(version: str) -> tuple:
    main, sep, pre = version.partition("-")
    main_parts = tuple(int(part) if part.isdigit() else 0 for part in main.split("."))
    if sep:
        pre_parts = tuple(int(part) if part.isdigit() else part for part in pre.split("."))
        return (main_parts, 0, pre_parts)
    return (main_parts, 1, ())


def find_latest_local_package_version(local_nuget_path: Path, package_id: str) -> str | None:
    escaped = re.escape(package_id)
    pattern = re.compile(rf"^{escaped}\.(?P<version>.+)\.nupkg$", re.IGNORECASE)
    versions: list[str] = []
    if not local_nuget_path.exists():
        return None
    for file in local_nuget_path.glob("*.nupkg"):
        name = file.name
        if name.lower().endswith(".snupkg"):
            continue
        match = pattern.match(name)
        if match:
            versions.append(match.group("version"))
    if not versions:
        return None
    versions.sort(key=_semver_sort_key)
    return versions[-1]
