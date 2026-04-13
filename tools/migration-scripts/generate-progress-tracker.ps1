param(
    [string]$RepoRoot = ".",
    [string]$OutputPath = "tools/migration-scripts/migration-progress-tracker.csv"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
$srcRoot = Join-Path $root "src"
$csvPath = Join-Path $root $OutputPath

$packages = Get-ChildItem -Path $srcRoot -Directory |
    Where-Object { $_.Name -like "Muonroi.*" -and $_.Name -ne "Muonroi.BuildingBlock" } |
    Sort-Object Name

$rows = foreach ($pkg in $packages) {
    [PSCustomObject]@{
        Package         = $pkg.Name
        Phase           = ""
        ProjectSetup    = "Pending"
        ContentMigration= "Pending"
        NamingCleanup   = "Pending"
        Tests           = "Pending"
        Sample          = "Pending"
        Build           = "Pending"
        Notes           = ""
    }
}

$parent = Split-Path $csvPath -Parent
if (-not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent | Out-Null
}

$rows | Export-Csv -Path $csvPath -NoTypeInformation
Write-Host "Generated tracker: $csvPath"
Write-Host "Packages listed : $($packages.Count)"
