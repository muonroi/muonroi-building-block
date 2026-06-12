param(
    [string]$RepoRoot = ".",
    [switch]$FailOnViolation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
$srcRoot = Join-Path $root "src"

$allowedDomains = @("Core", "Auth", "AuthZ", "Data", "Cache", "Msg", "Web", "Infra", "Tenant", "Rule")
$domainPattern = "^M(" + (($allowedDomains -join "|")) + ")[A-Z]\w*$"

$files = Get-ChildItem -Path $srcRoot -Recurse -File -Filter "*.cs" |
    Where-Object {
        $_.FullName -notlike "*\bin\*" -and
        $_.FullName -notlike "*\obj\*" -and
        $_.FullName -notlike "*\src\Muonroi.BuildingBlock\*"
    }

$violations = @()

foreach ($file in $files) {
    $lines = @(Get-Content -Path $file.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i].Trim()
        if ($line -match "^(public|internal)\s+(sealed\s+|abstract\s+|partial\s+)*((class|record|struct|enum))\s+([A-Za-z_]\w*)") {
            $typeName = $Matches[5]

            if ($typeName -match "^M[A-Z]") {
                if ($typeName -notmatch $domainPattern) {
                    $violations += [PSCustomObject]@{
                        File = $file.FullName
                        Line = ($i + 1)
                        Type = $typeName
                        Rule = "M-prefixed type must use domain-qualified pattern: $domainPattern"
                    }
                }
            }
        }
    }
}

if ($violations.Count -eq 0) {
    Write-Host "No M-prefix naming violations found."
    exit 0
}

Write-Host "M-prefix naming violations: $($violations.Count)"
$violations | Select-Object -First 200 | Format-Table -AutoSize

if ($FailOnViolation) {
    throw "M-prefix naming validation failed with $($violations.Count) violations."
}
