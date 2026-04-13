param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "check-runtime-parity.py"
if (-not (Test-Path $scriptPath)) {
    throw "Script not found: $scriptPath"
}

& py -3 $scriptPath @Args
exit $LASTEXITCODE
