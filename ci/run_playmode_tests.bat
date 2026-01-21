@echo off
setlocal enabledelayedexpansion

set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"

if "%UNITY_EXE%"=="" (
  set "UNITY_EXE=D:\Tool\DevTools\Unity\Unity6\Editor\Unity.exe"
)

if not exist "%UNITY_EXE%" (
  echo UNITY_EXE not found: %UNITY_EXE%
  exit /b 1
)

if not exist "%PROJECT_DIR%\Logs" mkdir "%PROJECT_DIR%\Logs"

if exist "%PROJECT_DIR%\Logs\playmode-results.xml" del /f /q "%PROJECT_DIR%\Logs\playmode-results.xml"

"%UNITY_EXE%" -batchmode ^
  -projectPath "%PROJECT_DIR%" ^
  -runTests -testPlatform PlayMode ^
  -testResults "%PROJECT_DIR%\Logs\playmode-results.xml" ^
  -testResultsFormat nunit3 ^
  -logFile "%PROJECT_DIR%\Logs\playmode.log"

exit /b %ERRORLEVEL%
