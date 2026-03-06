@echo off
setlocal

if "%~1"=="" (
  echo Usage:
  echo   %~n0 ^<ProjectPath.csproj^> ^<ActivationProofPath.json^> [BaseUrl]
  exit /b 1
)

set "PROJECT_PATH=%~1"
set "PROOF_PATH=%~2"
set "BASE_URL=%~3"

if "%PROOF_PATH%"=="" (
  echo ActivationProofPath is required.
  exit /b 1
)

if "%BASE_URL%"=="" (
  powershell -ExecutionPolicy Bypass -File "%~dp0flow-enterprise-multitenant-ruleengine-ui.ps1" -ProjectPath "%PROJECT_PATH%" -ActivationProofPath "%PROOF_PATH%"
) else (
  powershell -ExecutionPolicy Bypass -File "%~dp0flow-enterprise-multitenant-ruleengine-ui.ps1" -ProjectPath "%PROJECT_PATH%" -ActivationProofPath "%PROOF_PATH%" -BaseUrl "%BASE_URL%"
)

endlocal
