@echo off
setlocal enabledelayedexpansion
set ACTION=%1
set ROOT_DIR=%~dp0..
set API_KEY=%2

if "%ACTION%"=="build" (
    echo Building solution...
    dotnet build "%ROOT_DIR%\Muonroi.BuildingBlock.sln" -c Release || exit /b 1

    echo Packing all packable Muonroi libraries...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "$root = Resolve-Path '%ROOT_DIR%';" ^
      "$out = Join-Path $root 'nupkgs';" ^
      "New-Item -ItemType Directory -Path $out -Force | Out-Null;" ^
      "$projects = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter 'Muonroi.*.csproj' | Where-Object { $_.FullName -notmatch '\\\\(bin|obj)\\\\' };" ^
      "foreach ($p in $projects) {" ^
      "  [xml]$x = Get-Content $p.FullName;" ^
      "  $isPackable = $true;" ^
      "  foreach ($g in @($x.Project.PropertyGroup)) {" ^
      "    if ($g.IsPackable -and $g.IsPackable.Trim().ToLower() -eq 'false') { $isPackable = $false }" ^
      "  }" ^
      "  if ($isPackable) { dotnet pack $p.FullName -c Release -o $out --no-build }" ^
      "}" || exit /b 1
    :: Validate that all packable projects produced packages
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "$out = Join-Path (Resolve-Path '%ROOT_DIR%') 'nupkgs';" ^
      "$missing = @();" ^
      "$projects = Get-ChildItem (Join-Path (Resolve-Path '%ROOT_DIR%') 'src') -Recurse -Filter 'Muonroi.*.csproj' | Where-Object { $_.FullName -notmatch '\\(bin|obj\\)' };" ^
      "foreach ($p in $projects) {" ^
      "  [xml]$x = Get-Content $p.FullName;" ^
      "  $isPackable = $true;" ^
      "  foreach ($g in @($x.Project.PropertyGroup)) {" ^
      "    if ($g.IsPackable -and $g.IsPackable.Trim().ToLower() -eq 'false') { $isPackable = $false }" ^
      "  }" ^
      "  if ($isPackable) {" ^
      "    $name = $p.BaseName;" ^
      "    $nupkg = Get-ChildItem $out -Filter ($name + '*.nupkg');" ^
      "    if ($nupkg.Count -eq 0) { $missing += $name }" ^
      "  }" ^
      "}" ^
      "if ($missing.Count -gt 0) { Write-Host ('MISSING PACKAGED PROJECTS: ' + ($missing -join ', ')) -ForegroundColor Red; exit 1 }"

    echo Packing template packages from workspace...
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
      "$root = Resolve-Path '%ROOT_DIR%';" ^
      "$workspace = Split-Path $root -Parent;" ^
      "$out = Join-Path $root 'nupkgs';" ^
      "$candidateNames = @('Muonroi.BaseTemplate','Muonroi.Modular.Template','Muonroi.Microservices.Template','Muonroi.Base.Template');" ^
      "$templateRoots = New-Object System.Collections.Generic.List[string];" ^
      "foreach ($name in $candidateNames) {" ^
      "  $path = Join-Path $workspace $name;" ^
      "  if (Test-Path (Join-Path $path '.template.config\\template.json')) { $templateRoots.Add($path) }" ^
      "}" ^
      "foreach ($tpl in @($templateRoots | Sort-Object -Unique)) {" ^
      "  $csproj = Get-ChildItem $tpl -Filter '*.csproj' -File | Select-Object -First 1;" ^
      "  if ($null -ne $csproj) { dotnet pack $csproj.FullName -c Release -o $out }" ^
      "}" || exit /b 1
)
if "%ACTION%"=="push" (
    if "%API_KEY%"=="" (echo API Key required & exit /b 1)
    for %%f in ("%ROOT_DIR%\nupkgs\*.nupkg") do (
        dotnet nuget push "%%f" --api-key %API_KEY% --source https://api.nuget.org/v3/index.json --skip-duplicate
    )
)
