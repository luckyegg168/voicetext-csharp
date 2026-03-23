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

:: --- Check Python ---
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Please run install.bat first.
    pause
    exit /b 1
)

:: --- Launch C# app (it will spawn the Python ASR server automatically) ---
cd /d "%~dp0src"
dotnet run --project VoiceText.App
