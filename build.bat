@echo off
setlocal EnableDelayedExpansion
title VoiceText - Build

echo ================================================
echo  VoiceText Build Script
echo ================================================
echo.

:: --- Config ---
set "ROOT=%~dp0"
set "SRC=%ROOT%src"
set "ASR_DIR=%ROOT%asr_server"
set "DIST=%ROOT%dist"
set "PROJECT=%SRC%\VoiceText.App\VoiceText.App.csproj"
set "PUBLISH_OUT=%SRC%\VoiceText.App\bin\publish"

:: --- Clean previous dist ---
echo [1/5] Cleaning previous build output...
if exist "%DIST%" (
    rmdir /s /q "%DIST%"
    echo       Removed old dist\
)
if exist "%PUBLISH_OUT%" (
    rmdir /s /q "%PUBLISH_OUT%"
    echo       Removed old publish output.
)
echo [OK] Clean done.
echo.

:: --- Check dotnet ---
echo [2/5] Checking .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet not found. Please install .NET 8 SDK.
    pause
    exit /b 1
)
for /f "tokens=*" %%v in ('dotnet --version') do set "DOTNET_VER=%%v"
echo [OK] dotnet %DOTNET_VER% found.
echo.

:: --- Restore ---
echo [3/5] Restoring NuGet packages...
cd /d "%SRC%"
dotnet restore VoiceText.sln -m:1
if errorlevel 1 (
    echo [ERROR] dotnet restore failed.
    pause
    exit /b 1
)
echo [OK] Restore complete.
echo.

:: --- Publish ---
echo [4/5] Publishing VoiceText.App (win-x64, self-contained)...
dotnet publish "%PROJECT%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%PUBLISH_OUT%" ^
    -m:1 ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None ^
    -p:DebugSymbols=false
if errorlevel 1 (
    echo [ERROR] dotnet publish failed.
    pause
    exit /b 1
)
echo [OK] Publish complete.
echo.

:: --- Assemble dist ---
echo [5/5] Assembling dist\ folder...

:: App binary
mkdir "%DIST%"
xcopy /e /i /q "%PUBLISH_OUT%\*" "%DIST%\"
echo       Copied app binary to dist\

:: asr_server (exclude .venv to keep it lean; users run install.bat)
mkdir "%DIST%\asr_server"
xcopy /e /i /q "%ASR_DIR%\*" "%DIST%\asr_server\" /exclude:"%ROOT%build_xcopy_exclude.tmp"

:: Write xcopy exclude list (skip .venv and __pycache__)
echo .venv> "%ROOT%build_xcopy_exclude.tmp"
echo __pycache__>> "%ROOT%build_xcopy_exclude.tmp"
echo .pyc>> "%ROOT%build_xcopy_exclude.tmp"

:: Re-run copy with exclusions now that the file exists
rmdir /s /q "%DIST%\asr_server" >nul 2>&1
mkdir "%DIST%\asr_server"
xcopy /e /i /q "%ASR_DIR%\*" "%DIST%\asr_server\" /exclude:"%ROOT%build_xcopy_exclude.tmp"
del "%ROOT%build_xcopy_exclude.tmp" >nul 2>&1
echo       Copied asr_server\ (without .venv) to dist\

:: Helper scripts
copy /y "%ROOT%install.bat"   "%DIST%\install.bat"   >nul
copy /y "%ROOT%start.bat"     "%DIST%\start.bat"     >nul
copy /y "%ROOT%uninstall.bat" "%DIST%\uninstall.bat" >nul
echo       Copied install.bat / start.bat / uninstall.bat to dist\

:: Silero VAD model (if already downloaded)
set "MODEL_SRC=%SRC%\VoiceText.App\Assets\silero_vad.onnx"
if exist "%MODEL_SRC%" (
    if not exist "%DIST%\Assets" mkdir "%DIST%\Assets"
    copy /y "%MODEL_SRC%" "%DIST%\Assets\silero_vad.onnx" >nul
    echo       Copied silero_vad.onnx to dist\Assets\
) else (
    echo [WARN] silero_vad.onnx not found - users must run install.bat to download it.
)

echo.
echo ================================================
echo  Build SUCCESS
echo  Output : dist\
echo  Run    : cd dist ^&^& install.bat  (first time)
echo           cd dist ^&^& start.bat
echo ================================================
echo.
pause
