@echo off
setlocal enabledelayedexpansion

set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"

set "REPORT_PATH=%PROJECT_DIR%\report.md"

call "%~dp0run_playmode_tests.bat"
call "%~dp0capture_screenshots.bat"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0one_click_check.ps1" ^
  -ProjectDir "%PROJECT_DIR%" ^
  -ReportPath "%REPORT_PATH%"

exit /b 0
