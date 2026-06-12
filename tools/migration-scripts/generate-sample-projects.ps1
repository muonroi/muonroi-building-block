param(
    [string]$RepoRoot = ".",
    [switch]$AddToSolution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
$srcRoot = Join-Path $root "src"
$samplesRoot = Join-Path $root "Samples"
$solutionPath = Join-Path $root "Muonroi.BuildingBlock.sln"

if (-not (Test-Path $samplesRoot)) {
    New-Item -ItemType Directory -Path $samplesRoot | Out-Null
}

$packages = Get-ChildItem -Path $srcRoot -Directory |
    Where-Object { $_.Name -like "Muonroi.*" -and $_.Name -ne "Muonroi.BuildingBlock" } |
    Sort-Object Name

$created = 0
$existing = 0

foreach ($package in $packages) {
    $projectPath = Join-Path $package.FullName "$($package.Name).csproj"
    if (-not (Test-Path $projectPath)) {
        continue
    }

    $sampleProjectName = "$($package.Name).Sample"
    $sampleProjectDir = Join-Path $samplesRoot $sampleProjectName
    $sampleProjectPath = Join-Path $sampleProjectDir "$sampleProjectName.csproj"

    if (-not (Test-Path $sampleProjectDir)) {
        New-Item -ItemType Directory -Path $sampleProjectDir | Out-Null
    }

    if (Test-Path $sampleProjectPath) {
        $existing++
        continue
    }

    $relativeProjectRef = "..\..\src\$($package.Name)\$($package.Name).csproj"
    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$relativeProjectRef" />
  </ItemGroup>
</Project>
"@
    Set-Content -Path $sampleProjectPath -Value $csproj -NoNewline

    $program = @"
using $($package.Name);

Console.WriteLine(""$sampleProjectName running..."");
Console.WriteLine(""Reference loaded: $($package.Name)"");
"@
    Set-Content -Path (Join-Path $sampleProjectDir "Program.cs") -Value $program -NoNewline
    $created++

    if ($AddToSolution -and (Test-Path $solutionPath)) {
        dotnet sln $solutionPath add $sampleProjectPath | Out-Null
    }
}

Write-Host "Packages scanned       : $($packages.Count)"
Write-Host "Sample projects created: $created"
Write-Host "Sample projects existed: $existing"
