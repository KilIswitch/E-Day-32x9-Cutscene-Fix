@echo off
setlocal
cd /d "%~dp0"
"%~dp0E-Day-Gameplay-FOV.exe" --wait forever
set "EDAY_FOV_RESULT=%ERRORLEVEL%"
if not "%EDAY_FOV_RESULT%"=="0" pause
exit /b %EDAY_FOV_RESULT%
