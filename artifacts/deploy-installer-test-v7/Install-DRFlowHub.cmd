@echo off
setlocal
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Install-DRFlowHub.ps1" %*
endlocal
