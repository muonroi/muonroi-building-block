#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
CURRENT_DIR="${ROOT_DIR}/schema/distributedcache/contracts"
BASELINE_DIR="${ROOT_DIR}/schema/distributedcache/baseline"

python3 - "${CURRENT_DIR}" "${BASELINE_DIR}" <<'PY'
import glob
import json
import os
import sys

current_dir = sys.argv[1]
baseline_dir = sys.argv[2]

if not os.path.isdir(current_dir):
    raise SystemExit(f"Current contracts directory not found: {current_dir}")
if not os.path.isdir(baseline_dir):
    raise SystemExit(f"Baseline contracts directory not found: {baseline_dir}")

current_files = sorted(glob.glob(os.path.join(current_dir, "*.json")))
if not current_files:
    raise SystemExit("No Distributed Cache contract files found.")

errors = []
for current_path in current_files:
    name = os.path.basename(current_path)
    baseline_path = os.path.join(baseline_dir, name)
    if not os.path.exists(baseline_path):
        errors.append(f"[BREAKING] Missing baseline contract: {name}")
        continue

    with open(current_path, "r", encoding="utf-8") as f:
        current = json.load(f)
    with open(baseline_path, "r", encoding="utf-8") as f:
        baseline = json.load(f)

    current_version = int(current.get("version", 1))
    baseline_version = int(baseline.get("version", 1))
    if current_version < baseline_version:
        errors.append(
            f"[BREAKING] {name}: version regressed {current_version} < {baseline_version}"
        )

    current_props = current.get("properties", {})
    baseline_props = baseline.get("properties", {})
    current_required = set(current.get("required", []))
    baseline_required = set(baseline.get("required", []))

    removed_required = sorted(prop for prop in baseline_required if prop not in current_required)
    if removed_required:
        errors.append(
            f"[BREAKING] {name}: required fields removed: {', '.join(removed_required)}"
        )

    for prop, baseline_def in baseline_props.items():
        if prop not in current_props:
            errors.append(f"[BREAKING] {name}: property removed: {prop}")
            continue

        baseline_type = baseline_def.get("type")
        current_type = current_props[prop].get("type")
        if baseline_type != current_type:
            errors.append(
                f"[BREAKING] {name}: property '{prop}' type changed "
                f"from '{baseline_type}' to '{current_type}'"
            )

if errors:
    print("\n".join(errors))
    raise SystemExit(1)

print("Distributed Cache contract governance check passed.")
PY
