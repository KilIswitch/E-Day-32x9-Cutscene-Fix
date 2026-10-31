# E-Day startup fix: 32:9 cutscenes only

Steam +++fenix2+fairlight-omega-release-CL-4927986, module size 323710976.
Prepared October 30, 2026.

## Source variants

| Variant | Complete source | Build instructions |
| --- | --- | --- |
| Cutscenes only | [Root](./) | [Build](BUILD-INSTRUCTIONS.md) |
| Cutscenes + gameplay FOV | [Gameplay-FOV](Gameplay-FOV/) | [Build](Gameplay-FOV/BUILD-INSTRUCTIONS.md) |
| Gameplay FOV only | [Gameplay-FOV-Only](Gameplay-FOV-Only/) | [Build](Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md) |

All three variants include the startup retry fix and automatic required build
validation. See [Nexus review guide](NEXUS-FOV-REVIEW.md) for distributed hashes.

## What changed

The game exposes native class registration code in stages during startup.
The previous helper could see some classes before TCPlayerCameraManager_CamBridge
and incorrectly report a permanent incompatible layout. Missing required class
registration now remains a retryable initialization condition. No candidate
layout is published and no game changes are made until validation completes.
Progress is printed when the initialization reason changes rather than printing
the entire discovery message on every retry.

The Yes/No build-number question is removed. Different build numbers automatically
attempt full camera layout discovery and validation. This does not skip missing,
ambiguous, contradictory or invalid layout checks: a genuine signature or field
mismatch still stops the helper before it applies the mod. A class that remains
unavailable keeps the launcher waiting until ready or game exit. Future changed
code/structures can still require another mod revision.

## Install or update

Close the game before replacing its existing helper. Preserve your existing TXT
settings. Copy E-Day-32x9.exe, Launch-E-Day-32x9.bat, and the following settings beside GoWEDay-Steam.exe:

- E-Day-32x9-framing.txt

Keep Steam running and double-click Launch-E-Day-32x9.bat. The launcher starts the full Steam
game directly and waits for initialization without a fixed time limit. Leave
the launcher open until game exit. Apply-E-Day-32x9.bat and Restore-E-Day-Cutscenes.bat are optional shortcuts.
Use only one camera variant per session.

Enable Cinematic Ultrawide Support. E-Day-32x9-framing.txt accepts 1.00 through
8.00; packaged default 3.50. Lens-aware synchronous framing and original off-mode
restoration remain unchanged.

## Restore and compatibility

Run Restore-E-Day-Cutscenes.bat or close the game to discard session changes. Normal Steam launches
do not start this helper. Game executable files and permanent environment
settings are unchanged. Existing offline/protected-session checks remain.
The launcher's Steam-ID preservation/owned-file cleanup behavior is unchanged.

Steam only. This release does not establish Game Pass compatibility. Compatible
future relocations can be discovered; future structural/code changes are not
guaranteed. Full campaign/every camera/resolution remain unverified.

## Validation and source

This variant passed 111 fixture checks. These include staged registration
appearing after an initial scan, no partial layout publication, successful retry,
strict rejection of genuine mismatches, automatic build policy, relocated old/new
fields and 1000 masked-search comparisons. Fixtures use their own process only.

Live startup diagnostics reproduced the old fatal missing-class result before
the same build later passed all checks. The corrected resolver passed three
fresh-start checks on CL-4927986 without an incompatible result. See VALIDATION.txt
for details and limits. This folder contains the matching full source, build instructions and fixtures.
Compiled executables are excluded from this source repository.
