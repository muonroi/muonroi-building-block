param(
    [Parameter(Mandatory = $true)]
    [string]$CurrentMetricsPath,

    [Parameter(Mandatory = $false)]
    [string]$BaselineMetricsPath = "",

    [Parameter(Mandatory = $false)]
    [double]$MaxP95IncreasePercent = 10.0,

    [Parameter(Mandatory = $false)]
    [double]$MaxErrorRate = 0.01,

    [Parameter(Mandatory = $false)]
    [double]$MaxTamperDetections = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-Metrics([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Metrics file not found: $path"
    }

    $json = Get-Content -Raw -Path $path | ConvertFrom-Json
    if ($null -eq $json.p95Ms -or $null -eq $json.errorRate -or $null -eq $json.tamperDetections) {
        throw "Metrics file '$path' must contain 'p95Ms', 'errorRate', and 'tamperDetections'."
    }

    return [PSCustomObject]@{
        p95Ms = [double]$json.p95Ms
        errorRate = [double]$json.errorRate
        tamperDetections = [double]$json.tamperDetections
    }
}

$current = Read-Metrics -path $CurrentMetricsPath

Write-Host "Current Audit Trail metrics: p95=$($current.p95Ms)ms, errorRate=$($current.errorRate), tamperDetections=$($current.tamperDetections)"

if ($current.errorRate -gt $MaxErrorRate) {
    throw "SLO failed: errorRate $($current.errorRate) > max $MaxErrorRate"
}

if ($current.tamperDetections -gt $MaxTamperDetections) {
    throw "SLO failed: tamperDetections $($current.tamperDetections) > max $MaxTamperDetections"
}

if (-not [string]::IsNullOrWhiteSpace($BaselineMetricsPath)) {
    $baseline = Read-Metrics -path $BaselineMetricsPath
    Write-Host "Baseline Audit Trail metrics: p95=$($baseline.p95Ms)ms, errorRate=$($baseline.errorRate), tamperDetections=$($baseline.tamperDetections)"

    if ($baseline.p95Ms -gt 0) {
        $increasePercent = (($current.p95Ms - $baseline.p95Ms) / $baseline.p95Ms) * 100.0
        Write-Host "p95 delta: $increasePercent%"

        if ($increasePercent -gt $MaxP95IncreasePercent) {
            throw "SLO failed: p95 increased by $increasePercent% > max $MaxP95IncreasePercent%"
        }
    }
}

Write-Host "Audit Trail SLO check passed."
