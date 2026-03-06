param(
    [Parameter(Mandatory = $true)]
    [string]$BaselineLicensePath,

    [Parameter(Mandatory = $true)]
    [string]$TargetLicensePath,

    [Parameter(Mandatory = $false)]
    [string]$BaselinePolicyPath = "",

    [Parameter(Mandatory = $false)]
    [string]$TargetPolicyPath = "",

    [Parameter(Mandatory = $false)]
    [string]$BaselinePackageVersion = "",

    [Parameter(Mandatory = $false)]
    [string]$TargetPackageVersion = "",

    [Parameter(Mandatory = $false)]
    [switch]$FailOnWarning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$warnings = New-Object System.Collections.Generic.List[string]

function Read-Json([string]$path) {
    if (-not (Test-Path $path)) {
        throw "File not found: $path"
    }
    return Get-Content -Raw -Path $path | ConvertFrom-Json
}

function Normalize-Features($features) {
    if ($null -eq $features) { return @() }
    return @($features | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { "$_".Trim().ToLowerInvariant() })
}

function Parse-SemVer([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }

    $normalized = $value.Trim()
    if ($normalized.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(1)
    }
    $normalized = $normalized.Split("-", 2)[0].Split("+", 2)[0]
    $parts = $normalized.Split(".")
    if ($parts.Length -lt 2 -or $parts.Length -gt 3) { return $null }

    try {
        $major = [int]$parts[0]
        $minor = [int]$parts[1]
        $patch = if ($parts.Length -eq 3) { [int]$parts[2] } else { 0 }
        return [Version]::new($major, $minor, $patch)
    }
    catch {
        return $null
    }
}

$baselineLicense = Read-Json -path $BaselineLicensePath
$targetLicense = Read-Json -path $TargetLicensePath

$baselineFeatures = Normalize-Features -features $baselineLicense.AllowedFeatures
$targetFeatures = Normalize-Features -features $targetLicense.AllowedFeatures

foreach ($feature in $baselineFeatures) {
    if (-not ($targetFeatures -contains $feature) -and -not ($targetFeatures -contains "*")) {
        throw "Compatibility failed: target license removed required feature '$feature'."
    }
}

if (-not [string]::IsNullOrWhiteSpace($BaselinePolicyPath)) {
    if ([string]::IsNullOrWhiteSpace($TargetPolicyPath) -or -not (Test-Path $TargetPolicyPath)) {
        throw "Compatibility failed: target policy missing while baseline policy exists."
    }

    $baselinePolicy = Read-Json -path $BaselinePolicyPath
    $targetPolicy = Read-Json -path $TargetPolicyPath

    if (($baselinePolicy.Enforcement.FailMode -eq "Hard") -and ($targetPolicy.Enforcement.FailMode -ne "Hard")) {
        throw "Compatibility failed: target policy relaxes fail mode from Hard."
    }
}

if (-not [string]::IsNullOrWhiteSpace($BaselinePackageVersion) -and -not [string]::IsNullOrWhiteSpace($TargetPackageVersion)) {
    $baselineVersion = Parse-SemVer -value $BaselinePackageVersion
    $targetVersion = Parse-SemVer -value $TargetPackageVersion

    if ($null -eq $baselineVersion -or $null -eq $targetVersion) {
        $warnings.Add("Warning: package version format invalid; semantic checks skipped.")
    }
    else {
        if ($targetVersion -lt $baselineVersion) {
            throw "Compatibility failed: target package version '$TargetPackageVersion' is lower than baseline '$BaselinePackageVersion'."
        }

        if ($targetVersion.Major -gt $baselineVersion.Major) {
            $warnings.Add("Warning: major package version jump from '$BaselinePackageVersion' to '$TargetPackageVersion'.")
        }
    }
}

if ($warnings.Count -gt 0) {
    foreach ($warning in $warnings) {
        Write-Host $warning
    }

    if ($FailOnWarning.IsPresent) {
        throw "Compatibility check failed due to warning-level findings."
    }
}

Write-Host "Enterprise upgrade compatibility check passed."
