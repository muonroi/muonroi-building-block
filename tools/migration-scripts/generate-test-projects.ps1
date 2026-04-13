param(
    [string]$RepoRoot = ".",
    [switch]$AddToSolution
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path $RepoRoot).Path
$srcRoot = Join-Path $root "src"
$testsRoot = Join-Path $root "tests"
$solutionPath = Join-Path $root "Muonroi.BuildingBlock.sln"

if (-not (Test-Path $testsRoot)) {
    New-Item -ItemType Directory -Path $testsRoot | Out-Null
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

    $testProjectName = "$($package.Name).Tests"
    $testProjectDir = Join-Path $testsRoot $testProjectName
    $testProjectPath = Join-Path $testProjectDir "$testProjectName.csproj"

    if (-not (Test-Path $testProjectDir)) {
        New-Item -ItemType Directory -Path $testProjectDir | Out-Null
    }

    if (Test-Path $testProjectPath) {
        $existing++
        continue
    }

    $relativeProjectRef = "..\..\src\$($package.Name)\$($package.Name).csproj"
    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.*" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$relativeProjectRef" />
  </ItemGroup>
</Project>
"@
    Set-Content -Path $testProjectPath -Value $csproj -NoNewline

    $testFile = @"
namespace $testProjectName;

public class SmokeTests
{
    [Fact]
    public void Project_ShouldBuild_TestAssembly()
    {
        true.Should().BeTrue();
    }
}
"@
    Set-Content -Path (Join-Path $testProjectDir "SmokeTests.cs") -Value $testFile -NoNewline
    $created++

    if ($AddToSolution -and (Test-Path $solutionPath)) {
        dotnet sln $solutionPath add $testProjectPath | Out-Null
    }
}

Write-Host "Packages scanned      : $($packages.Count)"
Write-Host "Test projects created : $created"
Write-Host "Test projects existed : $existing"
