@echo off
:: Double-click entry point — calls build.ps1 via PowerShell.
:: If you see "execution of scripts is disabled", run this instead:
::   Set-ExecutionPolicy -Scope CurrentUser RemoteSigned

powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0build.ps1" %*
if %ERRORLEVEL% neq 0 (
    echo.
    echo Build failed. See errors above.
    pause
    exit /b %ERRORLEVEL%
)
pause
