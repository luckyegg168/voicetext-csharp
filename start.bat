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

:: --- Build first to surface errors clearly ---
echo [INFO] Building VoiceText.App...
cd /d "%~dp0src"
dotnet build VoiceText.App --no-restore -v minimal
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. Check the output above.
    pause
    exit /b 1
)

:: --- Launch C# app (it will spawn the Python ASR server via .venv automatically) ---
echo [INFO] Launching VoiceText.App...
dotnet run --project VoiceText.App --no-build
echo.
echo [INFO] App exited with code %errorlevel%.
pause
