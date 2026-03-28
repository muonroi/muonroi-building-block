#!/usr/bin/env bash
set -euo pipefail

if ! command -v buf >/dev/null 2>&1; then
  echo "buf is not installed."
  exit 1
fi

if ! find . -type f -name '*.proto' -not -path './**/bin/*' -not -path './**/obj/*' -not -path './templates/*' | grep -q .; then
  echo "No .proto files found. Skipping Buf lint/breaking checks."
  exit 0
fi

echo "Running buf lint..."
buf lint

if [[ -n "${GITHUB_BASE_REF:-}" && -n "${GITHUB_REPOSITORY:-}" ]]; then
  echo "Running buf breaking against base branch: ${GITHUB_BASE_REF}"
  buf breaking --against "https://github.com/${GITHUB_REPOSITORY}.git#branch=${GITHUB_BASE_REF}"
  exit 0
fi

if git rev-parse --verify HEAD~1 >/dev/null 2>&1; then
  echo "Running buf breaking against previous commit (HEAD~1)"
  buf breaking --against ".git#ref=HEAD~1"
else
  echo "Not enough git history for breaking check. Skipping."
fi
