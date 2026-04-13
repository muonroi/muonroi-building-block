param(
    [string]$RuleGenProject = "D:\sources\Core\MuonroiBuildingBlock\tools\Muonroi.RuleGen\Muonroi.RuleGen.csproj",
    [string]$WorkingDir,
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

    $stdoutPath = Join-Path $env:TEMP ("rulegen_stdout_" + [Guid]::NewGuid().ToString("N") + ".log")
    $stderrPath = Join-Path $env:TEMP ("rulegen_stderr_" + [Guid]::NewGuid().ToString("N") + ".log")

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

function Wait-ForFile {
    param(
        [string]$Path,
        [int]$TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $Path) { return $true }
        Start-Sleep -Milliseconds 400
    }

    return $false
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

$ruleGenProjectAbs = Resolve-AbsolutePath -PathValue $RuleGenProject
if (-not (Test-Path $ruleGenProjectAbs)) {
    throw "RuleGenProject not found: $RuleGenProject"
}

$workspaceRoot = Split-Path -Parent (Split-Path -Parent $ruleGenProjectAbs)
if ([string]::IsNullOrWhiteSpace($WorkingDir)) {
    $WorkingDir = Split-Path -Parent $ruleGenProjectAbs
}

$workingDirAbs = [System.IO.Path]::GetFullPath($WorkingDir)
$localNugetAbs = [System.IO.Path]::GetFullPath($LocalNugetPath)
if (-not (Test-Path $localNugetAbs)) {
    throw "LocalNugetPath not found: $localNugetAbs"
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$tempRoot = Join-Path $workspaceRoot "_tmp\rulegen_cli_v2_$timestamp"
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

$cliName = [System.IO.Path]::GetFileNameWithoutExtension($ruleGenProjectAbs)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $workspaceRoot "_tmp\${cliName}_v2_behaviors_$timestamp.json"
}

$outputPathAbs = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = Split-Path -Parent $outputPathAbs
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$cases = New-Object System.Collections.Generic.List[object]
$watchProc = $null

try {
    $buildRuleGen = Invoke-DotnetCommand -Arguments @("build", $ruleGenProjectAbs, "-c", "Release") -WorkDir $workingDirAbs
    $buildPass = $buildRuleGen.ExitCode -eq 0
    Add-Case -Cases $cases -Id "PREP" -Name "Build RuleGen CLI" -Pass $buildPass -Expected "dotnet build exit code 0" -Actual "ExitCode=$($buildRuleGen.ExitCode)" -Detail $buildRuleGen
    if (-not $buildPass) {
        throw "RuleGen build failed."
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

    $probeDir = Join-Path $tempRoot "RuleGenCliProbe"
    $newProj = Invoke-DotnetCommand -Arguments @("new", "classlib", "-n", "RuleGenCliProbe", "-f", "net9.0", "-o", $probeDir) -WorkDir $tempRoot
    if ($newProj.ExitCode -ne 0) {
        throw "Cannot create probe project."
    }

    $probeCsproj = Join-Path $probeDir "RuleGenCliProbe.csproj"
    $probeCsprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Muonroi.RuleEngine.Abstractions" Version="0.1.1" />
  </ItemGroup>
</Project>
"@
    Set-Content -Path $probeCsproj -Value $probeCsprojContent -Encoding UTF8

    $sampleHandlerPath = Join-Path $probeDir "SampleHandler.cs"
    $sampleHandlerContent = @"
using Muonroi.RuleEngine.Abstractions;

public static class AppConsts
{
    public const string RULE_CODE = "RULE_CONST";
    public const string NAMEOF_CODE = "RULE_NAMEOF";
}

public sealed class SampleContext
{
    public int Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public partial class SampleHandler
{
    [MExtractAsRule(nameof(AppConsts.NAMEOF_CODE), Order = 1, HookPoint = HookPoint.BeforeRule)]
    public Task<RuleResult> RuleByNameof(SampleContext ctx, FactBag facts, CancellationToken ct)
    {
        if (!ValidateHelper(ctx))
        {
            return Task.FromResult(RuleResult.Failure("invalid"));
        }

        facts["result.nameof"] = true;
        return Task.FromResult(RuleResult.Passed());
    }

    [MExtractAsRule(AppConsts.RULE_CODE, Order = 2, HookPoint = HookPoint.BeforeRule)]
    public Task<RuleResult> RuleByConst(SampleContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["result.const"] = ctx.Amount > 0;
        return Task.FromResult(RuleResult.Passed());
    }

    private static bool ValidateHelper(SampleContext ctx)
    {
        return ctx.Amount > 0;
    }
}
"@
    Set-Content -Path $sampleHandlerPath -Value $sampleHandlerContent -Encoding UTF8

    $duplicatePath = Join-Path $tempRoot "DuplicateHandler.cs"
    $duplicateContent = @"
using Muonroi.RuleEngine.Abstractions;

public sealed class DuplicateContext
{
    public int Value { get; set; }
}

public partial class DuplicateHandler
{
    [MExtractAsRule("SAME_CODE", Order = 1)]
    public Task<RuleResult> A(DuplicateContext ctx, FactBag facts, CancellationToken ct)
        => Task.FromResult(RuleResult.Passed());

    [MExtractAsRule("SAME_CODE", Order = 2)]
    public Task<RuleResult> B(DuplicateContext ctx, FactBag facts, CancellationToken ct)
        => Task.FromResult(RuleResult.Passed());
}
"@
    Set-Content -Path $duplicatePath -Value $duplicateContent -Encoding UTF8

    $mergeTargetPath = Join-Path $probeDir "MergeTarget.cs"
    $mergeTargetContent = @"
using Muonroi.RuleEngine.Abstractions;

public partial class MergeTarget
{
    [MExtractAsRule("BASE_RULE", Order = 1)]
    public Task<RuleResult> BaseRule(SampleContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["base"] = true;
        return Task.FromResult(RuleResult.Passed());
    }
}
"@
    Set-Content -Path $mergeTargetPath -Value $mergeTargetContent -Encoding UTF8

    $extractOutDir = Join-Path $tempRoot "generated"
    $extractArgs = @(
        "run", "--project", $ruleGenProjectAbs, "--",
        "extract",
        "--source", $sampleHandlerPath,
        "--output", $extractOutDir,
        "--namespace", "Probe.Generated",
        "--context", "SampleContext"
    )
    $extractResult = Invoke-DotnetCommand -Arguments $extractArgs -WorkDir $tempRoot
    $generatedFile = Get-ChildItem -Path $extractOutDir -Recurse -Filter *.g.cs -ErrorAction SilentlyContinue | Select-Object -First 1
    $generatedContent = if ($null -ne $generatedFile) { Get-Content -Path $generatedFile.FullName -Raw } else { "" }

    $g13Pass = $extractResult.ExitCode -eq 0 -and ($generatedContent -notmatch "nameof\(") -and ($generatedContent -notmatch "AppConsts\.")
    Add-Case -Cases $cases -Id "G13" -Name "extract + SemanticModel nameof/const" -Pass $g13Pass -Expected "Generated file does not contain nameof(...) or AppConsts." -Actual "ExitCode=$($extractResult.ExitCode); File=$($generatedFile.FullName)" -Detail $extractResult

    $containsHelper = $generatedContent -match "ValidateHelper\s*\("
    $g14Pass = $extractResult.ExitCode -eq 0 -and $containsHelper
    Add-Case -Cases $cases -Id "G14" -Name "extract + helper method" -Pass $g14Pass -Expected "Generated rule includes ValidateHelper" -Actual "ContainsHelper=$containsHelper" -Detail $generatedFile.FullName

    $dupOutDir = Join-Path $tempRoot "dup_generated"
    $dupArgs = @(
        "run", "--project", $ruleGenProjectAbs, "--",
        "extract",
        "--source", $duplicatePath,
        "--output", $dupOutDir,
        "--namespace", "Probe.Generated",
        "--context", "DuplicateContext"
    )
    $dupResult = Invoke-DotnetCommand -Arguments $dupArgs -WorkDir $tempRoot
    $g15Pass = $dupResult.ExitCode -ne 0 -and $dupResult.Output.ToLowerInvariant().Contains("duplicate")
    Add-Case -Cases $cases -Id "G15" -Name "verify duplicate detection" -Pass $g15Pass -Expected "Duplicate code produces non-zero exit and duplicate message" -Actual "ExitCode=$($dupResult.ExitCode)" -Detail $dupResult

    $runtimeInvalidPath = Join-Path $tempRoot "runtime-invalid.json"
    $runtimeInvalidJson = @"
{
  "workflowName": "merge-test",
  "version": 1,
  "rules": [
    {
      "code": "BROKEN_RULE",
      "name": "BROKEN_RULE",
      "order": 1,
      "hookPoint": "BeforeRule",
      "dependsOn": [],
      "condition": "order.amount >",
      "action": "facts['broken'] = true",
      "type": "Validation"
    }
  ]
}
"@
    Set-Content -Path $runtimeInvalidPath -Value $runtimeInvalidJson -Encoding UTF8

    $targetHashBefore = (Get-FileHash -Path $mergeTargetPath -Algorithm SHA256).Hash
    $mergeArgs = @(
        "run", "--project", $ruleGenProjectAbs, "--",
        "merge",
        "--rules-json", $runtimeInvalidPath,
        "--target", $mergeTargetPath,
        "--class", "MergeTarget",
        "--context", "SampleContext",
        "--compile-check", "true",
        "--compile-target", $probeCsproj
    )
    $mergeResult = Invoke-DotnetCommand -Arguments $mergeArgs -WorkDir $tempRoot
    $targetHashAfter = (Get-FileHash -Path $mergeTargetPath -Algorithm SHA256).Hash
    $mergeGeneratedPath = Join-Path $probeDir "MergeTarget.Generated.cs"
    $g16Pass = $mergeResult.ExitCode -ne 0 -and ($targetHashBefore -eq $targetHashAfter) -and (-not (Test-Path $mergeGeneratedPath))
    Add-Case -Cases $cases -Id "G16" -Name "merge compile-check rollback" -Pass $g16Pass -Expected "Merge fails; target unchanged; no generated file" -Actual "ExitCode=$($mergeResult.ExitCode); TargetUnchanged=$($targetHashBefore -eq $targetHashAfter); GeneratedExists=$(Test-Path $mergeGeneratedPath)" -Detail $mergeResult

    $splitOutDir = Join-Path $tempRoot "split_generated"
    $runtimeExportPath = Join-Path $tempRoot "runtime-export.json"
    $splitArgs = @(
        "run", "--project", $ruleGenProjectAbs, "--",
        "split",
        "--source", $sampleHandlerPath,
        "--output-dir", $splitOutDir,
        "--export-json", $runtimeExportPath,
        "--namespace", "Probe.Generated",
        "--context", "SampleContext"
    )
    $splitResult = Invoke-DotnetCommand -Arguments $splitArgs -WorkDir $tempRoot
    $runtimeText = if (Test-Path $runtimeExportPath) { Get-Content -Path $runtimeExportPath -Raw } else { "" }
    $g17Pass = $splitResult.ExitCode -eq 0 -and (Test-Path $runtimeExportPath) -and $runtimeText.Contains('"rules"') -and $runtimeText.Contains('"condition"')
    Add-Case -Cases $cases -Id "G17" -Name "split -> runtime JSON" -Pass $g17Pass -Expected "runtime.json exists with rules[] and condition fields" -Actual "ExitCode=$($splitResult.ExitCode); RuntimeExists=$(Test-Path $runtimeExportPath)" -Detail $splitResult

    $watchOutDir = Join-Path $tempRoot "watch_generated"
    $watchStdout = Join-Path $tempRoot "watch.out.log"
    $watchStderr = Join-Path $tempRoot "watch.err.log"
    $watchArgs = @(
        "run", "--project", $ruleGenProjectAbs, "--",
        "watch",
        "--source", $probeDir,
        "--output", $watchOutDir,
        "--namespace", "Probe.Generated",
        "--context", "SampleContext"
    )

    $watchProc = Start-Process dotnet -ArgumentList $watchArgs -WorkingDirectory $tempRoot -RedirectStandardOutput $watchStdout -RedirectStandardError $watchStderr -PassThru
    Start-Sleep -Seconds 3
    $watchGeneratedFile = Join-Path $watchOutDir "global\RULE_CONST.g.cs"
    $watchFileReady = Wait-ForFile -Path $watchGeneratedFile -TimeoutSeconds 30
    $oldWrite = if ($watchFileReady) { (Get-Item $watchGeneratedFile).LastWriteTimeUtc } else { [DateTime]::MinValue }

    Add-Content -Path $sampleHandlerPath -Value "`n// watch-touch-$timestamp"

    $changed = $false
    if ($watchFileReady) {
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline) {
            if ($watchProc.HasExited) { break }
            $newWrite = (Get-Item $watchGeneratedFile).LastWriteTimeUtc
            if ($newWrite -gt $oldWrite) {
                $changed = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }
    }

    if ($null -ne $watchProc -and -not $watchProc.HasExited) {
        Stop-Process -Id $watchProc.Id -Force
    }

    $g18Pass = $watchFileReady -and $changed
    Add-Case -Cases $cases -Id "G18" -Name "watch detects changes" -Pass $g18Pass -Expected "watch updates generated .g.cs after file touch" -Actual "FileReady=$watchFileReady; Changed=$changed" -Detail @{ WatchOut = $watchStdout; WatchErr = $watchStderr }

    $testsOutDir = Join-Path $tempRoot "generated_tests"
    $genTestsArgs = @(
        "run", "--project", $ruleGenProjectAbs, "--",
        "generate-tests",
        "--rules", $extractOutDir,
        "--output", $testsOutDir,
        "--namespace", "Probe.Generated"
    )
    $genTestsResult = Invoke-DotnetCommand -Arguments $genTestsArgs -WorkDir $tempRoot

    $compileDir = Join-Path $tempRoot "generated_compile"
    New-Item -ItemType Directory -Path $compileDir -Force | Out-Null
    $compileCsproj = Join-Path $compileDir "GeneratedCompile.csproj"
    $compileCsprojContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Muonroi.RuleEngine.Abstractions" Version="0.1.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="..\RuleGenCliProbe\SampleHandler.cs" Link="SampleHandler.cs" />
    <Compile Include="..\generated\**\*.g.cs" />
    <Compile Include="..\generated_tests\*.cs" />
  </ItemGroup>
</Project>
"@
    Set-Content -Path $compileCsproj -Value $compileCsprojContent -Encoding UTF8

    $compileResult = Invoke-DotnetCommand -Arguments @("build", $compileCsproj, "-c", "Debug") -WorkDir $tempRoot
    $g19Pass = $genTestsResult.ExitCode -eq 0 -and $compileResult.ExitCode -eq 0
    Add-Case -Cases $cases -Id "G19" -Name "generate-tests compilation" -Pass $g19Pass -Expected "generate-tests succeeds and compiled generated tests with 0 errors" -Actual "GenerateExit=$($genTestsResult.ExitCode); BuildExit=$($compileResult.ExitCode)" -Detail @{ Generate = $genTestsResult; Build = $compileResult }

    $foundSummary = $extractResult.Output.Contains("Rule Extraction Summary")
    $foundTotal = $extractResult.Output.Contains("Total:")
    $g20Pass = $extractResult.ExitCode -eq 0 -and ($foundSummary -or $foundTotal)
    Add-Case -Cases $cases -Id "G20" -Name "Spectre.Console output" -Pass $g20Pass -Expected "extract output contains Spectre summary/progress text" -Actual "FoundSummary=$foundSummary; FoundTotal=$foundTotal" -Detail $extractResult.Output

    $overallPass = ($cases | Where-Object { -not $_.Pass }).Count -eq 0
    $evidence = [ordered]@{
        OverallStatus = if ($overallPass) { "PASS" } else { "FAIL" }
        RuleGenProject = $ruleGenProjectAbs
        TempRoot = $tempRoot
        OutputPath = $outputPathAbs
        Cases = $cases
        CompletedAtUtc = [DateTime]::UtcNow.ToString("o")
    }
    $evidence | ConvertTo-Json -Depth 60 | Set-Content -Path $outputPathAbs -Encoding UTF8

    if ($overallPass) {
        Write-Host "RuleGen CLI v2 supplement flow: PASS" -ForegroundColor Green
    }
    else {
        Write-Host "RuleGen CLI v2 supplement flow: FAIL" -ForegroundColor Red
    }
    Write-Host "Evidence: $outputPathAbs"

    if (-not $overallPass) {
        exit 1
    }
}
finally {
    if ($null -ne $watchProc -and -not $watchProc.HasExited) {
        try { Stop-Process -Id $watchProc.Id -Force } catch {}
    }
}
