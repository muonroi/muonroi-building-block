param(
    [string]$SourceGeneratorProject = "D:\sources\Core\MuonroiBuildingBlock\src\Muonroi.RuleEngine.SourceGenerators\Muonroi.RuleEngine.SourceGenerators.csproj",
    [string]$LocalNugetPath = "D:\sources\Core\LocalNuget",
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param([string]$PathValue)
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return $null }
    return (Resolve-Path $PathValue).Path
}

function Invoke-DotnetCommand {
    param(
        [string[]]$Arguments,
        [string]$WorkDir
    )

    $stdoutPath = Join-Path $env:TEMP ("sourcegen_stdout_" + [Guid]::NewGuid().ToString("N") + ".log")
    $stderrPath = Join-Path $env:TEMP ("sourcegen_stderr_" + [Guid]::NewGuid().ToString("N") + ".log")

    try {
        $proc = Start-Process dotnet `
            -ArgumentList $Arguments `
            -WorkingDirectory $WorkDir `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru `
            -Wait

        $output = ""
        if (Test-Path $stdoutPath) {
            $output += Get-Content -Path $stdoutPath -Raw
        }
        if (Test-Path $stderrPath) {
            if (-not [string]::IsNullOrEmpty($output)) { $output += "`n" }
            $output += Get-Content -Path $stderrPath -Raw
        }

        return [ordered]@{
            ExitCode = [int]$proc.ExitCode
            Output = [string]$output
            Arguments = ($Arguments -join " ")
        }
    }
    finally {
        if (Test-Path $stdoutPath) { Remove-Item $stdoutPath -Force -ErrorAction SilentlyContinue }
        if (Test-Path $stderrPath) { Remove-Item $stderrPath -Force -ErrorAction SilentlyContinue }
    }
}

function Add-Case {
    param(
        [System.Collections.Generic.List[object]]$Cases,
        [string]$Id,
        [string]$Name,
        [bool]$Pass,
        [string]$Expected,
        [string]$Actual,
        [object]$Detail
    )

    $Cases.Add([ordered]@{
            Id = $Id
            Name = $Name
            Pass = $Pass
            Expected = $Expected
            Actual = $Actual
            Detail = $Detail
        })
}

$sourceGeneratorProjectAbs = Resolve-AbsolutePath -PathValue $SourceGeneratorProject
if (-not (Test-Path $sourceGeneratorProjectAbs)) {
    throw "SourceGeneratorProject not found: $SourceGeneratorProject"
}

$workspaceRoot = Split-Path -Parent $PSScriptRoot
$localNugetAbs = [System.IO.Path]::GetFullPath($LocalNugetPath)
if (-not (Test-Path $localNugetAbs)) {
    throw "LocalNugetPath not found: $localNugetAbs"
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$tempRoot = Join-Path $workspaceRoot "_tmp\source_generator_integration_$timestamp"
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspaceRoot "_tmp\source_generator_integration_$timestamp.json"
}
$outputPathAbs = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = Split-Path -Parent $outputPathAbs
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$cases = New-Object System.Collections.Generic.List[object]

$packResult = Invoke-DotnetCommand -Arguments @("pack", $sourceGeneratorProjectAbs, "-c", "Release", "-o", $localNugetAbs) -WorkDir (Split-Path -Parent $sourceGeneratorProjectAbs)
$packPass = $packResult.ExitCode -eq 0
Add-Case -Cases $cases -Id "PREP_PACK" -Name "Pack SourceGenerator" -Pass $packPass -Expected "dotnet pack exit code 0" -Actual "ExitCode=$($packResult.ExitCode)" -Detail $packResult
if (-not $packPass) {
    throw "dotnet pack failed for source generator."
}

$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$localNugetAbs" />
    <add key="nuget" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
Set-Content -Path (Join-Path $tempRoot "NuGet.config") -Value $nugetConfig -Encoding UTF8

$probeDir = Join-Path $tempRoot "SourceGeneratorProbe"
$newProj = Invoke-DotnetCommand -Arguments @("new", "classlib", "-n", "SourceGeneratorProbe", "-f", "net9.0", "-o", $probeDir) -WorkDir $tempRoot
if ($newProj.ExitCode -ne 0) {
    throw "Cannot create source generator probe project."
}

$probeCsproj = Join-Path $probeDir "SourceGeneratorProbe.csproj"
$probeCsprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Muonroi.RuleEngine.Abstractions" Version="0.1.1" />
    <PackageReference Include="Muonroi.RuleEngine.SourceGenerators" Version="0.1.1">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
"@
Set-Content -Path $probeCsproj -Value $probeCsprojContent -Encoding UTF8

$samplePath = Join-Path $probeDir "SampleHandler.cs"
$sampleContent = @"
using Muonroi.RuleEngine.Abstractions;

public sealed class SgContext
{
    public int Amount { get; set; }
}

public partial class SampleHandler
{
    [MExtractAsRule("TEST_RULE", Order = 1, HookPoint = HookPoint.BeforeRule)]
    public Task<RuleResult> HandleA(SgContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["ok"] = ctx.Amount > 0;
        return Task.FromResult(RuleResult.Passed());
    }
}
"@
Set-Content -Path $samplePath -Value $sampleContent -Encoding UTF8

$buildClean = Invoke-DotnetCommand -Arguments @("build", $probeCsproj, "-c", "Debug") -WorkDir $tempRoot
$g21Pass = $buildClean.ExitCode -eq 0
Add-Case -Cases $cases -Id "G21" -Name "Source Generator compile" -Pass $g21Pass -Expected "Probe project builds with 0 errors" -Actual "ExitCode=$($buildClean.ExitCode)" -Detail $buildClean

$generatedRoot = Join-Path $probeDir "obj\Generated"
$generatedFiles = @()
if (Test-Path $generatedRoot) {
    $generatedFiles = Get-ChildItem -Path $generatedRoot -Recurse -Filter *.g.cs
}

$containsGeneratedRule = $false
$containsRegistration = $false
foreach ($file in $generatedFiles) {
    $text = Get-Content -Path $file.FullName -Raw
    if ($text.Contains("IRule<") -and $text.Contains("TEST_RULE")) {
        $containsGeneratedRule = $true
    }

    if ($text.Contains("AddGeneratedRules(")) {
        $containsRegistration = $true
    }
}

$g23Pass = $g21Pass -and $containsGeneratedRule
Add-Case -Cases $cases -Id "G23" -Name "Generated rule IntelliSense artifact" -Pass $g23Pass -Expected "Generated .g.cs contains IRule< and TEST_RULE" -Actual "GeneratedFiles=$($generatedFiles.Count); ContainsGeneratedRule=$containsGeneratedRule" -Detail ($generatedFiles | Select-Object -ExpandProperty FullName)

$g24Pass = $g21Pass -and $containsRegistration
Add-Case -Cases $cases -Id "G24" -Name "RuleRegistrationGenerator AddGeneratedRules" -Pass $g24Pass -Expected "Generated code contains AddGeneratedRules extension method" -Actual "ContainsRegistration=$containsRegistration" -Detail ($generatedFiles | Select-Object -ExpandProperty FullName)

$duplicateAppend = @"

public partial class SampleHandler
{
    [MExtractAsRule("TEST_RULE", Order = 2, HookPoint = HookPoint.BeforeRule)]
    public Task<RuleResult> HandleB(SgContext ctx, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }
}
"@
Add-Content -Path $samplePath -Value $duplicateAppend

$buildDup = Invoke-DotnetCommand -Arguments @("build", $probeCsproj, "-c", "Debug") -WorkDir $tempRoot
$g22Pass = $buildDup.Output.Contains("MRG001")
Add-Case -Cases $cases -Id "G22" -Name "MRG001 diagnostic duplicate code" -Pass $g22Pass -Expected "Build output contains MRG001 for duplicate rule code" -Actual "ExitCode=$($buildDup.ExitCode); ContainsMRG001=$($buildDup.Output.Contains('MRG001'))" -Detail $buildDup

$overallPass = ($cases | Where-Object { -not $_.Pass }).Count -eq 0
$evidence = [ordered]@{
    OverallStatus = if ($overallPass) { "PASS" } else { "FAIL" }
    SourceGeneratorProject = $sourceGeneratorProjectAbs
    LocalNugetPath = $localNugetAbs
    TempRoot = $tempRoot
    ProbeProject = $probeCsproj
    Cases = $cases
    CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
}
$evidence | ConvertTo-Json -Depth 60 | Set-Content -Path $outputPathAbs -Encoding UTF8

if ($overallPass) {
    Write-Host "Source generator integration flow: PASS" -ForegroundColor Green
}
else {
    Write-Host "Source generator integration flow: FAIL" -ForegroundColor Red
}

Write-Host "Evidence: $outputPathAbs"
if (-not $overallPass) {
    exit 1
}
