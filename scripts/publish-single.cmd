@echo off
setlocal

REM Publish a self-contained single EXE (win-x64) to the publish folder.
REM Output path:
REM   src\GymManager.App\bin\Release\net8.0-windows\win-x64\publish\GymManager.App.exe

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-single.ps1" -Runtime win-x64 -Configuration Release

echo.
echo Done.
pause

