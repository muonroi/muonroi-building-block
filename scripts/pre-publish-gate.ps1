<#
.SYNOPSIS
    Pre-publish gate script for Muonroi Building Block.
    Ensures all tests pass and quality gates are met before NuGet publishing.

.DESCRIPTION
    This script runs the full test suite (excluding slow integration tests)
    and can be extended to run other checks like boundary validations.
#>

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$rootPath = Split-Path -Parent $scriptPath
$solutionFile = Join-Path $rootPath "Muonroi.BuildingBlock.sln"

Write-Host "--- Starting Muonroi Pre-Publish Gate ---" -ForegroundColor Cyan

# 1. Run Tests
Write-Host "[1/2] Running full test suite (Happy-case Coverage)..." -ForegroundColor Yellow
dotnet test $solutionFile -c Release --filter "Category!=SlowIntegration" --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Error "Test suite failed! Pre-publish gate blocked."
    exit $LASTEXITCODE
}

# 2. Boundary Checks (Optional but recommended)
Write-Host "[2/2] Running boundary and architecture checks..." -ForegroundColor Yellow
powershell.exe -NoProfile -File (Join-Path $scriptPath "check-modular-boundaries.ps1")

if ($LASTEXITCODE -ne 0) {
    Write-Error "Boundary checks failed! Pre-publish gate blocked."
    exit $LASTEXITCODE
}

Write-Host "--- ALL GATES PASSED ---" -ForegroundColor Green
Write-Host "Safe to proceed with NuGet pack and publish." -ForegroundColor Green
exit 0
