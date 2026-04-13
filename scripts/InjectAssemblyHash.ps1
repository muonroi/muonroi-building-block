# PowerShell script to calculate and inject assembly hash for CodeIntegrityVerifier
# Usage: .\InjectAssemblyHash.ps1 -AssemblyPath "path\to\Muonroi.BuildingBlock.dll"

param(
    [Parameter(Mandatory=$true)]
    [string]$AssemblyPath
)

Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "  Code Integrity Hash Injection" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host ""

# Verify assembly exists
if (-not (Test-Path $AssemblyPath)) {
    Write-Host "Error: Assembly not found at: $AssemblyPath" -ForegroundColor Red
    exit 1
}

Write-Host "Assembly: $AssemblyPath" -ForegroundColor White

# Calculate SHA256 hash
try {
    $bytes = [System.IO.File]::ReadAllBytes($AssemblyPath)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $hashBytes = $sha256.ComputeHash($bytes)
    $hashBase64 = [Convert]::ToBase64String($hashBytes)

    $preview = $hashBase64.Substring(0, [Math]::Min(32, $hashBase64.Length))
    Write-Host "Hash calculated: $preview..." -ForegroundColor Green
    Write-Host "Full hash: $hashBase64" -ForegroundColor Gray
    Write-Host ""
}
catch {
    Write-Host "Error calculating hash: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Find CodeIntegrityVerifier.cs
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$verifierPath = Join-Path $rootDir "src\Muonroi.BuildingBlock\Shared\License\CodeIntegrityVerifier.cs"

if (-not (Test-Path $verifierPath)) {
    Write-Host "Error: CodeIntegrityVerifier.cs not found at: $verifierPath" -ForegroundColor Red
    exit 1
}

Write-Host "Verifier file: $verifierPath" -ForegroundColor White

# Read current content
$content = Get-Content $verifierPath -Raw

# Check if placeholder exists
$pattern = 'private const string ExpectedHash = "([^"]+)";'
if ($content -match $pattern) {
    $currentHash = $matches[1]
    Write-Host "Current hash: $currentHash" -ForegroundColor Gray
} else {
    Write-Host "Error: ExpectedHash constant not found in CodeIntegrityVerifier.cs" -ForegroundColor Red
    exit 1
}

# Replace placeholder with actual hash
$newContent = $content -replace $pattern, "private const string ExpectedHash = `"$hashBase64`";"

# Write back to file
try {
    [System.IO.File]::WriteAllText($verifierPath, $newContent)
    Write-Host "Hash injected into CodeIntegrityVerifier.cs" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "Error writing file: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Display summary
Write-Host "===============================================================" -ForegroundColor Cyan
Write-Host "  Summary" -ForegroundColor Cyan
Write-Host "===============================================================" -ForegroundColor Cyan
$assemblyName = Split-Path -Leaf $AssemblyPath
Write-Host "Assembly:     $assemblyName" -ForegroundColor White
Write-Host "Hash (SHA256): $hashBase64" -ForegroundColor White
Write-Host "Status:       Injected" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: You must rebuild the assembly for the hash to be effective." -ForegroundColor Yellow
Write-Host "           The injected hash will be compiled into the DLL." -ForegroundColor Yellow
Write-Host ""
