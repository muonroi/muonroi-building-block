<#
.SYNOPSIS
    Pack all 4 Muonroi.Pdf packages to verifiable CPM-compliant .nupkg artifacts.

.DESCRIPTION
    Runs dotnet pack -c Release on each of the 4 Pdf package projects, then asserts:
    - All 4 .nupkg files are produced with the correct version (1.0.0-alpha.N)
    - No inline Version element exists in any of the 4 Pdf csprojs (CPM compliance / PKG-07)
    - Exits 0 only when all assertions pass.

    PUBLISH NOTE: dotnet nuget push is OUT OF SCOPE - no nuget.config/feed in repo.
    Push is a release-pipeline step (locked decision 4 from 07-05-PLAN.md).
#>

param()

$ErrorActionPreference = "Continue"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = (Get-Location).Path }
$rootPath = Split-Path -Parent $scriptPath

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "  Muonroi.Pdf Package Pack + CPM Compliance Gate  " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""

# -----------------------------------------------------------------------
# 1. CPM compliance: no inline Version element in any Pdf csproj
# -----------------------------------------------------------------------
Write-Host "[1/3] Checking CPM compliance (no inline Version in Pdf csprojs)..." -ForegroundColor Yellow

$pdfCsprojs = @(
    "src\Muonroi.Pdf.Abstractions\Muonroi.Pdf.Abstractions.csproj",
    "src\Muonroi.Pdf\Muonroi.Pdf.csproj",
    "src\Muonroi.Pdf.Governance\Muonroi.Pdf.Governance.csproj",
    "src\Muonroi.Pdf.Enterprise\Muonroi.Pdf.Enterprise.csproj"
)

$cpmViolations = @()
foreach ($rel in $pdfCsprojs) {
    $fullPath = Join-Path $rootPath $rel
    if (-not (Test-Path $fullPath)) {
        Write-Host "  ERROR: csproj not found: $rel" -ForegroundColor Red
        exit 1
    }
    $content = Get-Content $fullPath -Raw
    # Use [xml] parser to check for a Version element (avoids regex with angle brackets)
    try {
        $xml = [xml]$content
        $versionNode = $xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ -ne $null -and $_.Trim() -ne "" }
        if ($versionNode) {
            $cpmViolations += $rel
        }
    } catch {
        # Fallback: simple string check
        if ($content.Contains("<Version>")) {
            $parts = $content.Split("<Version>", [System.StringSplitOptions]::None)
            if ($parts.Count -gt 1) {
                $after = $parts[1]
                # Confirm it's not VersionPrefix/VersionSuffix/AssemblyVersion/FileVersion
                # by checking what precedes it — a standalone <Version> tag
                if ($content -match '(?<![a-zA-Z])<Version>[^<]+</Version>') {
                    $cpmViolations += $rel
                }
            }
        }
    }
}

if ($cpmViolations.Count -gt 0) {
    Write-Host "  CPM VIOLATION: Pdf csprojs with inline Version element:" -ForegroundColor Red
    $cpmViolations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "  Remove inline Version -- use Directory.Build.props VersionPrefix/VersionSuffix." -ForegroundColor Red
    exit 1
}
Write-Host "  CPM compliance OK: no inline Version element found in any Pdf csproj." -ForegroundColor Green

# -----------------------------------------------------------------------
# 2. Read expected version from Directory.Build.props
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "[2/3] Resolving expected version from Directory.Build.props..." -ForegroundColor Yellow

$buildPropsPath = Join-Path $rootPath "Directory.Build.props"
$buildPropsContent = Get-Content $buildPropsPath -Raw

$prefixMatch = [regex]::Match($buildPropsContent, '<VersionPrefix>([^<]+)</VersionPrefix>')
$suffixMatch = [regex]::Match($buildPropsContent, '<VersionSuffix>([^<]+)</VersionSuffix>')

if (-not $prefixMatch.Success -or -not $suffixMatch.Success) {
    Write-Host "  ERROR: Could not parse VersionPrefix/VersionSuffix from Directory.Build.props" -ForegroundColor Red
    exit 1
}

$versionPrefix = $prefixMatch.Groups[1].Value.Trim()
$versionSuffix = $suffixMatch.Groups[1].Value.Trim()
$expectedVersion = $versionPrefix + "-" + $versionSuffix
Write-Host ("  Expected version: " + $expectedVersion) -ForegroundColor Cyan

# -----------------------------------------------------------------------
# 3. dotnet pack each Pdf project and assert .nupkg artifacts
# -----------------------------------------------------------------------
Write-Host ""
Write-Host "[3/3] Packing all 4 Pdf packages..." -ForegroundColor Yellow

$producedPackages = [System.Collections.ArrayList]::new()
$packErrors = [System.Collections.ArrayList]::new()

foreach ($rel in $pdfCsprojs) {
    $fullPath = Join-Path $rootPath $rel
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($fullPath)

    Write-Host ""
    Write-Host ("  Packing: " + $projectName) -ForegroundColor White

    & dotnet pack $fullPath -c Release -m:1 -nodereuse:false --nologo
    $packExit = $LASTEXITCODE
    if ($packExit -ne 0) {
        [void]$packErrors.Add(("dotnet pack failed for: " + $rel + " (exit code " + $packExit + ")"))
        continue
    }

    # Find the produced .nupkg -- default output is bin/Release/
    $projectDir = Split-Path $fullPath -Parent
    $nupkgFileName = $projectName + "." + $expectedVersion + ".nupkg"
    $nupkgPath = Join-Path $projectDir ("bin\Release\" + $nupkgFileName)
    if (Test-Path $nupkgPath) {
        Write-Host ("  Produced: " + $nupkgFileName) -ForegroundColor Green
        [void]$producedPackages.Add($nupkgPath)
    } else {
        # Fallback: search for any .nupkg in bin/Release to diagnose version mismatch
        $binRelease = Join-Path $projectDir "bin\Release"
        $anyNupkg = @(Get-ChildItem $binRelease -Filter "*.nupkg" -ErrorAction SilentlyContinue)
        if ($anyNupkg.Count -gt 0) {
            [void]$packErrors.Add(("Version mismatch for " + $projectName + ". Expected: " + $nupkgFileName + ". Found: " + ($anyNupkg.Name -join ", ")))
        } else {
            [void]$packErrors.Add(("No .nupkg produced for " + $projectName + ". Expected: " + $nupkgPath))
        }
    }
}

Write-Host ""

if ($packErrors.Count -gt 0) {
    Write-Host "PACK ERRORS:" -ForegroundColor Red
    $packErrors | ForEach-Object { Write-Host ("  - " + $_) -ForegroundColor Red }
    exit 1
}

if ($producedPackages.Count -ne 4) {
    Write-Host ("ERROR: Expected 4 .nupkg artifacts, found " + $producedPackages.Count) -ForegroundColor Red
    exit 1
}

# -----------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "  Pack Complete -- All Assertions Passed           " -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Artifacts produced:" -ForegroundColor White
$producedPackages | ForEach-Object { Write-Host ("  " + $_) -ForegroundColor Green }
Write-Host ""
Write-Host "PUBLISH (dotnet nuget push) is OUT OF SCOPE -- no nuget.config/feed in repo." -ForegroundColor Yellow
Write-Host "Push is a release-pipeline step (locked decision 4 from 07-05-PLAN.md)." -ForegroundColor Yellow
Write-Host ""
exit 0
