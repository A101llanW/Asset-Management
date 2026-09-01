@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\dev\Reset-VisualStudioStartup.ps1" %*
exit /b %ERRORLEVEL%
