@echo off
setlocal
py -3 "%~dp0check-runtime-parity.py" %*
exit /b %ERRORLEVEL%
