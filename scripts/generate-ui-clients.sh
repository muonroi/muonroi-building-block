#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Generate frontend/backend API clients from OpenAPI.

Usage:
  ./scripts/generate-ui-clients.sh \
    --openapi <openapi-file-or-url> \
    [--framework angular|react|mvc|all] \
    [--output <output-dir>]

Examples:
  ./scripts/generate-ui-clients.sh --openapi ./_tmp/openapi.json --framework all
  ./scripts/generate-ui-clients.sh --openapi http://localhost:5000/swagger/v1/swagger.json --framework angular

Notes:
  - Primary runtime is shell (.sh) for macOS/Linux portability.
  - Requires openapi-generator-cli in PATH or Docker as fallback.
EOF
}

OPENAPI_SOURCE=""
FRAMEWORK="all"
OUTPUT_DIR="./_tmp/generated-clients"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --openapi)
      OPENAPI_SOURCE="${2:-}"
      shift 2
      ;;
    --framework)
      FRAMEWORK="${2:-}"
      shift 2
      ;;
    --output)
      OUTPUT_DIR="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "${OPENAPI_SOURCE}" ]]; then
  echo "--openapi is required." >&2
  usage
  exit 1
fi

case "${FRAMEWORK}" in
  angular|react|mvc|all) ;;
  *)
    echo "Invalid --framework value: ${FRAMEWORK}" >&2
    exit 1
    ;;
esac

mkdir -p "${OUTPUT_DIR}"

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "${TMP_DIR}"' EXIT

SPEC_FILE="${OPENAPI_SOURCE}"
if [[ "${OPENAPI_SOURCE}" =~ ^https?:// ]]; then
  SPEC_FILE="${TMP_DIR}/openapi.json"
  curl -fsSL "${OPENAPI_SOURCE}" -o "${SPEC_FILE}"
fi

if [[ ! -f "${SPEC_FILE}" ]]; then
  echo "OpenAPI file not found: ${SPEC_FILE}" >&2
  exit 1
fi

run_openapi_generator() {
  local generator="$1"
  local outdir="$2"
  local extra="$3"

  mkdir -p "${outdir}"

  if command -v openapi-generator-cli >/dev/null 2>&1; then
    openapi-generator-cli generate \
      -g "${generator}" \
      -i "${SPEC_FILE}" \
      -o "${outdir}" \
      ${extra}
    return
  fi

  if command -v docker >/dev/null 2>&1; then
    local spec_for_docker="${SPEC_FILE}"
    case "${spec_for_docker}" in
      /*) ;;
      *) spec_for_docker="$(pwd)/${spec_for_docker}" ;;
    esac

    if [[ "${spec_for_docker}" != "$(pwd)"/* ]]; then
      cp "${SPEC_FILE}" "${TMP_DIR}/openapi-for-docker.json"
      spec_for_docker="${TMP_DIR}/openapi-for-docker.json"
    fi

    local local_spec
    local local_out
    local_spec="${spec_for_docker#$(pwd)/}"
    local_out="${outdir#$(pwd)/}"

    if [[ "${local_spec}" == "${spec_for_docker}" ]]; then
      local_spec="${TMP_DIR#$(pwd)/}/openapi-for-docker.json"
    fi

    if [[ "${local_out}" == /* ]]; then
      echo "When Docker fallback is used, --output must be inside current repository." >&2
      exit 1
    fi

    docker run --rm \
      -v "$(pwd):/local" \
      openapitools/openapi-generator-cli:v7.9.0 generate \
      -g "${generator}" \
      -i "/local/${local_spec#./}" \
      -o "/local/${local_out#./}" \
      ${extra}
    return
  fi

  echo "openapi-generator-cli or Docker is required." >&2
  exit 1
}

if [[ "${FRAMEWORK}" == "angular" || "${FRAMEWORK}" == "all" ]]; then
  run_openapi_generator \
    "typescript-angular" \
    "${OUTPUT_DIR}/angular" \
    "--additional-properties=ngVersion=18.0.0,npmName=@muonroi/api-client,npmVersion=0.0.1,withInterfaces=true"
fi

if [[ "${FRAMEWORK}" == "react" || "${FRAMEWORK}" == "all" ]]; then
  run_openapi_generator \
    "typescript-fetch" \
    "${OUTPUT_DIR}/react" \
    "--additional-properties=npmName=@muonroi/api-client,npmVersion=0.0.1,typescriptThreePlus=true,modelPropertyNaming=camelCase"
fi

if [[ "${FRAMEWORK}" == "mvc" || "${FRAMEWORK}" == "all" ]]; then
  run_openapi_generator \
    "csharp" \
    "${OUTPUT_DIR}/mvc-csharp" \
    "--additional-properties=packageName=Muonroi.UiClient,targetFramework=net9.0,nullableReferenceTypes=true"
fi

echo "Client generation completed. Output: ${OUTPUT_DIR}"
