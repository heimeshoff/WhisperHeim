@echo off
rem Thin shim so `deploy` from the repo root runs scripts\publish.ps1.
rem Forwards all args (e.g. `deploy -NoLaunch`).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\publish.ps1" %*
exit /b %ERRORLEVEL%
