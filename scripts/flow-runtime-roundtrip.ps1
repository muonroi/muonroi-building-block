param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "flow-runtime-roundtrip.py"
if (-not (Test-Path $scriptPath)) {
    throw "Script not found: $scriptPath"
}

& py -3 $scriptPath @Args
exit $LASTEXITCODE
