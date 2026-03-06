@echo off
setlocal

if "%~1"=="" (
  echo Usage:
  echo   %~n0 ^<ProjectPath.csproj^> ^<ActivationProofPath.json^> [BaseUrl] [TenantA] [TenantB]
  exit /b 1
)

set "PROJECT_PATH=%~1"
set "PROOF_PATH=%~2"
set "BASE_URL=%~3"
set "TENANT_A=%~4"
set "TENANT_B=%~5"

if "%PROOF_PATH%"=="" (
  echo ActivationProofPath is required.
  exit /b 1
)

if "%BASE_URL%"=="" set "BASE_URL=http://127.0.0.1:7310"
if "%TENANT_A%"=="" set "TENANT_A=tenant-a"
if "%TENANT_B%"=="" set "TENANT_B=tenant-b"

py -3 "%~dp0flow-multitenant-rule-isolation.py" --project-path "%PROJECT_PATH%" --activation-proof-path "%PROOF_PATH%" --base-url "%BASE_URL%" --tenant-a "%TENANT_A%" --tenant-b "%TENANT_B%"

endlocal
