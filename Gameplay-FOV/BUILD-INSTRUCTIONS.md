# Build from source

On 64-bit Windows with .NET Framework 4 installed, double-click Build.bat or run
`Build.bat --no-pause`. It uses the Windows Framework64 v4.0.30319 csc.exe and
outputs Build/E-Day-32x9-FOV.exe. No external SDK, assembler, network download, or game file
is needed to compile. Copy that helper with the runtime launcher and settings.

The readable camera-frame-gate.asm and its disassembly document the embedded
Windows x64 wrapper. E-Day-CameraFrameGate.cs includes its machine bytes and
patches two allocation-specific pointer relocations; the normal build does not
assemble external files.

Run `Test.bat` to compile and run every included fixture.

Fixtures execute against allocated fake camera and dispatch data in their own
test process. They do not open or modify the game. Compile all production E-Day-*.cs files plus the desired fixture with csc.exe /nologo /platform:x64
/target:exe /main:CameraFrameGateTests /out:Build/CameraFrameGateTests.exe.
For lens fixtures use /main:LensAdaptiveTests, and in the combined variant use
/main:GameplayFovTests for gameplay fixtures. Run each resulting test executable.
The test source declares its Main entry explicitly if inspection is needed.

See VALIDATION.txt and SHA256SUMS.txt for checks and package hashes. Compiler
output bytes may differ across compiler revisions; source builds retain the same
behavior without requiring byte-identical PE output.
