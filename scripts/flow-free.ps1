param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$OutputRoot,
    [string[]]$TemplateRoots,
    [string[]]$TemplateShortNames,
    [switch]$SkipRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-TemplateDefinitions {
    param(
        [string]$WorkspaceRoot,
        [string[]]$ExplicitRoots,
        [string[]]$RequestedShortNames
    )

    $roots = New-Object System.Collections.Generic.List[string]
    if ($ExplicitRoots -and $ExplicitRoots.Count -gt 0) {
        foreach ($root in $ExplicitRoots) {
            if ([string]::IsNullOrWhiteSpace($root)) { continue }
            if (Test-Path $root) {
                $roots.Add((Resolve-Path $root).Path)
            }
        }
    }
    else {
        foreach ($candidate in @(
                (Join-Path $WorkspaceRoot "Muonroi.BaseTemplate"),
                (Join-Path $WorkspaceRoot "Muonroi.Modular.Template"),
                (Join-Path $WorkspaceRoot "Muonroi.Microservices.Template"),
                (Join-Path $WorkspaceRoot "Muonroi.Base.Template")
            )) {
            if (Test-Path $candidate) {
                $roots.Add((Resolve-Path $candidate).Path)
            }
        }
    }

    $definitions = New-Object System.Collections.Generic.List[object]
    foreach ($root in @($roots | Sort-Object -Unique)) {
        $templateJsonPath = Join-Path $root ".template.config\template.json"
        if (-not (Test-Path $templateJsonPath)) { continue }
        $templateJson = Get-Content -Path $templateJsonPath -Raw | ConvertFrom-Json
        $shortName = [string]$templateJson.shortName
        if ([string]::IsNullOrWhiteSpace($shortName)) { continue }

        if ($RequestedShortNames -and $RequestedShortNames.Count -gt 0 -and
            -not ($RequestedShortNames -contains $shortName)) {
            continue
        }

        $identity = [string]$templateJson.identity
        $nameSeed = [System.IO.Path]::GetFileName($root).Replace(".", "")
        if (-not [string]::IsNullOrWhiteSpace($identity)) {
            $identityParts = $identity -split '\.'
            $identityTail = $null
            if ($identityParts.Count -ge 2) {
                $identityTail = ($identityParts[-2..-1] -join "")
            }
            elseif ($identityParts.Count -eq 1) {
                $identityTail = $identityParts[0]
            }
            if (-not [string]::IsNullOrWhiteSpace($identityTail)) {
                $nameSeed = $identityTail.Replace(".", "")
            }
        }

        $definitions.Add([pscustomobject]@{
                RootPath   = $root
                ShortName  = $shortName
                Identity   = $identity
                NameSeed   = $nameSeed
            })
    }

    return $definitions.ToArray()
}

function Wait-ForSwagger {
    param(
        [string]$ProjectPath,
        [int]$Port
    )

    $url = "http://127.0.0.1:$Port/swagger/index.html"
    $process = Start-Process dotnet -ArgumentList @(
        "run", "--no-build", "--project", $ProjectPath, "--urls", "http://127.0.0.1:$Port"
    ) -PassThru

    $isOk = $false
    try {
        for ($i = 0; $i -lt 45; $i++) {
            Start-Sleep -Seconds 2
            try {
                $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 3
                if ($response.StatusCode -eq 200) {
                    $isOk = $true
                    break
                }
            }
            catch {
                if ($process.HasExited) { break }
            }
        }
    }
    finally {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }

    if (-not $isOk) {
        throw "Run smoke test failed for '$ProjectPath' on port $Port."
    }
}

function Find-SmokeProject {
    param([string]$ProjectRoot)

    $webProjects = @(Get-ChildItem -Path $ProjectRoot -Recurse -Filter "*.csproj" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
        Where-Object { (Get-Content -Path $_.FullName -Raw) -match 'Project Sdk="Microsoft\.NET\.Sdk\.Web"' })

    if ($webProjects.Count -eq 0) { return $null }

    $preferred = $webProjects |
        Where-Object { $_.Name -match '(API|Host|Catalog)\.csproj$' } |
        Select-Object -First 1

    if ($null -ne $preferred) { return $preferred.FullName }
    return ($webProjects | Select-Object -First 1).FullName
}

$buildingBlockRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workspaceRoot = (Resolve-Path (Join-Path $buildingBlockRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $workspaceRoot "_tmp\flow_free_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

if (-not $TemplateRoots -or $TemplateRoots.Count -eq 0) {
    $TemplateRoots = @(
        (Join-Path $workspaceRoot "Muonroi.BaseTemplate"),
        (Join-Path $workspaceRoot "Muonroi.Modular.Template"),
        (Join-Path $workspaceRoot "Muonroi.Microservices.Template")
    )
}

$templateDefinitions = Get-TemplateDefinitions -WorkspaceRoot $workspaceRoot -ExplicitRoots $TemplateRoots -RequestedShortNames $TemplateShortNames
if ($templateDefinitions.Count -eq 0) {
    throw "No template definitions detected. Verify template repositories contain .template.config/template.json."
}

Write-Host "=== FLOW 1: FREE MODE ===" -ForegroundColor Cyan
Write-Host "[1/5] Bump and reinstall templates to version $Version" -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "bump-version.ps1") -Version $Version -TemplateRoots $TemplateRoots

$generatedProjects = New-Object System.Collections.Generic.List[object]
$timestamp = Get-Date -Format 'HHmmss'

Write-Host "[2/5] Create projects from detected templates" -ForegroundColor Yellow
for ($i = 0; $i -lt $templateDefinitions.Count; $i++) {
    $template = $templateDefinitions[$i]
    $name = "Free$($template.NameSeed)$timestamp$($i + 1)"
    $root = Join-Path $OutputRoot $name
    dotnet new $template.ShortName -n $name -o $root
    $generatedProjects.Add([pscustomobject]@{
            Name      = $name
            RootPath  = $root
            ShortName = $template.ShortName
        })
}

Write-Host "[3/5] Build generated solutions/projects" -ForegroundColor Yellow
foreach ($project in $generatedProjects) {
    $solutions = @(Get-ChildItem -Path $project.RootPath -Filter "*.sln" -File)
    if ($solutions.Count -gt 0) {
        foreach ($solution in $solutions) {
            dotnet build $solution.FullName
        }
        continue
    }

    $projectFiles = @(Get-ChildItem -Path $project.RootPath -Recurse -Filter "*.csproj" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" })
    foreach ($projectFile in $projectFiles) {
        dotnet build $projectFile.FullName
    }
}

Write-Host "[4/5] Run EF init/update when scripts\\ef.cmd exists" -ForegroundColor Yellow
foreach ($project in $generatedProjects) {
    $efScript = Join-Path $project.RootPath "scripts\ef.cmd"
    if (-not (Test-Path $efScript)) {
        Write-Host "  Skipped EF for $($project.Name): scripts\\ef.cmd not found."
        continue
    }

    Push-Location $project.RootPath
    try {
        cmd /c "scripts\ef.cmd init && scripts\ef.cmd update"
    }
    finally {
        Pop-Location
    }
}

if (-not $SkipRun) {
    Write-Host "[5/5] Run smoke (swagger reachable)" -ForegroundColor Yellow
    $port = 7301
    foreach ($project in $generatedProjects) {
        $smokeProjectPath = Find-SmokeProject -ProjectRoot $project.RootPath
        if ([string]::IsNullOrWhiteSpace($smokeProjectPath)) {
            Write-Host "  Skipped smoke for $($project.Name): no ASP.NET Core web project detected."
            continue
        }

        Wait-ForSwagger -ProjectPath $smokeProjectPath -Port $port
        $port++
    }
}

Write-Host ""
Write-Host "Flow Free completed successfully." -ForegroundColor Green
Write-Host "Output root: $OutputRoot"
foreach ($project in $generatedProjects) {
    Write-Host "  [$($project.ShortName)] $($project.RootPath)"
}
