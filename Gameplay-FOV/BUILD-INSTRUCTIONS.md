# Build 32:9 cutscenes + gameplay FOV

Download/clone this repository or extract the matching Source ZIP on 64-bit Windows with .NET Framework 4.x.
Double-click Build.bat or run `Build.bat --no-pause`.
Output: Build/E-Day-32x9-FOV.exe

The script uses %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe,
/optimize+ /target:exe /platform:x64, and E-Day-*.cs production inputs.
No downloaded dependency, assembler, game file or external SDK is required.
FixtureLayout.cs and test entry points are excluded from production.

Run Test.bat for all C# fixtures, including staged-startup and automatic-build
policy tests. These tests inspect/change only their own allocated fixture data.
The optional cinematic assembly/disassembly documents the unchanged embedded
view wrapper and its validated relocations. Normal builds do not assemble it.

DISTRIBUTED-BINARY-SHA256.txt identifies the packaged executable. Compiler/PE
metadata differences can produce different rebuild bytes. Source manifests
identify the exact package inputs; byte-identical rebuilds are not claimed.
