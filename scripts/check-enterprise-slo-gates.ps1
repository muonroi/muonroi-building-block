param(
    [Parameter(Mandatory = $true)]
    [string]$CurrentMetricsRoot,

    [Parameter(Mandatory = $false)]
    [string]$BaselineMetricsRoot = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet("balanced", "strict", "regulated")]
    [string]$PresetName = "balanced",

    [Parameter(Mandatory = $false)]
    [string]$PresetPath = "",

    [Parameter(Mandatory = $false)]
    [string[]]$SkipModules = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-PresetPath {
    param(
        [string]$name,
        [string]$overridePath
    )

    if (-not [string]::IsNullOrWhiteSpace($overridePath)) {
        return (Resolve-Path -Path $overridePath).Path
    }

    $scriptRoot = Split-Path -Parent $MyInvocation.PSCommandPath
    $repoRoot = Split-Path -Parent $scriptRoot
    $defaultPath = Join-Path $repoRoot "deploy/enterprise/slo-presets/$name.json"
    return (Resolve-Path -Path $defaultPath).Path
}

function Read-Preset {
    param([string]$path)
    if (-not (Test-Path $path)) {
        throw "SLO preset file not found: $path"
    }

    return Get-Content -Raw -Path $path | ConvertFrom-Json
}

function Build-BaselinePath {
    param(
        [string]$root,
        [string]$fileName
    )
    if ([string]::IsNullOrWhiteSpace($root)) {
        return ""
    }
    return (Join-Path $root $fileName)
}

function Invoke-IfEnabled {
    param(
        [string]$moduleName,
        [scriptblock]$operation
    )

    if ($SkipModules -contains $moduleName) {
        Write-Host "Skipping module: $moduleName"
        return
    }

    & $operation
}

$resolvedPresetPath = Resolve-PresetPath -name $PresetName -overridePath $PresetPath
$preset = Read-Preset -path $resolvedPresetPath

if (-not (Test-Path $CurrentMetricsRoot)) {
    throw "Current metrics root not found: $CurrentMetricsRoot"
}

$scriptRoot = Split-Path -Parent $MyInvocation.PSCommandPath

Invoke-IfEnabled -moduleName "grpc" -operation {
    & (Join-Path $scriptRoot "check-grpc-slo.ps1") `
        -CurrentMetricsPath (Join-Path $CurrentMetricsRoot "grpc.json") `
        -BaselineMetricsPath (Build-BaselinePath -root $BaselineMetricsRoot -fileName "grpc.json") `
        -MaxP95IncreasePercent ([double]$preset.grpc.maxP95IncreasePercent) `
        -MaxErrorRate ([double]$preset.grpc.maxErrorRate)
}

Invoke-IfEnabled -moduleName "messagebus" -operation {
    & (Join-Path $scriptRoot "check-messagebus-slo.ps1") `
        -CurrentMetricsPath (Join-Path $CurrentMetricsRoot "messagebus.json") `
        -BaselineMetricsPath (Build-BaselinePath -root $BaselineMetricsRoot -fileName "messagebus.json") `
        -MaxP95IncreasePercent ([double]$preset.messageBus.maxP95IncreasePercent) `
        -MaxErrorRate ([double]$preset.messageBus.maxErrorRate) `
        -MaxLag ([double]$preset.messageBus.maxLag)
}

Invoke-IfEnabled -moduleName "distributed-cache" -operation {
    & (Join-Path $scriptRoot "check-distributed-cache-slo.ps1") `
        -CurrentMetricsPath (Join-Path $CurrentMetricsRoot "distributed-cache.json") `
        -BaselineMetricsPath (Build-BaselinePath -root $BaselineMetricsRoot -fileName "distributed-cache.json") `
        -MaxP95IncreasePercent ([double]$preset.distributedCache.maxP95IncreasePercent) `
        -MaxErrorRate ([double]$preset.distributedCache.maxErrorRate) `
        -MinHitRate ([double]$preset.distributedCache.minHitRate)
}

Invoke-IfEnabled -moduleName "audittrail" -operation {
    & (Join-Path $scriptRoot "check-audittrail-slo.ps1") `
        -CurrentMetricsPath (Join-Path $CurrentMetricsRoot "audittrail.json") `
        -BaselineMetricsPath (Build-BaselinePath -root $BaselineMetricsRoot -fileName "audittrail.json") `
        -MaxP95IncreasePercent ([double]$preset.auditTrail.maxP95IncreasePercent) `
        -MaxErrorRate ([double]$preset.auditTrail.maxErrorRate) `
        -MaxTamperDetections ([double]$preset.auditTrail.maxTamperDetections)
}

Invoke-IfEnabled -moduleName "antitamper" -operation {
    & (Join-Path $scriptRoot "check-antitamper-slo.ps1") `
        -CurrentMetricsPath (Join-Path $CurrentMetricsRoot "antitamper.json") `
        -BaselineMetricsPath (Build-BaselinePath -root $BaselineMetricsRoot -fileName "antitamper.json") `
        -MaxP95IncreasePercent ([double]$preset.antiTampering.maxP95IncreasePercent) `
        -MaxErrorRate ([double]$preset.antiTampering.maxErrorRate) `
        -MaxTamperDetections ([double]$preset.antiTampering.maxTamperDetections)
}

Write-Host "Enterprise SLO gate check passed using preset '$($preset.name)'."
