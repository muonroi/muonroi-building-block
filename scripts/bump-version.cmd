@echo off
setlocal

if "%~1"=="" (
    echo ERROR: Version is required. Example: bump-version.cmd 1.9.5
    exit /b 1
)

powershell -ExecutionPolicy Bypass -File "%~dp0bump-version.ps1" -Version %1
exit /b %errorlevel%
