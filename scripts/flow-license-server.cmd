@echo off
setlocal

set ORG=%~1
if "%ORG%"=="" set ORG=Muonroi Local Test

powershell -ExecutionPolicy Bypass -File "%~dp0flow-license-server.ps1" -Organization "%ORG%"
exit /b %errorlevel%
