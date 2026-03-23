@echo off
setlocal EnableDelayedExpansion
title VoiceText - Install

echo ================================================
echo  VoiceText Install
echo ================================================
echo.

:: --- 1. Download Silero VAD model ---
set "ASSET_DIR=%~dp0src\VoiceText.App\Assets"
set "MODEL_FILE=%ASSET_DIR%\silero_vad.onnx"

if not exist "%ASSET_DIR%" mkdir "%ASSET_DIR%"

if exist "%MODEL_FILE%" (
    echo [OK] silero_vad.onnx already exists, skipping download.
) else (
    echo [1/3] Downloading Silero VAD model...
    curl -L "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx" -o "%MODEL_FILE%"
    if errorlevel 1 (
        echo [ERROR] Failed to download silero_vad.onnx
        echo        Please check your internet connection and try again.
        pause
        exit /b 1
    )
    echo [OK] silero_vad.onnx downloaded.
)

:: --- 2. Python .venv + dependencies ---
echo.
echo [2/3] Setting up Python virtual environment...
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found. Please install Python 3.10+ and add it to PATH.
    pause
    exit /b 1
)

set "VENV_DIR=%~dp0asr_server\.venv"

if not exist "%VENV_DIR%\Scripts\python.exe" (
    echo       Creating .venv in asr_server\...
    python -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo [ERROR] Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo [OK] .venv created.
) else (
    echo [OK] .venv already exists, skipping creation.
)

echo       Installing Python dependencies into .venv...
"%VENV_DIR%\Scripts\pip.exe" install -r "%~dp0asr_server\requirements.txt"
if errorlevel 1 (
    echo [ERROR] pip install failed.
    pause
    exit /b 1
)
echo [OK] Python dependencies installed.

:: --- 3. Restore .NET dependencies ---
echo.
echo [3/3] Restoring .NET dependencies...
cd /d "%~dp0src"
dotnet restore VoiceText.sln
if errorlevel 1 (
    echo [ERROR] dotnet restore failed.
    pause
    exit /b 1
)
echo [OK] .NET dependencies restored.

echo.
echo ================================================
echo  Installation complete!
echo  Run start.bat to launch VoiceText.
echo  Hotkey: Ctrl+Alt+F8 to toggle recording.
echo ================================================
pause
