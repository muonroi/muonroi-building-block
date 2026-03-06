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
    [double]$MaxLag = 1000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-Metrics([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Metrics file not found: $path"
    }

    $json = Get-Content -Raw -Path $path | ConvertFrom-Json
    if ($null -eq $json.p95Ms -or $null -eq $json.errorRate -or $null -eq $json.consumerLag) {
        throw "Metrics file '$path' must contain 'p95Ms', 'errorRate', and 'consumerLag'."
    }

    return [PSCustomObject]@{
        p95Ms = [double]$json.p95Ms
        errorRate = [double]$json.errorRate
        consumerLag = [double]$json.consumerLag
    }
}

$current = Read-Metrics -path $CurrentMetricsPath

Write-Host "Current Message Bus metrics: p95=$($current.p95Ms)ms, errorRate=$($current.errorRate), lag=$($current.consumerLag)"

if ($current.errorRate -gt $MaxErrorRate) {
    throw "SLO failed: errorRate $($current.errorRate) > max $MaxErrorRate"
}

if ($current.consumerLag -gt $MaxLag) {
    throw "SLO failed: consumerLag $($current.consumerLag) > max $MaxLag"
}

if (-not [string]::IsNullOrWhiteSpace($BaselineMetricsPath)) {
    $baseline = Read-Metrics -path $BaselineMetricsPath
    Write-Host "Baseline Message Bus metrics: p95=$($baseline.p95Ms)ms, errorRate=$($baseline.errorRate), lag=$($baseline.consumerLag)"

    if ($baseline.p95Ms -gt 0) {
        $increasePercent = (($current.p95Ms - $baseline.p95Ms) / $baseline.p95Ms) * 100.0
        Write-Host "p95 delta: $increasePercent%"

        if ($increasePercent -gt $MaxP95IncreasePercent) {
            throw "SLO failed: p95 increased by $increasePercent% > max $MaxP95IncreasePercent%"
        }
    }
}

Write-Host "Message Bus SLO check passed."
