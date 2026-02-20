@echo off
setlocal

REM Build a real installer (Inno Setup)
REM Prerequisite: install Inno Setup 6 (ISCC.exe)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" -Runtime win-x64 -Configuration Release

echo.
echo Done.
pause

