@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Dev.ps1" %*
exit /b %ERRORLEVEL%
