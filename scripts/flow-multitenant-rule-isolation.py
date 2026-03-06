import argparse
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.parse import urlparse

from _flow_common import (
    add_case,
    build_auth_headers,
    close_handle_silent,
    find_property_recursive,
    http_request,
    iso_utc_now,
    resolve_path,
    start_dotnet_process,
    stop_process_safe,
    timestamp_slug,
    wait_for_tcp_ready,
    write_json_file,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Runtime supplement flow for G09.")
    parser.add_argument("--project-path", required=True)
    parser.add_argument("--activation-proof-path", required=True)
    parser.add_argument("--public-key-path")
    parser.add_argument("--base-url", default="http://127.0.0.1:7310")
    parser.add_argument("--tenant-a", default="tenant-a")
    parser.add_argument("--tenant-b", default="tenant-b")
    parser.add_argument("--output-path")
    parser.add_argument("--startup-timeout-seconds", type=int, default=120)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    script_dir = Path(__file__).resolve().parent
    workspace_root = script_dir.parent

    project_path = resolve_path(args.project_path)
    activation_proof_path = resolve_path(args.activation_proof_path)
    if project_path is None or project_path.suffix.lower() != ".csproj":
        raise RuntimeError("ProjectPath must be a .csproj file.")
    if not project_path.exists():
        raise RuntimeError(f"ProjectPath not found: {project_path}")
    if activation_proof_path is None or not activation_proof_path.exists():
        raise RuntimeError(f"ActivationProofPath not found: {activation_proof_path}")

    if args.public_key_path:
        public_key_path = resolve_path(args.public_key_path)
    else:
        public_key_path = (workspace_root / "tools" / "MockLicenseServer" / "server_public_key.pem").resolve()
    if public_key_path is None or not public_key_path.exists():
        raise RuntimeError(f"PublicKeyPath not found: {public_key_path}")

    base_uri = urlparse(args.base_url)
    host_port = f"{base_uri.scheme}://{base_uri.hostname}:{base_uri.port}"

    if args.output_path:
        output_path = resolve_path(args.output_path)
        if output_path is None:
            raise RuntimeError("OutputPath is invalid.")
    else:
        output_path = workspace_root / "_tmp" / f"{project_path.stem}_multitenant_rule_isolation_{timestamp_slug()}.json"

    stdout_log = output_path.with_suffix(".app.out.log")
    stderr_log = output_path.with_suffix(".app.err.log")

    env_overrides = {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "LicenseConfigs__Mode": "Offline",
        "LicenseConfigs__ActivationProofPath": str(activation_proof_path),
        "LicenseConfigs__PublicKeyPath": str(public_key_path),
        "LicenseConfigs__ProjectSeed": "MUONROI_MULTITENANT_SUPPLEMENT_TEST_SEED_20260303",
        "MultiTenantConfigs__Enabled": "true",
        "MultiTenantConfigs__RequireTenantClaimForAuthenticatedUser": "false",
        "TokenConfigs__MultiTenantEnabled": "true",
    }

    proc = None
    stdout_handle = None
    stderr_handle = None
    try:
        proc, stdout_handle, stderr_handle = start_dotnet_process(
            project_path=project_path,
            host_port=host_port,
            stdout_log=stdout_log,
            stderr_log=stderr_log,
            env_overrides=env_overrides,
        )
        wait_for_tcp_ready(host_port, args.startup_timeout_seconds, proc, "API")

        suffix = timestamp_slug().replace("_", "")[-10:]
        username = f"tenant_{suffix}"
        password = "P@ssw0rd!123"

        register_payload = {
            "userName": username,
            "password": password,
            "email": f"{username}@example.com",
            "phoneNumber": "0123456789",
            "name": "Tenant",
            "surname": "Isolation",
            "isActive": True,
            "isTwoFactorEnabled": False,
            "isUseThirdPartyLogin": False,
            "externalLoginProvider": "",
            "externalLoginToken": "",
        }
        register_response = http_request(
            f"{host_port}/api/v1/Auth/register",
            method="POST",
            body=register_payload,
        )
        if not (200 <= register_response["StatusCode"] < 300):
            raise RuntimeError(f"Register failed with HTTP {register_response['StatusCode']}.")

        login_response = http_request(
            f"{host_port}/api/v1/Auth/login",
            method="POST",
            body={"username": username, "password": password},
        )
        if not (200 <= login_response["StatusCode"] < 300):
            raise RuntimeError(f"Login failed with HTTP {login_response['StatusCode']}.")

        access_token = find_property_recursive(login_response["Json"], "accessToken")
        if not isinstance(access_token, str) or not access_token.strip():
            raise RuntimeError("Login succeeded but accessToken was not found in response.")

        tenant_a_headers = build_auth_headers(token=access_token, tenant_id=args.tenant_a)
        tenant_b_headers = build_auth_headers(token=access_token, tenant_id=args.tenant_b)

        reset = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/reset",
            method="POST",
            headers=tenant_a_headers,
            body={},
        )
        if reset["StatusCode"] != 200:
            raise RuntimeError(f"Cannot reset supplement state. HTTP {reset['StatusCode']}.")

        cases: list[dict] = []
        workflow = "order-validation"

        register_a_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/tenant-rules/register",
            method="POST",
            headers=tenant_a_headers,
            body={
                "tenantId": args.tenant_a,
                "workflow": workflow,
                "ruleCode": "TA_RULE",
                "outputKey": "tenant.marker",
                "outputValue": "A",
            },
        )
        register_b_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/tenant-rules/register",
            method="POST",
            headers=tenant_b_headers,
            body={
                "tenantId": args.tenant_b,
                "workflow": workflow,
                "ruleCode": "TB_RULE",
                "outputKey": "tenant.marker",
                "outputValue": "B",
            },
        )
        register_pass = register_a_resp["StatusCode"] == 200 and register_b_resp["StatusCode"] == 200
        add_case(
            cases,
            "G09_REG",
            "Register tenant rules",
            register_pass,
            "Both tenant registrations return HTTP 200",
            f"A={register_a_resp['StatusCode']}; B={register_b_resp['StatusCode']}",
            [register_a_resp["Json"], register_b_resp["Json"]],
        )

        evaluate_body = {"workflow": workflow, "input": {"amount": 100}}
        eval_a = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/tenant-rules/evaluate",
            method="POST",
            headers=tenant_a_headers,
            body=evaluate_body,
        )
        eval_b = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/tenant-rules/evaluate",
            method="POST",
            headers=tenant_b_headers,
            body=evaluate_body,
        )

        applied_a = find_property_recursive(eval_a["Json"], "appliedRules")
        applied_b = find_property_recursive(eval_b["Json"], "appliedRules")
        rules_a = [str(x) for x in applied_a] if isinstance(applied_a, list) else []
        rules_b = [str(x) for x in applied_b] if isinstance(applied_b, list) else []
        marker_a = find_property_recursive(eval_a["Json"], "tenant.marker")
        marker_b = find_property_recursive(eval_b["Json"], "tenant.marker")

        isolation_pass = (
            eval_a["StatusCode"] == 200
            and eval_b["StatusCode"] == 200
            and "TA_RULE" in rules_a
            and "TB_RULE" not in rules_a
            and "TB_RULE" in rules_b
            and "TA_RULE" not in rules_b
            and str(marker_a) == "A"
            and str(marker_b) == "B"
        )
        add_case(
            cases,
            "G09",
            "Multi-tenant rule isolation",
            isolation_pass,
            "tenant-a only sees TA_RULE; tenant-b only sees TB_RULE",
            f"A=[{','.join(rules_a)}], marker={marker_a}; B=[{','.join(rules_b)}], marker={marker_b}",
            [eval_a["Json"], eval_b["Json"]],
        )

        quota_a_body = {
            "scenario": "quota-concurrent",
            "tenantId": args.tenant_a,
            "concurrentLimit": 1,
            "rateLimitPerSecond": 100,
            "artificialDelayMs": 900,
        }

        def _tenant_a_quota_call() -> dict:
            return http_request(
                f"{host_port}/api/v1/rule-engine/supplement/test",
                method="POST",
                headers=tenant_a_headers,
                body=quota_a_body,
                timeout_seconds=30,
            )

        with ThreadPoolExecutor(max_workers=2) as executor:
            future1 = executor.submit(_tenant_a_quota_call)
            future2 = executor.submit(_tenant_a_quota_call)
            tenant_a_quota_results = [future1.result(), future2.result()]

        tenant_a_status_codes = [int(r["StatusCode"]) for r in tenant_a_quota_results]
        tenant_a_has_429 = 429 in tenant_a_status_codes

        quota_b_body = {
            "scenario": "quota-concurrent",
            "tenantId": args.tenant_b,
            "concurrentLimit": 1,
            "rateLimitPerSecond": 100,
            "artificialDelayMs": 100,
        }
        tenant_b_quota_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=tenant_b_headers,
            body=quota_b_body,
        )
        quota_isolation_pass = tenant_a_has_429 and tenant_b_quota_resp["StatusCode"] == 200
        add_case(
            cases,
            "G09_QUOTA",
            "Tenant quota isolation",
            quota_isolation_pass,
            "tenant-a overload gets 429; tenant-b remains 200",
            f"tenant-a statuses={','.join(str(s) for s in tenant_a_status_codes)}; tenant-b={tenant_b_quota_resp['StatusCode']}",
            [tenant_a_quota_results, tenant_b_quota_resp["Json"]],
        )

        overall_pass = all(case["Pass"] for case in cases)
        evidence = {
            "OverallStatus": "PASS" if overall_pass else "FAIL",
            "ProjectPath": str(project_path),
            "BaseUrl": host_port,
            "ActivationProofPath": str(activation_proof_path),
            "TenantA": args.tenant_a,
            "TenantB": args.tenant_b,
            "RegisteredUser": username,
            "Cases": cases,
            "CompletedAtUtc": iso_utc_now(),
        }
        write_json_file(output_path, evidence)

        if overall_pass:
            print("Multi-tenant rule isolation supplement flow: PASS")
        else:
            print("Multi-tenant rule isolation supplement flow: FAIL")
        print(f"Evidence: {output_path}")
        return 0 if overall_pass else 1
    finally:
        stop_process_safe(proc)
        close_handle_silent(stdout_handle)
        close_handle_silent(stderr_handle)


if __name__ == "__main__":
    raise SystemExit(main())
