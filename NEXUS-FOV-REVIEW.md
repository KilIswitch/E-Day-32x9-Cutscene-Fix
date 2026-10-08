# Nexus review: cinematic and gameplay FOV versions

The three variants have separate source locations and executables. This page
identifies the versions provided for review; publication here does not establish
Nexus approval. The repository root contains the lens-aware cutscene-only source.

## Complete source and build instructions

| Version | Full source folder | Build instructions | Output |
| --- | --- | --- | --- |
| 32:9 cutscenes only | [Repository root](./) | [Build instructions](README.md#build-using-the-supplied-script) | `Build/E-Day-32x9.exe` |
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
| 32:9 cutscenes only | `E-Day-32x9.exe` | `68fe5cc3aa584507210f6681f0f30b479298fe6f0588a363057d79ff144030ea` |
| Combined 32:9 cutscenes + gameplay FOV | `E-Day-32x9-FOV.exe` | `a4489f779432c814b77098a42950552e40abf70bb58d75450a4e473813df1f0a` |
| Gameplay FOV only | `E-Day-Gameplay-FOV.exe` | `00c5bf5d7659dfabce6b55a3ef79aaebacf5fab911977469f83c518624c73e1b` |

Each source folder includes `DISTRIBUTED-BINARY-SHA256.txt` and `SHA256SUMS.txt`.
The distributed executables are excluded from this source repository. A fresh
build can have different PE timestamps/module metadata and is not guaranteed
to match the distributed binary byte for byte. The build commands and source
are provided for managed-code review.

## Lens-aware update and version behavior

- The root and combined cinematic versions reduce extra widening on wide
  lens angles while preserving the full multiplier on tighter lenses. They
  read filmback/focal fields, validate an additional native signature, and
  adjust only the existing overscan data. Both keep the 3.50 preset and
  loaded sequence template preparation. Gameplay-only is unchanged.
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

See [cinematic validation](VALIDATION.txt), [combined validation](Gameplay-FOV/VALIDATION.txt) and
[gameplay-only validation](Gameplay-FOV-Only/VALIDATION.txt).
The new lens calculation passed 11 checks. The combined gameplay memory
fixtures passed all 16 checks again. The supplied builds succeeded and the
user confirmed the tested wide shot and subsequent camera changes looked good.
The prior combined version also passed 13 launcher/helper checks; the launcher
has not changed in this revision.
The combined gameplay adjustment was previously tested visually at 120 on
Steam full-game CL 4894958
at 5120x1440. The separate gameplay-only version passed 21 synthetic fixtures,
including unchanged cinematic memory sentinels and session isolation, and 13
launcher/helper checks. It has not had a separate live visual test.

Different builds present a Yes/No choice; choosing Yes keeps compatibility
checks enabled. Initialization waits until ready or the game exits. Protected
sessions are refused. No online safety, anti-cheat approval, complete campaign
coverage, or future-build compatibility is claimed. Helpers perform no network
communication or downloads.
