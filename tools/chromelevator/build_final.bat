@echo off
set "VC_VARS=C:\Program Files\Microsoft Visual Studio\18\Insiders\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VC_VARS%" (
    echo [!] ERROR: vcvars64.bat not found at %VC_VARS%
    exit /b 1
)
echo [*] Initializing MSVC Environment...
call "%VC_VARS%" >nul
if errorlevel 1 (
    echo [!] ERROR: Failed to initialize MSVC environment.
    exit /b 1
)
echo [*] Environment initialized. Starting build...
call .\make.bat
