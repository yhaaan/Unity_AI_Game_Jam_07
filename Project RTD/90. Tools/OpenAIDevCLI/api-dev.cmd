@echo off
setlocal
powershell.exe -NoLogo -ExecutionPolicy Bypass -File "%~dp0start.ps1" %*
endlocal
