param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$LocalFeedPath,
    [string]$LegacyFeedPath,
    [string[]]$TemplateRoots,
    [switch]$SkipTemplateUpdate,
    [switch]$SkipTemplateInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Set-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$NewVersion
    )

    [xml]$xml = Get-Content -Path $ProjectPath
    $propertyGroups = @($xml.Project.PropertyGroup)
    if ($propertyGroups.Count -eq 0) {
        $propertyGroupNode = $xml.CreateElement("PropertyGroup")
        $null = $xml.Project.AppendChild($propertyGroupNode)
        $propertyGroups = @($xml.Project.PropertyGroup)
    }

    $targetPropertyGroup = $propertyGroups[0]
    $changed = $false
    $assemblyVersion = ($NewVersion -replace "-.*$", "").Trim()
    $segments = @($assemblyVersion.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries))
    if ($segments.Count -eq 1) {
        $assemblyVersion = "$assemblyVersion.0.0.0"
    }
    elseif ($segments.Count -eq 2) {
        $assemblyVersion = "$assemblyVersion.0.0"
    }
    elseif ($segments.Count -eq 3) {
        $assemblyVersion = "$assemblyVersion.0"
    }
    elseif ($segments.Count -gt 4) {
        $assemblyVersion = ($segments[0..3] -join '.')
    }

    foreach ($nodeName in @("Version", "AssemblyVersion", "FileVersion")) {
        $nodes = @($xml.SelectNodes("//Project/PropertyGroup/$nodeName"))
        $targetVersion = if ($nodeName -eq "Version") { $NewVersion } else { $assemblyVersion }
        if ($nodes.Count -eq 0) {
            $newNode = $xml.CreateElement($nodeName)
            $newNode.InnerText = $targetVersion
            $null = $targetPropertyGroup.AppendChild($newNode)
            $changed = $true
            continue
        }

        foreach ($node in $nodes) {
            if ($null -eq $node) { continue }
            if ($node.InnerText -ne $targetVersion) {
                $node.InnerText = $targetVersion
                $changed = $true
            }
        }
    }

    if ($changed) {
        $xml.Save($ProjectPath)
    }

    return $changed
}

function Get-ProjectPackageId {
    param([string]$ProjectPath)

    [xml]$xml = Get-Content -Path $ProjectPath
    $propertyGroups = @($xml.Project.PropertyGroup)
    foreach ($group in $propertyGroups) {
        if ($group.PSObject.Properties.Name.Contains("PackageId") -and -not [string]::IsNullOrWhiteSpace($group.PackageId)) {
            return $group.PackageId.Trim()
        }
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
}

function Is-PackableProject {
    param([string]$ProjectPath)

    [xml]$xml = Get-Content -Path $ProjectPath
    $propertyGroups = @($xml.Project.PropertyGroup)
    foreach ($group in $propertyGroups) {
        if ($group.PSObject.Properties.Name.Contains("IsPackable") -and
            $group.IsPackable.Trim().ToLowerInvariant() -eq "false") {
            return $false
        }
    }
    return $true
}

function Test-IsTemplateProject {
    param([string]$ProjectPath)

    [xml]$xml = Get-Content -Path $ProjectPath
    $propertyGroups = @($xml.Project.PropertyGroup)
    foreach ($group in $propertyGroups) {
        if ($group.PSObject.Properties.Name.Contains("PackageType") -and
            -not [string]::IsNullOrWhiteSpace($group.PackageType) -and
            $group.PackageType.Trim().Equals("Template", [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-TemplateRepositories {
    param(
        [string]$WorkspaceRoot,
        [string[]]$ExplicitTemplateRoots
    )

    $repositories = New-Object System.Collections.Generic.List[object]
    $candidateRoots = New-Object System.Collections.Generic.List[string]

    if ($ExplicitTemplateRoots -and $ExplicitTemplateRoots.Count -gt 0) {
        foreach ($root in $ExplicitTemplateRoots) {
            if ([string]::IsNullOrWhiteSpace($root)) { continue }
            if (-not (Test-Path $root)) {
                Write-Warning "Template root not found: $root"
                continue
            }
            $candidateRoots.Add((Resolve-Path $root).Path)
        }
    }
    else {
        $knownCandidates = @(
            (Join-Path $WorkspaceRoot "Muonroi.BaseTemplate"),
            (Join-Path $WorkspaceRoot "Muonroi.Modular.Template"),
            (Join-Path $WorkspaceRoot "Muonroi.Microservices.Template"),
            (Join-Path $WorkspaceRoot "Muonroi.Base.Template")
        )

        foreach ($candidate in $knownCandidates) {
            if (Test-Path $candidate) {
                $candidateRoots.Add((Resolve-Path $candidate).Path)
            }
        }

        $directChildren = Get-ChildItem -Path $WorkspaceRoot -Directory
        foreach ($child in $directChildren) {
            $templateJson = Join-Path $child.FullName ".template.config\template.json"
            if (Test-Path $templateJson) {
                $candidateRoots.Add($child.FullName)
            }
        }
    }

    $dedupedRoots = @($candidateRoots | Sort-Object -Unique)
    foreach ($root in $dedupedRoots) {
        $templateJsonPath = Join-Path $root ".template.config\template.json"
        if (-not (Test-Path $templateJsonPath)) { continue }

        $templateProject = Get-ChildItem -Path $root -Filter "*.csproj" -File |
            Where-Object { Test-IsTemplateProject -ProjectPath $_.FullName } |
            Select-Object -First 1

        if ($null -eq $templateProject) { continue }

        $shortName = $null
        try {
            $templateJson = Get-Content -Path $templateJsonPath -Raw | ConvertFrom-Json
            $shortName = [string]$templateJson.shortName
        }
        catch {
            Write-Warning "Unable to parse template.json in $root. ShortName will be empty."
        }

        $repositories.Add([pscustomobject]@{
                RootPath            = $root
                TemplateProjectPath = $templateProject.FullName
                TemplateProjectName = $templateProject.Name
                ShortName           = $shortName
            })
    }

    return @($repositories | Sort-Object RootPath -Unique)
}

function Update-TemplateReferences {
    param(
        [string]$TemplateRoot,
        [string]$NewVersion,
        [System.Collections.Generic.HashSet[string]]$AvailablePackageIds
    )

    $legacyPackageId = "Muonroi.BuildingBlock"
    $replacementPackageId = "Muonroi.BuildingBlock.All"

    $updatedFiles = 0
    $projects = Get-ChildItem -Path $TemplateRoot -Recurse -Filter "*.csproj" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }

    foreach ($proj in $projects) {
        [xml]$xml = Get-Content -Path $proj.FullName
        $changed = $false
        $itemGroups = @($xml.Project.ItemGroup)
        foreach ($itemGroup in $itemGroups) {
            if (-not $itemGroup.PSObject.Properties.Name.Contains("PackageReference")) {
                continue
            }

            $packageRefs = @($itemGroup.PackageReference)
            foreach ($packageRef in $packageRefs) {
                if ($null -eq $packageRef) { continue }
                $include = $packageRef.GetAttribute("Include")
                $includeAttributeName = "Include"

                if ([string]::IsNullOrWhiteSpace($include)) {
                    $include = $packageRef.GetAttribute("Update")
                    $includeAttributeName = "Update"
                }

                if ([string]::IsNullOrWhiteSpace($include) -or
                    -not $include.StartsWith("Muonroi.", [System.StringComparison]::OrdinalIgnoreCase)) {
                    continue
                }

                if ($include.Equals($legacyPackageId, [System.StringComparison]::OrdinalIgnoreCase) -and
                    $AvailablePackageIds.Contains($replacementPackageId)) {
                    $packageRef.SetAttribute($includeAttributeName, $replacementPackageId)
                    $include = $replacementPackageId
                    $changed = $true
                }

                if (-not $AvailablePackageIds.Contains($include)) {
                    continue
                }

                $currentVersion = $packageRef.GetAttribute("Version")
                if ($currentVersion -ne $NewVersion) {
                    $packageRef.SetAttribute("Version", $NewVersion)
                    $changed = $true
                }
            }
        }

        if ($changed) {
            $xml.Save($proj.FullName)
            $updatedFiles++
            Write-Host "  Updated PackageReferences: $($proj.FullName)"
        }
    }

    return $updatedFiles
}

function Uninstall-TemplateIfInstalled {
    param([string]$TemplatePath)

    $installed = dotnet new uninstall
    if ($installed -match [regex]::Escape($TemplatePath)) {
        Write-Host "  Removing installed template source: $TemplatePath"
        dotnet new uninstall $TemplatePath | Out-Null
    }
}

$buildingBlockRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$workspaceRoot = (Resolve-Path (Join-Path $buildingBlockRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($LocalFeedPath)) {
    $LocalFeedPath = Join-Path $workspaceRoot "LocalNuget"
}
if ([string]::IsNullOrWhiteSpace($LegacyFeedPath)) {
    $LegacyFeedPath = Join-Path $workspaceRoot "LocalNuGetFeed"
}

New-Item -ItemType Directory -Path $LocalFeedPath -Force | Out-Null
New-Item -ItemType Directory -Path $LegacyFeedPath -Force | Out-Null

Write-Host "--- Bump version to $Version ---" -ForegroundColor Cyan
Write-Host "Workspace root: $workspaceRoot"
Write-Host "Local feed: $LocalFeedPath"

$templateRepositories = Get-TemplateRepositories -WorkspaceRoot $workspaceRoot -ExplicitTemplateRoots $TemplateRoots
if ($templateRepositories.Count -eq 0) {
    Write-Warning "No template repositories found. Build/pack will continue for MuonroiBuildingBlock."
}
else {
    Write-Host "--- Detected template repositories ---" -ForegroundColor Cyan
    foreach ($templateRepo in $templateRepositories) {
        $shortName = if ([string]::IsNullOrWhiteSpace($templateRepo.ShortName)) { "<unknown>" } else { $templateRepo.ShortName }
        Write-Host "  [$shortName] $($templateRepo.RootPath)"
    }
}

$packedPackageIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

Push-Location $buildingBlockRoot
try {
    $libraryProjects = Get-ChildItem -Path "src" -Recurse -Filter "Muonroi.*.csproj" |
        Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" }

    if ($libraryProjects.Count -eq 0) {
        throw "No library projects were found under '$buildingBlockRoot\src'."
    }

    Write-Host "--- Update project versions ---" -ForegroundColor Cyan
    foreach ($proj in $libraryProjects) {
        $null = Set-ProjectVersion -ProjectPath $proj.FullName -NewVersion $Version
    }

    Write-Host "--- Build solution (Release) ---" -ForegroundColor Cyan
    dotnet build ".\Muonroi.BuildingBlock.sln" -c Release

    Write-Host "--- Pack all packable libraries ---" -ForegroundColor Cyan
    $packableProjects = @($libraryProjects | Where-Object { Is-PackableProject -ProjectPath $_.FullName })
    foreach ($proj in $packableProjects) {
        $packageId = Get-ProjectPackageId -ProjectPath $proj.FullName
        dotnet pack $proj.FullName -c Release -o $LocalFeedPath --no-build
        $null = $packedPackageIds.Add($packageId)
    }

    Write-Host "Packed $($packableProjects.Count) projects to $LocalFeedPath"
}
finally {
    Pop-Location
}

Copy-Item (Join-Path $LocalFeedPath "*.nupkg") $LegacyFeedPath -Force -ErrorAction SilentlyContinue

if (-not $SkipTemplateUpdate -and $templateRepositories.Count -gt 0) {
    Write-Host "--- Update template versions and PackageReferences ---" -ForegroundColor Cyan
    foreach ($templateRepo in $templateRepositories) {
        Write-Host "Template: $($templateRepo.RootPath)" -ForegroundColor Yellow
        $null = Set-ProjectVersion -ProjectPath $templateRepo.TemplateProjectPath -NewVersion $Version
        $updatedFiles = Update-TemplateReferences -TemplateRoot $templateRepo.RootPath -NewVersion $Version -AvailablePackageIds $packedPackageIds
        Write-Host "  Updated files: $updatedFiles"
    }
}

if (-not $SkipTemplateInstall -and $templateRepositories.Count -gt 0) {
    Write-Host "--- Re-install templates from source roots ---" -ForegroundColor Cyan
    foreach ($templateRepo in $templateRepositories) {
        Uninstall-TemplateIfInstalled -TemplatePath $templateRepo.RootPath
        dotnet new install $templateRepo.RootPath --force
    }
}

Write-Host "--- Done. Version $Version is ready ---" -ForegroundColor Green
