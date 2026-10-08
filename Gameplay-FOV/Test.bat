@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "EDAY_TEST_COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%EDAY_TEST_COMPILER%" exit /b 1
if not exist "%~dp0Build" mkdir "%~dp0Build"
"%EDAY_TEST_COMPILER%" /nologo /platform:x64 /target:exe /main:CameraFrameGateTests /out:"%~dp0Build\CameraFrameGateTests.exe" "%~dp0E-Day-32x9.cs" "%~dp0E-Day-CameraFraming.cs" "%~dp0E-Day-CameraFrameGate.cs" "%~dp0E-Day-GameplayFov.cs" "%~dp0CameraFrameGateTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\CameraFrameGateTests.exe"
if errorlevel 1 exit /b 1
"%EDAY_TEST_COMPILER%" /nologo /platform:x64 /target:exe /main:LensAdaptiveTests /out:"%~dp0Build\LensAdaptiveTests.exe" "%~dp0E-Day-32x9.cs" "%~dp0E-Day-CameraFraming.cs" "%~dp0E-Day-CameraFrameGate.cs" "%~dp0E-Day-GameplayFov.cs" "%~dp0LensAdaptiveTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\LensAdaptiveTests.exe"
if errorlevel 1 exit /b 1
"%EDAY_TEST_COMPILER%" /nologo /platform:x64 /target:exe /main:GameplayFovTests /out:"%~dp0Build\GameplayFovTests.exe" "%~dp0E-Day-32x9.cs" "%~dp0E-Day-CameraFraming.cs" "%~dp0E-Day-CameraFrameGate.cs" "%~dp0E-Day-GameplayFov.cs" "%~dp0GameplayFovTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\GameplayFovTests.exe"
if errorlevel 1 exit /b 1
exit /b 0
