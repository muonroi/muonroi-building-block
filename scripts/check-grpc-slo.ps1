param(
    [Parameter(Mandatory = $true)]
    [string]$CurrentMetricsPath,

    [Parameter(Mandatory = $false)]
    [string]$BaselineMetricsPath = "",

    [Parameter(Mandatory = $false)]
    [double]$MaxP95IncreasePercent = 10.0,

    [Parameter(Mandatory = $false)]
    [double]$MaxErrorRate = 0.01
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-Metrics([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Metrics file not found: $path"
    }

    $json = Get-Content -Raw -Path $path | ConvertFrom-Json
    if ($null -eq $json.p95Ms -or $null -eq $json.errorRate) {
        throw "Metrics file '$path' must contain 'p95Ms' and 'errorRate'."
    }

    return [PSCustomObject]@{
        p95Ms = [double]$json.p95Ms
        errorRate = [double]$json.errorRate
    }
}

$current = Read-Metrics -path $CurrentMetricsPath

Write-Host "Current gRPC metrics: p95=$($current.p95Ms)ms, errorRate=$($current.errorRate)"

if ($current.errorRate -gt $MaxErrorRate) {
    throw "SLO failed: errorRate $($current.errorRate) > max $MaxErrorRate"
}

if (-not [string]::IsNullOrWhiteSpace($BaselineMetricsPath)) {
    $baseline = Read-Metrics -path $BaselineMetricsPath
    Write-Host "Baseline gRPC metrics: p95=$($baseline.p95Ms)ms, errorRate=$($baseline.errorRate)"

    if ($baseline.p95Ms -gt 0) {
        $increasePercent = (($current.p95Ms - $baseline.p95Ms) / $baseline.p95Ms) * 100.0
        Write-Host "p95 delta: $increasePercent%"

        if ($increasePercent -gt $MaxP95IncreasePercent) {
            throw "SLO failed: p95 increased by $increasePercent% > max $MaxP95IncreasePercent%"
        }
    }
}

Write-Host "gRPC SLO check passed."
