@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Watch-Latest-AI.ps1" %*
if errorlevel 1 pause
