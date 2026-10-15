# Nexus review: CL-4909945 and validated dynamic layouts

This source revision updates all three Steam helpers for
+++fenix2+fairlight-gamma-release-CL-4909945 (module size 324030464).
The binaries identified here were packaged on October 15, 2026. This guide
provides source/build evidence, not Nexus or anti-cheat approval.

| Variant | Complete source | Build instructions | Output |
| --- | --- | --- | --- |
| Cutscenes only | [Root](./) | [Build](BUILD-INSTRUCTIONS.md) | Build/E-Day-32x9.exe |
| Cutscenes + gameplay FOV | [Gameplay-FOV](Gameplay-FOV/) | [Build](Gameplay-FOV/BUILD-INSTRUCTIONS.md) | Build/E-Day-32x9-FOV.exe |
| Gameplay FOV only | [Gameplay-FOV-Only](Gameplay-FOV-Only/) | [Build](Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md) | Build/E-Day-Gameplay-FOV.exe |

## Building and reviewing

Download or clone this repository, open the chosen folder, and run Build.bat
or `Build.bat --no-pause`. It uses Windows Framework64 v4.0.30319 csc.exe and
all E-Day-*.cs production files in that folder. No game, SDK, downloaded package
or assembler is needed. Test.bat compiles and runs that variant's fixtures.

GameLayout (E-Day-GameLayout.cs) discovers executable addresses and selected
manager fields from masked code, relative references, native class registration,
default-object vtables and property metadata. It validates the complete required
layout before publication. There is no fallback to old fixed RVAs. An untested
build still requires Yes/No consent; Yes never skips layout validation.
Compatible relocations/field changes can be discovered; changed code/native
structures can still require another revision. Game Pass is not validated.

The cinematic wrapper retains its synchronous lens-aware correction and asset
exclusion. Its readable assembly/disassembly match the embedded machine bytes
before two pointer relocations and the newly validated cinematic fill-field
displacement. [Review notes](REVIEW-NOTES.md) describe memory writes/lifetime.

## Distributed executable SHA-256

| Executable | SHA-256 |
| --- | --- |
| E-Day-32x9.exe | `5109773414da628dec886504683747a629eed5eca5e080e3ebed1204cbdbba4d` |
| E-Day-32x9-FOV.exe | `28591350d03e037c7893e2ba9d5b7ffee4b535b6308e2ffc789d52357b5941ef` |
| E-Day-Gameplay-FOV.exe | `9da2fd816cda483f26e4bacc00112e668f5ed1f1f1bb64d7e5e210e029e63785` |

Each source folder has DISTRIBUTED-BINARY-SHA256.txt and a SHA256SUMS.txt source
manifest. Executables are excluded from GitHub. PE metadata/compiler revisions
can change rebuild hashes; byte-identical recompilation is not claimed.

## Validation

Cutscenes-only: 113 checks. Combined: 145 checks. Gameplay-only: 82 checks.
Each suite includes 31 layout checks (including 1000 masked-search comparisons).
Layout fixtures cover relocated images, old/new manager members, invalid field
bounds, ambiguous/missing signatures, changed clamp constants, metadata conflicts
and object identities. Camera and gameplay fixtures use both member layouts.
All fixture memory belongs to the test process; tests never attach to the game.

The combined helper was live-tested on CL-4909945 at 5120x1440. The user confirmed
correct cutscene framing with no camera-change flashes and working gameplay FOV
and aiming. Undo/check/reapply succeeded; existing settings hashes were preserved.
Other variants share the validated production components and passed their own
fixtures; they were not separately visually tested. Full campaign/all cameras,
other resolutions, future updates and Game Pass remain unverified.

See [root validation](VALIDATION.txt), [combined validation](Gameplay-FOV/VALIDATION.txt),
and [gameplay-only validation](Gameplay-FOV-Only/VALIDATION.txt).
