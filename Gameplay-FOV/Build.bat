@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "EDAY_COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%EDAY_COMPILER%" exit /b 1
if not exist "%~dp0Build" mkdir "%~dp0Build"
"%EDAY_COMPILER%" /nologo /optimize+ /target:exe /platform:x64 /out:"%~dp0Build\E-Day-32x9-FOV.exe" "%~dp0E-Day-*.cs"
set "EDAY_RESULT=%ERRORLEVEL%"
if /i not "%~1"=="--no-pause" pause
exit /b %EDAY_RESULT%
