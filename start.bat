@echo off
setlocal EnableDelayedExpansion
title VoiceText

echo ================================================
echo  VoiceText - Starting
echo  Hotkey: Alt+Shift+V to toggle recording
echo ================================================
echo.

:: --- Check model exists ---
set "MODEL_FILE=%~dp0src\VoiceText.App\Assets\silero_vad.onnx"
if not exist "%MODEL_FILE%" (
    echo [ERROR] silero_vad.onnx not found.
    echo        Please run install.bat first.
    pause
    exit /b 1
)

:: --- Check .venv exists ---
set "VENV_PYTHON=%~dp0asr_server\.venv\Scripts\python.exe"
if not exist "%VENV_PYTHON%" (
    echo [ERROR] Python virtual environment not found.
    echo        Please run install.bat first.
    pause
    exit /b 1
)

:: --- Kill existing instance if running ---
tasklist /fi "imagename eq VoiceText.App.exe" 2>nul | find /i "VoiceText.App.exe" >nul
if not errorlevel 1 (
    echo [INFO] Stopping existing VoiceText.App instance...
    taskkill /f /im VoiceText.App.exe >nul 2>&1
    timeout /t 1 /nobreak >nul
)

:: --- Launch C# app (spawns Python ASR server via .venv automatically) ---
echo [INFO] Launching VoiceText.App...
cd /d "%~dp0src"
dotnet run --project VoiceText.App
echo.
echo [INFO] App exited with code %errorlevel%.
pause
