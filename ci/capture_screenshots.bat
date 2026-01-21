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

if not exist "%PROJECT_DIR%\Screenshots" mkdir "%PROJECT_DIR%\Screenshots"
if not exist "%PROJECT_DIR%\Logs" mkdir "%PROJECT_DIR%\Logs"

"%UNITY_EXE%" -batchmode -quit ^
  -projectPath "%PROJECT_DIR%" ^
  -executeMethod BatchTools.UIBatchRunner.RunAll ^
  -logFile "%PROJECT_DIR%\Logs\ui_batch.log"

if errorlevel 1 exit /b 1

if not exist "%PROJECT_DIR%\Screenshots\ui_shot_1.png" (
  echo ui_shot_1.png not found after capture.
  exit /b 1
)

exit /b 0
