# Nexus review: both gameplay FOV versions

The two FOV variants have separate source folders and executables. This page
identifies the versions provided for review; publication here does not establish
Nexus approval. The repository root remains the original cutscene-only source.

## Complete source and build instructions

| Version | Full source folder | Build instructions | Output |
| --- | --- | --- | --- |
| 32:9 cutscenes + gameplay FOV | [Gameplay-FOV](Gameplay-FOV/) | [Build instructions](Gameplay-FOV/BUILD-INSTRUCTIONS.md) | `Build/E-Day-32x9-FOV.exe` |
| Gameplay FOV only | [Gameplay-FOV-Only](Gameplay-FOV-Only/) | [Build instructions](Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md) | `Build/E-Day-Gameplay-FOV.exe` |

Download/clone this repository and open the selected version's folder.
Double-click its `Build.bat`, or run `Build.bat --no-pause` from Command Prompt
in that folder. The 64-bit .NET Framework 4.x C# compiler is used. All required
C# source and batch files are included; there are no external libraries,
downloaded dependencies, obfuscation, embedded binaries, or post-build steps.
Building requires no game files and does not start the game or helper.

## Distributed binaries to identify

SHA-256 of each packaged executable:

| Version | Executable | SHA-256 |
| --- | --- | --- |
| Combined 32:9 cutscenes + gameplay FOV | `E-Day-32x9-FOV.exe` | `8e1380db2ef1634065acb55f3106ed97d26eb3761290e6913301d14521c4546d` |
| Gameplay FOV only | `E-Day-Gameplay-FOV.exe` | `00c5bf5d7659dfabce6b55a3ef79aaebacf5fab911977469f83c518624c73e1b` |

Each source folder includes `DISTRIBUTED-BINARY-SHA256.txt` and `SHA256SUMS.txt`.
The distributed executables are excluded from this source repository. A fresh
build can have different PE timestamps/module metadata and is not guaranteed
to match the distributed binary byte for byte. The build commands and source
are provided for managed-code review.

## Changes from the original cutscene-only helper

- Combined version: retains the 3.50 cinematic preset and camera-template
  preparation, adds the validated gameplay settings multiplier, default 120,
  and reads `E-Day-gameplay-fov.txt` separately from cinematic framing.
- Gameplay-only version: removes all cinematic width-limit, lens, overscan,
  crop, and template adjustments. Its only game-memory write paths apply and
  restore the gameplay manager's FOV settings multiplier at `+0x5ea8`.
- Both read customization live, permit values 60-150 or 0 to restore the game's
  own setting, suspend gameplay adjustment while a valid cinematic target is
  active, validate object identity/signatures/values, and restore owned values.
- Both launchers retain the launch-local Steam ID/environment behavior and
  require an offline unprotected session. They do not patch game executables
  on disk or change permanent environment variables.

## Validation and scope

See [combined validation](Gameplay-FOV/VALIDATION.txt) and
[gameplay-only validation](Gameplay-FOV-Only/VALIDATION.txt).
The combined version passed 16 synthetic fixtures and 13 launcher/helper checks;
its gameplay adjustment was tested visually at 120 on Steam full-game CL 4894958
at 5120x1440. The separate gameplay-only version passed 21 synthetic fixtures,
including unchanged cinematic memory sentinels and session isolation, and 13
launcher/helper checks. It has not had a separate live visual test.

Different builds present a Yes/No choice; choosing Yes keeps compatibility
checks enabled. Initialization waits until ready or the game exits. Protected
sessions are refused. No online safety, anti-cheat approval, complete campaign
coverage, or future-build compatibility is claimed. Helpers perform no network
communication or downloads.
