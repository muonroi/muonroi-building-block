param(
    [string]$RepoRoot = ".",
    [switch]$DowngradePackageReferences
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
$scanDirs = @("src", "tests", "tools", "Samples")
$patterns = @("*.csproj")

$csprojFiles = @()
foreach ($dir in $scanDirs) {
    $full = Join-Path $root $dir
    if (Test-Path $full) {
        $csprojFiles += Get-ChildItem -Path $full -Recurse -File -Include $patterns
    }
}

$replacements = @(
    @{ Pattern = '(<TargetFramework>)net9\.0(</TargetFramework>)'; Replacement = '${1}net8.0${2}' }
)

if ($DowngradePackageReferences) {
    $replacements += @(
        @{ Pattern = '(Include="Microsoft\.Extensions\.[^"]+"\s+Version=")9\.[^"]*(")'; Replacement = '${1}8.0.*${2}' },
        @{ Pattern = '(Include="Microsoft\.EntityFrameworkCore[^"]*"\s+Version=")9\.[^"]*(")'; Replacement = '${1}8.0.*${2}' },
        @{ Pattern = '(Include="Microsoft\.AspNetCore[^"]*"\s+Version=")9\.[^"]*(")'; Replacement = '${1}8.0.*${2}' },
        @{ Pattern = '(Include="Microsoft\.NET\.Test\.Sdk"\s+Version=")18\.[^"]*(")'; Replacement = '${1}17.11.*${2}' }
    )
}

$changed = 0
foreach ($file in $csprojFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $updated = $content

    foreach ($entry in $replacements) {
        $updated = [regex]::Replace($updated, $entry.Pattern, $entry.Replacement)
    }

    if ($updated -ne $content) {
        Set-Content -Path $file.FullName -Value $updated -NoNewline
        $changed++
        Write-Host "Updated: $($file.FullName)"
    }
}

Write-Host ""
Write-Host "Total .csproj scanned : $($csprojFiles.Count)"
Write-Host "Total .csproj updated : $changed"
