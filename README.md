# E-Day synchronous cinematic framing fix

This version corrects cinematic framing before each compatible camera's view
is calculated. It replaces the previous polling and asset-template preparation
approach that could leave a briefly zoomed frame at camera changes.

The included cinematic preset is 3.50. The lens-aware rule reduces additional
widening for already-wide lenses while keeping the full setting for normal and
tight lenses. Authored filmback and focal length remain intact. Valid cinematic
crop ratios are used directly; otherwise sensor aspect is recomputed before the
view calculation. Serialized MovieScene camera assets are left unchanged.

## Source variants

The repository root builds the cutscene-only helper. [Gameplay-FOV](Gameplay-FOV/)
builds the combined cutscene and gameplay FOV helper. [Gameplay-FOV-Only](Gameplay-FOV-Only/)
contains the separate gameplay-only helper, unchanged in this revision.

Run Build.bat in the chosen folder; see [build instructions](BUILD-INSTRUCTIONS.md).
Run Test.bat for the cinematic fixtures (42 checks here, 58 in Gameplay-FOV).
See [Nexus review guide](NEXUS-FOV-REVIEW.md) for all distributed binary hashes.
Compiled executables are excluded from this source repository.

## Installation

Copy these required runtime files beside GoWEDay-Steam.exe:

- E-Day-32x9.exe
- Launch-E-Day-32x9.bat
- E-Day-32x9-framing.txt

Keep your existing text settings when updating. Restore the previous helper or
close the game before replacing its executable. Keep Steam running, double-click
Launch-E-Day-32x9.bat, enable Cinematic Ultrawide Support, and leave the launcher window open
until the game exits. Apply-E-Day-32x9.bat and Restore-E-Day-Cutscenes.bat are optional shortcuts. Run one
cinematic variant at a time; they share a session guard.

## Settings

E-Day-32x9-framing.txt contains one number from 1.00 through 8.00. The packaged
default is 3.50. Larger values show more of the cinematic scene; the lens-aware
limit still applies on wider lenses. Changes are read about every 250 ms.
The viewport ratio is read from the game, rather than assuming a fixed pixel
resolution. The live visual test used 5120 x 1440; other resolutions remain
unverified. Turning Cinematic Ultrawide Support off restores authored framing
for the compatible views.

## Restore

Run Restore-E-Day-Cutscenes.bat to stop the helper and restore the memory values it owns.
Closing the game discards all session changes. After restoring or closing,
remove the chosen variant's runtime files to uninstall. Normal Steam launches
do not start this helper automatically.

## Implementation and compatibility

The helper validates the supported camera layout and replaces one eight-byte
virtual-method dispatch pointer in process memory. Its own allocated executable
wrapper adjusts cinematic overscan and two camera flags, then tail-calls the
original view method with its arguments preserved. It also changes the existing
four-byte cinematic limit. The combined variant additionally adjusts gameplay
FOV through its separate settings control.

Game executable instructions and game files on disk are not patched. The new
wrapper itself is executable code allocated in the running process. Restoration
disables it, restores the dispatch pointer, waits for active wrapper entries,
and restores owned camera data after identity checks. Roughly 1 MiB of ownership
data and two 4 KiB pages are retained disabled until game exit after installation,
to avoid freeing a wrapper while a thread could still be leaving it.

The launcher sets SteamAppId=3010850 and EOS_USE_ANTICHEATCLIENTNULL=1 only for
its launch. It requires Steam and starts GoWEDay-Steam.exe directly from its own
folder. It creates steam_appid.txt only when absent, refuses a different existing
ID, preserves an existing correct file, waits for exit, and removes only its own
created ID file. No permanent environment settings are changed.

Use the offline launch workflow. The existing anti-cheat/session checks remain.
Supported test build: +++fenix2+fairlight-omega-release-CL-4894958. A different
build prompts Yes/No before attempting compatibility checks; Yes does not bypass
the required signatures. Startup waits for readiness without a fixed time limit.
Unknown camera classes are excluded. The user confirmed no zoom flashes and
correct framing in two tested cutscenes; the complete campaign, every special
camera, other resolutions, and future builds have not been tested.

Matching source and fixtures are included in the separate Source archive.
This repository contains the complete source for this revision.
