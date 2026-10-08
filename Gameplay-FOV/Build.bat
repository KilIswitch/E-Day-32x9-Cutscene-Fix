@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "EDAY_REVIEW_COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "EDAY_REVIEW_RESULT=1"
if not exist "%EDAY_REVIEW_COMPILER%" (
    echo The 64-bit .NET Framework C# compiler was not found.
    echo Read BUILD-INSTRUCTIONS.md for prerequisites.
    goto finish
)
if not exist "%~dp0Build" mkdir "%~dp0Build"
if not exist "%~dp0Build" goto finish
"%EDAY_REVIEW_COMPILER%" /nologo /target:exe /platform:x64 /out:"%~dp0Build\E-Day-32x9-FOV.exe" "%~dp0E-Day-32x9.cs" "%~dp0E-Day-CameraFraming.cs" "%~dp0E-Day-CameraFrameGate.cs" "%~dp0E-Day-GameplayFov.cs"
set "EDAY_REVIEW_RESULT=%ERRORLEVEL%"
if "%EDAY_REVIEW_RESULT%"=="0" echo Build succeeded: "%~dp0Build\E-Day-32x9-FOV.exe"
:finish
if /i not "%~1"=="--no-pause" pause
exit /b %EDAY_REVIEW_RESULT%
