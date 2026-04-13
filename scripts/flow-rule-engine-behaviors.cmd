@echo off
setlocal

if "%~1"=="" (
  echo Usage:
  echo   %~n0 ^<ProjectPath.csproj^> ^<ActivationProofPath.json^> [BaseUrl] [TenantId]
  exit /b 1
)

set "PROJECT_PATH=%~1"
set "PROOF_PATH=%~2"
set "BASE_URL=%~3"
set "TENANT_ID=%~4"

if "%PROOF_PATH%"=="" (
  echo ActivationProofPath is required.
  exit /b 1
)

if "%BASE_URL%"=="" set "BASE_URL=http://127.0.0.1:7310"
if "%TENANT_ID%"=="" set "TENANT_ID=tenant-a"

py -3 "%~dp0flow-rule-engine-behaviors.py" --project-path "%PROJECT_PATH%" --activation-proof-path "%PROOF_PATH%" --base-url "%BASE_URL%" --tenant-id "%TENANT_ID%"

endlocal
