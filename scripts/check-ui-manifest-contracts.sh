#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CURRENT_SCHEMA="${ROOT_DIR}/schema/ui/contracts/m-ui-manifest.v1.schema.json"
BASELINE_SCHEMA="${ROOT_DIR}/schema/ui/baseline/m-ui-manifest.v1.schema.json"
CURRENT_ENGINE_SCHEMA="${ROOT_DIR}/schema/ui/contracts/m-ui-engine.v1.schema.json"
BASELINE_ENGINE_SCHEMA="${ROOT_DIR}/schema/ui/baseline/m-ui-engine.v1.schema.json"

if [[ ! -f "${CURRENT_SCHEMA}" ]]; then
  echo "Missing current schema: ${CURRENT_SCHEMA}" >&2
  exit 1
fi

if [[ ! -f "${BASELINE_SCHEMA}" ]]; then
  echo "Missing baseline schema: ${BASELINE_SCHEMA}" >&2
  exit 1
fi

if [[ ! -f "${CURRENT_ENGINE_SCHEMA}" ]]; then
  echo "Missing current schema: ${CURRENT_ENGINE_SCHEMA}" >&2
  exit 1
fi

if [[ ! -f "${BASELINE_ENGINE_SCHEMA}" ]]; then
  echo "Missing baseline schema: ${BASELINE_ENGINE_SCHEMA}" >&2
  exit 1
fi

if ! grep -q '"const": "mui.manifest.v1"' "${CURRENT_SCHEMA}"; then
  echo "Current schema does not pin schemaVersion const to mui.manifest.v1" >&2
  exit 1
fi

if ! diff -u "${BASELINE_SCHEMA}" "${CURRENT_SCHEMA}" >/tmp/m-ui-manifest.diff 2>&1; then
  echo "UI manifest schema drift detected." >&2
  cat /tmp/m-ui-manifest.diff >&2
  exit 1
fi

if ! grep -q '"const": "mui.engine.v1"' "${CURRENT_ENGINE_SCHEMA}"; then
  echo "Current engine schema does not pin schemaVersion const to mui.engine.v1" >&2
  exit 1
fi

if ! diff -u "${BASELINE_ENGINE_SCHEMA}" "${CURRENT_ENGINE_SCHEMA}" >/tmp/m-ui-engine.diff 2>&1; then
  echo "UI engine schema drift detected." >&2
  cat /tmp/m-ui-engine.diff >&2
  exit 1
fi

echo "UI manifest and UI engine contract check passed."
