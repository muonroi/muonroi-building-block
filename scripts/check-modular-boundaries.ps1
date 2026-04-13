param(
    [string]$RepoRoot = "."
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path $RepoRoot
$srcRoot = Join-Path $repo "src"

if (-not (Test-Path $srcRoot)) { throw "src directory not found at $srcRoot" }

# Commercial packages - OSS projects must NOT reference these
$commercialPackages = @(
    "Muonroi.Governance.Enterprise",
    "Muonroi.AuthZ",
    "Muonroi.Caching.Redis",
    "Muonroi.Messaging.MassTransit",
    "Muonroi.BackgroundJobs.Hangfire",
    "Muonroi.BackgroundJobs.Quartz",
    "Muonroi.SignalR",
    "Muonroi.Grpc",
    "Muonroi.Secrets",
    "Muonroi.Bff",
    "Muonroi.ServiceDiscovery.Consul",
    "Muonroi.RuleEngine.Runtime.Web",
    "Muonroi.RuleEngine.DecisionTable.Web",
    "Muonroi.UiEngine.Catalog",
    "Muonroi.BuildingBlock.All"
)

$violations = @()
$projects = Get-ChildItem $srcRoot -Recurse -Filter *.csproj -File

foreach ($project in $projects) {
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project.FullName)
    $content = Get-Content $project.FullName -Raw

    # Skip commercial packages themselves - they can reference each other
    $isCommercial = $content -match "<IsCommercialPackage>true</IsCommercialPackage>"
    if ($isCommercial) { continue }

    $referenceNames = @()

    $projectReferenceMatches = [regex]::Matches(
        $content,
        '<ProjectReference\s+[^>]*Include\s*=\s*"([^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    foreach ($match in $projectReferenceMatches) {
        $includePath = $match.Groups[1].Value
        if ([string]::IsNullOrWhiteSpace($includePath)) { continue }
        $referenceNames += [System.IO.Path]::GetFileNameWithoutExtension($includePath)
    }

    $packageReferenceMatches = [regex]::Matches(
        $content,
        '<PackageReference\s+[^>]*Include\s*=\s*"([^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    foreach ($match in $packageReferenceMatches) {
        $packageName = $match.Groups[1].Value
        if ([string]::IsNullOrWhiteSpace($packageName)) { continue }
        $referenceNames += $packageName.Trim()
    }

    $referenceNames = $referenceNames | Select-Object -Unique

    foreach ($reference in $referenceNames) {
        if ($commercialPackages -contains $reference) {
            $violations += "$($project.FullName.Substring($repo.Path.Length + 1)) references commercial package: $reference"
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "OSS boundary check FAILED. OSS projects must not reference commercial packages:"
    $violations | ForEach-Object { Write-Host " - $_" }
    exit 1
}

Write-Host "OSS boundary check PASSED: no OSS project references commercial packages."
