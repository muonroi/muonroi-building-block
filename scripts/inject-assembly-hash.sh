#!/bin/bash
# Bash script to calculate and inject assembly hash for CodeIntegrityVerifier
# Usage: ./inject-assembly-hash.sh path/to/Muonroi.BuildingBlock.dll

set -e

ASSEMBLY_PATH="$1"

echo "═══════════════════════════════════════════════════════════"
echo "  Code Integrity Hash Injection"
echo "═══════════════════════════════════════════════════════════"
echo ""

# Verify assembly path provided
if [ -z "$ASSEMBLY_PATH" ]; then
    echo "❌ Error: Assembly path not provided"
    echo "Usage: $0 <path-to-assembly>"
    exit 1
fi

# Verify assembly exists
if [ ! -f "$ASSEMBLY_PATH" ]; then
    echo "❌ Error: Assembly not found at: $ASSEMBLY_PATH"
    exit 1
fi

echo "📦 Assembly: $ASSEMBLY_PATH"

# Calculate SHA256 hash
if command -v sha256sum >/dev/null 2>&1; then
    # Linux
    HASH_HEX=$(sha256sum "$ASSEMBLY_PATH" | awk '{print $1}')
elif command -v shasum >/dev/null 2>&1; then
    # macOS
    HASH_HEX=$(shasum -a 256 "$ASSEMBLY_PATH" | awk '{print $1}')
else
    echo "❌ Error: Neither sha256sum nor shasum found"
    exit 1
fi

# Convert hex to base64
HASH_BASE64=$(echo -n "$HASH_HEX" | xxd -r -p | base64)

echo "✓ Hash calculated: ${HASH_BASE64:0:32}..."
echo "  Full hash: $HASH_BASE64"
echo ""

# Find CodeIntegrityVerifier.cs
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
VERIFIER_PATH="$ROOT_DIR/src/Muonroi.BuildingBlock/Shared/License/CodeIntegrityVerifier.cs"

if [ ! -f "$VERIFIER_PATH" ]; then
    echo "❌ Error: CodeIntegrityVerifier.cs not found at: $VERIFIER_PATH"
    exit 1
fi

echo "📄 Verifier file: $VERIFIER_PATH"

# Get current hash
CURRENT_HASH=$(grep 'private const string ExpectedHash = ' "$VERIFIER_PATH" | sed -E 's/.*= "([^"]+)".*/\1/')
echo "  Current hash: $CURRENT_HASH"

# Replace hash using sed (cross-platform compatible)
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS requires empty string for -i
    sed -i '' "s|private const string ExpectedHash = \"[^\"]*\"|private const string ExpectedHash = \"$HASH_BASE64\"|" "$VERIFIER_PATH"
else
    # Linux
    sed -i "s|private const string ExpectedHash = \"[^\"]*\"|private const string ExpectedHash = \"$HASH_BASE64\"|" "$VERIFIER_PATH"
fi

echo "✓ Hash injected into CodeIntegrityVerifier.cs"
echo ""

# Display summary
echo "═══════════════════════════════════════════════════════════"
echo "  Summary"
echo "═══════════════════════════════════════════════════════════"
echo "Assembly:     $(basename "$ASSEMBLY_PATH")"
echo "Hash (SHA256): $HASH_BASE64"
echo "Status:       Injected ✓"
echo ""
echo "⚠ IMPORTANT: You must rebuild the assembly for the hash to be effective."
echo "   The injected hash will be compiled into the DLL."
echo ""
