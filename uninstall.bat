@echo off
setlocal EnableDelayedExpansion
title VoiceText - Uninstall

echo ================================================
echo  VoiceText - Uninstall
echo ================================================
echo.
echo This will remove:
echo   - Build output (bin / obj folders)
echo   - Downloaded model (silero_vad.onnx)
echo   - Python virtual environment (asr_server\.venv)
echo.
set /p "CONFIRM=Continue? (y/N): "
if /i not "%CONFIRM%"=="y" (
    echo Cancelled.
    pause
    exit /b 0
)

:: --- Remove build output ---
echo.
echo [1/3] Removing build output...
for /d /r "%~dp0src" %%D in (bin obj) do (
    if exist "%%D" (
        rd /s /q "%%D"
        echo       Removed: %%D
    )
)
echo [OK] Build output removed.

:: --- Remove downloaded model ---
echo.
echo [2/3] Removing downloaded model...
set "MODEL_FILE=%~dp0src\VoiceText.App\Assets\silero_vad.onnx"
if exist "%MODEL_FILE%" (
    del /f /q "%MODEL_FILE%"
    echo [OK] silero_vad.onnx removed.
) else (
    echo [OK] silero_vad.onnx not found, skipping.
)

:: --- Remove Python .venv ---
echo.
echo [3/3] Removing Python virtual environment...
set "VENV_DIR=%~dp0asr_server\.venv"
if exist "%VENV_DIR%" (
    rd /s /q "%VENV_DIR%"
    echo [OK] asr_server\.venv removed.
) else (
    echo [OK] No .venv found, skipping.
)

echo.
echo ================================================
echo  Uninstall complete.
echo ================================================
pause
