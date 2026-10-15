@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "EDAY_COMPILER=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%~dp0Build" mkdir "%~dp0Build"
"%EDAY_COMPILER%" /nologo /optimize+ /platform:x64 /target:exe /main:CameraFrameGateTests /out:"%~dp0Build\CameraFrameGateTests.exe" "%~dp0E-Day-*.cs" "%~dp0FixtureLayout.cs" "%~dp0CameraFrameGateTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\CameraFrameGateTests.exe"
if errorlevel 1 exit /b 1
"%~dp0Build\CameraFrameGateTests.exe" gamma
if errorlevel 1 exit /b 1
"%EDAY_COMPILER%" /nologo /optimize+ /platform:x64 /target:exe /main:LensAdaptiveTests /out:"%~dp0Build\LensAdaptiveTests.exe" "%~dp0E-Day-*.cs" "%~dp0FixtureLayout.cs" "%~dp0LensAdaptiveTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\LensAdaptiveTests.exe"
if errorlevel 1 exit /b 1
"%EDAY_COMPILER%" /nologo /optimize+ /platform:x64 /target:exe /main:GameplayFovTests /out:"%~dp0Build\GameplayFovTests.exe" "%~dp0E-Day-*.cs" "%~dp0FixtureLayout.cs" "%~dp0GameplayFovTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\GameplayFovTests.exe"
if errorlevel 1 exit /b 1
"%~dp0Build\GameplayFovTests.exe" gamma
if errorlevel 1 exit /b 1
"%EDAY_COMPILER%" /nologo /optimize+ /platform:x64 /target:exe /main:BuildPromptTests /out:"%~dp0Build\BuildPromptTests.exe" "%~dp0E-Day-*.cs" "%~dp0FixtureLayout.cs" "%~dp0BuildPromptTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\BuildPromptTests.exe"
if errorlevel 1 exit /b 1
"%EDAY_COMPILER%" /nologo /optimize+ /platform:x64 /target:exe /main:GammaLayoutTests /out:"%~dp0Build\GammaLayoutTests.exe" "%~dp0E-Day-*.cs" "%~dp0FixtureLayout.cs" "%~dp0LayoutTests.cs"
if errorlevel 1 exit /b 1
"%~dp0Build\GammaLayoutTests.exe"
if errorlevel 1 exit /b 1
exit /b 0
