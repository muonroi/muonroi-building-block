param(
    [string]$Organization = "Muonroi Local Test",
    [switch]$NoRunServer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$buildingBlockRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$serverRoot = Join-Path $buildingBlockRoot "tools\MockLicenseServer"
$generateScript = Join-Path $serverRoot "Generate-MockLicenseKeys.ps1"
$summaryPath = Join-Path $serverRoot "generated-licenses\generated-license-keys.json"

Write-Host "=== FLOW 2: PAID/ENTERPRISE KEYS + MASTER SERVER ===" -ForegroundColor Cyan
Write-Host "[1/2] Generate and register Paid/Enterprise child keys" -ForegroundColor Yellow
powershell -ExecutionPolicy Bypass -File $generateScript -Organization $Organization

if (Test-Path $summaryPath) {
    Write-Host ""
    Write-Host "Latest generated keys summary:" -ForegroundColor Yellow
    Get-Content -Path $summaryPath
}

if ($NoRunServer) {
    Write-Host ""
    Write-Host "Skip running server (-NoRunServer)." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "[2/2] Run MockLicenseServer (master key manages many child keys)" -ForegroundColor Yellow
Push-Location $serverRoot
dotnet run --project ".\MockLicenseServer.csproj"
Pop-Location
