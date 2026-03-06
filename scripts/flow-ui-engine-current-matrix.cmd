@echo off
setlocal

if "%~1"=="" (
  echo Usage:
  echo   %~n0 ^<ProjectPath.csproj^> [ActivationProofPath.json]
  exit /b 1
)

set "PROJECT_PATH=%~1"
set "PROOF_PATH=%~2"

if "%PROOF_PATH%"=="" (
  powershell -ExecutionPolicy Bypass -File "%~dp0flow-ui-engine-current-matrix.ps1" -ProjectPath "%PROJECT_PATH%" -LicenseMode Free
) else (
  powershell -ExecutionPolicy Bypass -File "%~dp0flow-ui-engine-current-matrix.ps1" -ProjectPath "%PROJECT_PATH%" -ActivationProofPath "%PROOF_PATH%" -LicenseMode Enterprise
)

endlocal
