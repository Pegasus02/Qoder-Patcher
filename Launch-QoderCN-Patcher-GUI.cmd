@echo off
setlocal
set "PROJECT_ROOT=%~dp0"
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%PROJECT_ROOT%src\QoderCN-Patcher-GUI.ps1" %*
if errorlevel 1 (
  echo.
  echo The GUI exited with an error. Review the message above.
  pause
)
endlocal
