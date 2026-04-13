@echo off
setlocal

if "%~1"=="" (
    echo ERROR: Version is required. Example: flow-free.cmd 1.9.4
    exit /b 1
)

powershell -ExecutionPolicy Bypass -File "%~dp0flow-free.ps1" -Version %1
exit /b %errorlevel%
