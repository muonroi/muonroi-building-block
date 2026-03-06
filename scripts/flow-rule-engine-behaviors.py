import argparse
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.parse import urlparse

from _flow_common import (
    add_case,
    build_auth_headers,
    close_handle_silent,
    collection_count,
    find_property_recursive,
    http_request,
    iso_utc_now,
    resolve_path,
    start_dotnet_process,
    stop_process_safe,
    timestamp_slug,
    to_bool,
    wait_for_tcp_ready,
    write_json_file,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Runtime supplement flow for G01-G12.")
    parser.add_argument("--project-path", required=True)
    parser.add_argument("--activation-proof-path", required=True)
    parser.add_argument("--public-key-path")
    parser.add_argument("--base-url", default="http://127.0.0.1:7310")
    parser.add_argument("--tenant-id", default="tenant-a")
    parser.add_argument("--output-path")
    parser.add_argument("--startup-timeout-seconds", type=int, default=120)
    return parser.parse_args()


def _extract_rule_order(rule_results: object, code: str) -> int | None:
    if isinstance(rule_results, dict):
        rule_obj = rule_results.get(code)
        if isinstance(rule_obj, dict):
            value = rule_obj.get("executionOrder")
            if isinstance(value, int):
                return value
            if isinstance(value, str) and value.isdigit():
                return int(value)
    return None


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
        project_name = project_path.stem
        output_path = workspace_root / "_tmp" / f"{project_name}_rule_engine_behaviors_{timestamp_slug()}.json"

    stdout_log = output_path.with_suffix(".app.out.log")
    stderr_log = output_path.with_suffix(".app.err.log")

    env_overrides = {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "LicenseConfigs__Mode": "Offline",
        "LicenseConfigs__ActivationProofPath": str(activation_proof_path),
        "LicenseConfigs__PublicKeyPath": str(public_key_path),
        "LicenseConfigs__ProjectSeed": "MUONROI_RULE_ENGINE_SUPPLEMENT_TEST_SEED_20260303",
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
        username = f"rule_{suffix}"
        password = "P@ssw0rd!123"

        register_payload = {
            "userName": username,
            "password": password,
            "email": f"{username}@example.com",
            "phoneNumber": "0123456789",
            "name": "Rule",
            "surname": "Supplement",
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

        auth_tenant_headers = build_auth_headers(token=access_token, tenant_id=args.tenant_id)

        reset = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/reset",
            method="POST",
            headers=auth_tenant_headers,
            body={},
        )
        if reset["StatusCode"] != 200:
            raise RuntimeError(f"Cannot reset supplement state. HTTP {reset['StatusCode']}.")

        cases: list[dict] = []

        all_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body={"scenario": "allornothing", "executionMode": "AllOrNothing", "tenantId": args.tenant_id},
        )
        all_rule_count = collection_count(find_property_recursive(all_resp["Json"], "ruleResults"))
        all_error_count = collection_count(find_property_recursive(all_resp["Json"], "errors"))
        all_pass = all_resp["StatusCode"] == 200 and all_rule_count < 3 and all_error_count >= 1
        add_case(
            cases,
            "G01",
            "ExecutionMode.AllOrNothing",
            all_pass,
            "HTTP 200; stop early (<3 rules); errors>=1",
            f"HTTP {all_resp['StatusCode']}; ruleCount={all_rule_count}; errors={all_error_count}",
            all_resp["Json"],
        )

        best_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body={"scenario": "besteffort", "executionMode": "BestEffort", "tenantId": args.tenant_id},
        )
        best_rule_count = collection_count(find_property_recursive(best_resp["Json"], "ruleResults"))
        best_error_count = collection_count(find_property_recursive(best_resp["Json"], "errors"))
        best_pass = best_resp["StatusCode"] == 200 and best_rule_count >= 3 and best_error_count >= 1
        add_case(
            cases,
            "G02",
            "ExecutionMode.BestEffort",
            best_pass,
            "HTTP 200; executes all rules (>=3); errors aggregated",
            f"HTTP {best_resp['StatusCode']}; ruleCount={best_rule_count}; errors={best_error_count}",
            best_resp["Json"],
        )

        comp_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body={"scenario": "compensateonfailure", "executionMode": "CompensateOnFailure", "tenantId": args.tenant_id},
        )
        comp_error_count = collection_count(find_property_recursive(comp_resp["Json"], "compensationErrors"))
        comp_rules_count = collection_count(find_property_recursive(comp_resp["Json"], "compensatedRules"))
        comp_pass = comp_resp["StatusCode"] == 200 and comp_rules_count >= 1 and comp_error_count == 0
        add_case(
            cases,
            "G03_G04",
            "CompensateOnFailure + ICompensatableRule",
            comp_pass,
            "HTTP 200; compensatedRules>=1; compensationErrors=0",
            f"HTTP {comp_resp['StatusCode']}; compensatedRules={comp_rules_count}; compensationErrors={comp_error_count}",
            comp_resp["Json"],
        )

        fact_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body={"scenario": "factbag", "tenantId": args.tenant_id},
        )
        fact_value = find_property_recursive(fact_resp["Json"], "order.validated")
        fact_pass = fact_resp["StatusCode"] == 200 and to_bool(fact_value)
        add_case(
            cases,
            "G05",
            "FactBag propagation",
            fact_pass,
            "HTTP 200; facts['order.validated']=true",
            f"HTTP {fact_resp['StatusCode']}; order.validated={fact_value}",
            fact_resp["Json"],
        )

        dep_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body={"scenario": "dependson", "tenantId": args.tenant_id},
        )
        dep_results = find_property_recursive(dep_resp["Json"], "ruleResults")
        order_a = _extract_rule_order(dep_results, "RULE_A")
        order_b = _extract_rule_order(dep_results, "RULE_B")
        dep_pass = dep_resp["StatusCode"] == 200 and order_a is not None and order_b is not None and order_b > order_a
        add_case(
            cases,
            "G06",
            "DependsOn ordering",
            dep_pass,
            "RULE_B.executionOrder > RULE_A.executionOrder",
            f"HTTP {dep_resp['StatusCode']}; RULE_A={order_a}; RULE_B={order_b}",
            dep_resp["Json"],
        )

        quota_concurrent_body = {
            "scenario": "quota-concurrent",
            "tenantId": args.tenant_id,
            "concurrentLimit": 1,
            "rateLimitPerSecond": 100,
            "artificialDelayMs": 900,
        }

        def _quota_call() -> dict:
            return http_request(
                f"{host_port}/api/v1/rule-engine/supplement/test",
                method="POST",
                headers=auth_tenant_headers,
                body=quota_concurrent_body,
                timeout_seconds=30,
            )

        with ThreadPoolExecutor(max_workers=2) as executor:
            future1 = executor.submit(_quota_call)
            future2 = executor.submit(_quota_call)
            job_results = [future1.result(), future2.result()]
        status_codes = [int(x["StatusCode"]) for x in job_results]
        g07_pass = 429 in status_codes
        add_case(
            cases,
            "G07",
            "Quota ConcurrentExecutions",
            g07_pass,
            "At least one concurrent request returns 429",
            f"StatusCodes={','.join(str(s) for s in status_codes)}",
            job_results,
        )

        rate_tenant_id = f"{args.tenant_id}-rate"
        quota_rate_body = {
            "scenario": "quota-rate",
            "tenantId": rate_tenant_id,
            "concurrentLimit": 5,
            "rateLimitPerSecond": 1,
            "artificialDelayMs": 0,
        }
        rate_resp1 = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body=quota_rate_body,
        )
        rate_resp2 = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body=quota_rate_body,
        )
        g08_pass = rate_resp1["StatusCode"] == 200 and rate_resp2["StatusCode"] == 429
        add_case(
            cases,
            "G08",
            "Quota RuleEvaluationsPerSecond",
            g08_pass,
            "First call 200; second call 429 with rateLimitPerSecond=1",
            f"First={rate_resp1['StatusCode']}; Second={rate_resp2['StatusCode']}",
            [rate_resp1, rate_resp2],
        )

        hook_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/test",
            method="POST",
            headers=auth_tenant_headers,
            body={"scenario": "hooks", "executionMode": "BestEffort", "tenantId": args.tenant_id},
        )
        hook_trace = find_property_recursive(hook_resp["Json"], "hookTrace")
        if isinstance(hook_trace, list):
            hook_text = "|".join(str(x) for x in hook_trace)
        else:
            hook_text = str(hook_trace) if hook_trace is not None else ""
        g10_pass = (
            hook_resp["StatusCode"] == 200
            and "BeforeRule:HOOK_OK" in hook_text
            and "AfterRule:HOOK_OK" in hook_text
            and "OnError:HOOK_FAIL" in hook_text
        )
        add_case(
            cases,
            "G10",
            "HookPoint execution order",
            g10_pass,
            "Hook trace contains Before/After/OnError markers",
            f"HTTP {hook_resp['StatusCode']}; Trace={hook_text}",
            hook_resp["Json"],
        )

        metrics_resp = http_request(
            f"{host_port}/api/v1/rule-engine/supplement/metrics",
            method="GET",
            headers=auth_tenant_headers,
        )
        matched = find_property_recursive(metrics_resp["Json"], "matched")
        fired = find_property_recursive(metrics_resp["Json"], "fired")
        matched_int = int(matched) if isinstance(matched, (int, float, str)) and str(matched).isdigit() else 0
        fired_int = int(fired) if isinstance(fired, (int, float, str)) and str(fired).isdigit() else 0
        g11_pass = metrics_resp["StatusCode"] == 200 and matched_int > 0 and fired_int > 0
        add_case(
            cases,
            "G11",
            "IRuleEventListener events",
            g11_pass,
            "metrics.rules.matched>0 and fired>0",
            f"HTTP {metrics_resp['StatusCode']}; matched={matched_int}; fired={fired_int}",
            metrics_resp["Json"],
        )

        required_fields = ["isSuccess", "executionMode", "ruleResults", "errors", "compensationErrors", "facts"]
        missing = [field for field in required_fields if f'"{field}"' not in (all_resp["Body"] or "")]
        g12_pass = len(missing) == 0
        add_case(
            cases,
            "G12",
            "OrchestratorResult structure",
            g12_pass,
            "Response contains isSuccess/executionMode/ruleResults/errors/compensationErrors/facts",
            f"Missing={','.join(missing)}",
            all_resp["Json"],
        )

        overall_pass = all(case["Pass"] for case in cases)
        evidence = {
            "OverallStatus": "PASS" if overall_pass else "FAIL",
            "ProjectPath": str(project_path),
            "BaseUrl": host_port,
            "ActivationProofPath": str(activation_proof_path),
            "TenantId": args.tenant_id,
            "RegisteredUser": username,
            "Cases": cases,
            "CompletedAtUtc": iso_utc_now(),
        }
        write_json_file(output_path, evidence)

        if overall_pass:
            print("Rule engine behaviors supplement flow: PASS")
        else:
            print("Rule engine behaviors supplement flow: FAIL")
        print(f"Evidence: {output_path}")
        return 0 if overall_pass else 1
    finally:
        stop_process_safe(proc)
        close_handle_silent(stdout_handle)
        close_handle_silent(stderr_handle)


if __name__ == "__main__":
    raise SystemExit(main())
