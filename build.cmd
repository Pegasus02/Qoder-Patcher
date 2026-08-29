@echo off
setlocal
cd /d "%~dp0"
echo ===================================================
echo   Building Qoder CN Gateway Manager v3.0.1
echo ===================================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
set "BUILD_EXIT=%ERRORLEVEL%"
if not "%BUILD_EXIT%"=="0" (
    echo [ERROR] Build failed with exit code %BUILD_EXIT%.
) else (
    echo [SUCCESS] Build completed.
)
echo.
pause
exit /b %BUILD_EXIT%
