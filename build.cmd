@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo   Qoder CN OpenAI Patcher - 原生 EXE 一键编译脚本
echo ===================================================

set "CSC="
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if "%CSC%"=="" (
    echo [ERROR] 未找到系统的 .NET Framework C# 编译器 csc.exe！
    pause
    exit /b 1
)

if not exist "%~dp0bin" mkdir "%~dp0bin"

echo 编译器: %CSC%
echo 正在编译...

"%CSC%" /target:winexe /out:"%~dp0bin\QoderCN-Patcher.exe" /win32manifest:"%~dp0src\gui\app.manifest" /platform:anycpu /optimize+ /utf8output /r:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Web.Extensions.dll "%~dp0src\gui\Program.cs" "%~dp0src\gui\PatcherCore.cs" "%~dp0src\gui\MainForm.cs"

if %ERRORLEVEL% equ 0 (
    copy /y "%~dp0bin\QoderCN-Patcher.exe" "%~dp0QoderCN-Patcher.exe" >nul
    if exist "%~dp0bin\configs" rmdir /s /q "%~dp0bin\configs" >nul 2>&1
    echo.
    echo ===================================================
    echo [OK] 编译成功！生成文件：QoderCN-Patcher.exe
    echo 可直接双击项目根目录或 bin\ 目录下的 QoderCN-Patcher.exe 运行！
    echo ===================================================
    echo.
) else (
    echo.
    echo [ERROR] 编译失败，请检查错误提示。
    pause
    exit /b %ERRORLEVEL%
)

if "%1"=="--run" (
    start "" "%~dp0bin\QoderCN-Patcher.exe"
)
