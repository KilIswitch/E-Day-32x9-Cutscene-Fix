# E-Day 32:9 cinematic fix - source and build instructions

## Versions

- The repository root contains the original cutscene-only helper and its build instructions.
- [Gameplay-FOV](Gameplay-FOV/README.md) contains the full source for the cutscene fix plus customizable gameplay FOV, default 120. Build that version using [Gameplay-FOV/BUILD-INSTRUCTIONS.md](Gameplay-FOV/BUILD-INSTRUCTIONS.md).


- [Gameplay-FOV-Only](Gameplay-FOV-Only/README.md) contains the separate gameplay-only version, default 120, which leaves cinematic framing unchanged. Build it using [Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md](Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md).

For Nexus review of the two FOV versions, see [NEXUS-FOV-REVIEW.md](NEXUS-FOV-REVIEW.md). Each version has its own full source, build instructions, validation notes, and distributed executable hash.

The gameplay-only version needs three runtime files: `E-Day-Gameplay-FOV.exe`,
`Launch-E-Day-Gameplay-FOV.bat`, and `E-Day-gameplay-fov.txt`.

The combined cutscene + gameplay FOV version needs only four runtime files beside GoWEDay-Steam.exe:
`E-Day-32x9-FOV.exe`, `Launch-E-Day-32x9-FOV.bat`,
`E-Day-gameplay-fov.txt`, and `E-Day-32x9-framing.txt`.
Its Apply and Restore batch files are optional shortcuts. Compiled executables
are excluded from this source repository; the build scripts produce them locally.


This repository contains the full source for the E-Day 32:9 helper and the readable
batch launch/apply/restore scripts. No repository, package manager, game files,
or external libraries are needed to compile the application.

## Prerequisites

Use 64-bit Windows with the .NET Framework 4.x compiler available at:

`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

The path's v4.0.30319 component is the .NET Framework directory name. The actual
compiler version can be recorded using `csc.exe /help`. Windows PowerShell is
used by the runtime launcher, not by the compiler. Runtime camera timing uses
the high-resolution waitable timer supported by Windows 10 version 1803 onward.
The game has its own Windows requirements.

## Build using the supplied script

1. Download and extract this repository, or clone it, into a writable local directory.
2. Open the repository directory containing Build.bat and the three C# source files.
3. Double-click Build.bat. It compiles the three C# files into
   `Build\E-Day-32x9.exe`, displays the result, and waits for a key press.
4. A successful build exits with code 0. Build.bat does not run the helper, launch
   the game, download dependencies, or require administrator privileges.

For unattended compilation, run `Build.bat --no-pause` from Command Prompt.

## Equivalent manual compilation

Open Command Prompt in the repository root directory and run:

```bat
if not exist Build mkdir Build
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /out:"Build\E-Day-32x9.exe" "E-Day-32x9.cs" "E-Day-CameraFraming.cs" "E-Day-CameraSources.cs"
```

Entry point: `CinematicFix.Main` in E-Day-32x9.cs. Target: x64 Windows console
application. All dependencies are framework assemblies or Windows kernel32
APIs. There are no embedded assets, generated-source prerequisites, NuGet
dependencies, external DLLs, obfuscation, packing, or post-build steps.

## Source map

- E-Day-32x9.cs: arguments, build prompt, compatibility checks, cinematic width
  constant, initialization retrying, and main entry point.
- E-Day-CameraFraming.cs: camera validation, object identities, overscan changes,
  timer, background process management, configuration reading, and restoration.
- E-Day-CameraSources.cs: loaded sequence camera template preparation and
  inherited-overscan tracking to avoid the camera-cut zoom flash.
- *.bat: full readable source for the three user entry points.
- E-Day-32x9-framing.txt: included preset, 3.50.
- DISTRIBUTED-BINARY-SHA256.txt: hash of the distributed binary submitted for review. The compiled binary is not stored in this source repository.

## Binary comparison

SHA256SUMS.txt identifies the supplied source and documentation files.
DISTRIBUTED-BINARY-SHA256.txt identifies the distributed binary submitted for
review. A rebuilt binary may differ because of compiler version, PE timestamps,
and generated module metadata. No byte-for-byte reproducibility claim is made.
Compare source, compiler settings, imports, and disassembled managed code when
reviewing the rebuilt binary.

PowerShell hash example:

```powershell
Get-FileHash -Algorithm SHA256 .\Build\E-Day-32x9.exe
```

## Optional runtime review

Building does not require owning or launching the game. No compiled game binaries or assets are included. To review functionality,
use a separate offline Steam full-game session. The tested executable version is
`+++fenix2+fairlight-omega-release-CL-4894958`, tested visually at 5120 x 1440.
Copy the rebuilt executable and the four batch/configuration files from this repository beside
GoWEDay-Steam.exe in FairlightConcept\Binaries\Win64. Keep Steam running,
double-click Launch-E-Day-32x9.bat, and enable Cinematic Ultrawide Support.
Keep the launcher window open until game exit. Restore-E-Day-Cutscenes.bat
restores runtime changes, and game exit discards process memory changes.

For a different build, Yes permits an experimental attempt; No or Enter skips
the helper. Signature, memory-value, object, and protected-session checks remain
enabled. A build with a different memory layout still fails without changes.
Future build support and anti-cheat approval are not claimed.
