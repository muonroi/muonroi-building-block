@echo off
setlocal
py -3 "%~dp0flow-runtime-roundtrip.py" %*
exit /b %ERRORLEVEL%
