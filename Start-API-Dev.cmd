@echo off
setlocal
title OpenAI API Dev CLI
powershell.exe -NoLogo -NoExit -ExecutionPolicy Bypass -File "%~dp0Project RTD\90. Tools\OpenAIDevCLI\start.ps1"
endlocal
