@echo off
setlocal
set ROOT=%~dp0
set TEMPLATE_OUT=%ROOT%obj\Release\Temp

set MSBUILD_EXE=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe
if not exist "%MSBUILD_EXE%" set MSBUILD_EXE=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
if not exist "%MSBUILD_EXE%" set MSBUILD_EXE=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe
if not exist "%MSBUILD_EXE%" (
  echo MSBuild.exe for Visual Studio 2022 not found.
  exit /b 1
)

"%MSBUILD_EXE%" "%ROOT%Muonroi.RuleGen.VisualStudio.csproj" /t:Restore,Build,GeneratePkgDef /p:Configuration=Release /v:minimal
if errorlevel 1 exit /b %errorlevel%

"%MSBUILD_EXE%" "%ROOT%Muonroi.RuleGen.VisualStudio.csproj" /t:CreateVsixContainer /p:Configuration=Release /p:TemplateOutputDirectory="%TEMPLATE_OUT%" /v:minimal
if errorlevel 1 exit /b %errorlevel%

echo VSIX built at: %ROOT%bin\Release\Muonroi.RuleGen.VisualStudio.vsix
