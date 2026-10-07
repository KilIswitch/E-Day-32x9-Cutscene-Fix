@echo off
setlocal EnableExtensions DisableDelayedExpansion
"%~dp0E-Day-32x9.exe" --restore
set "FIX_EXIT_CODE=%ERRORLEVEL%"
pause
exit /b %FIX_EXIT_CODE%
