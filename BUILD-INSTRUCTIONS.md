# Build: 32:9 cutscenes only

1. Download/clone this repository or extract the matching Source ZIP to a writable directory on 64-bit Windows.
2. With .NET Framework 4.x installed, double-click Build.bat (or run
   `Build.bat --no-pause`).
3. The result is `Build/E-Day-32x9.exe`. Copy it with the launcher and settings files
   described in README.md beside GoWEDay-Steam.exe.

Build.bat uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` with
/platform:x64 /target:exe /optimize+ and every E-Day-*.cs production file.
There are no external SDK, assembler, network or game-file build dependencies.
FixtureLayout.cs and the *Tests.cs files are excluded from production.

Run Test.bat to compile and execute all included fixtures. The fixture compiler
also uses /main to select one test entry point. Tests allocate and inspect only
their own fake data; they do not attach to the game. No Python dependency is
needed to build the helper or run these C# tests.

The cinematic ASM and disassembly, when present, match the embedded x64 wrapper
before its pointer and validated fill-field relocations. They are provided for
review; Build.bat does not assemble them. Native cinematic-member structures
remain validated and intentionally are not guessed on unfamiliar code layouts.

DISTRIBUTED-BINARY-SHA256.txt identifies this packaged EXE. Different compiler
revisions or PE timestamps may yield different binary bytes from equivalent
source builds. SHA256SUMS.txt lists all packaged source-file hashes.
