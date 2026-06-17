@echo off
setlocal
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Install-UniFlowHub.ps1" %*
endlocal
