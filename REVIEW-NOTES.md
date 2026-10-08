# Security review notes

Purpose: extend real-time cinematic rendering to 32:9 and adjust cinematic
overscan. Default framing is 3.50. This is intended for offline Steam sessions.
The sources correspond to the distribution binary identified in DISTRIBUTED-BINARY-SHA256.txt.

## Lens-aware revision

The cinematic helpers read animated focal length and filmback width to scale
extra overscan on wide lenses. The existing overscan/flag write paths and
restoration ownership checks are retained. No new game-memory write locations
were added. A native focal-field read signature is now validated.
DISTRIBUTED-BINARY-SHA256.txt identifies this revised root executable.
See VALIDATION.txt for the current lens tests and visual feedback.

## Behavior relevant to review

The helper accesses the running GoWEDay-Steam.exe process using OpenProcess,
ReadProcessMemory, and WriteProcessMemory. VirtualProtectEx temporarily makes
a four-byte data constant writable and then restores its prior protection.
The constant is changed from 3440/1440 to 5120/1440. Camera overscan and two
related overscan flags are updated on validated cinematic objects and loaded
sequence templates. It does not patch executable instructions or inject a DLL
or remote thread. Game executable and asset files on disk are not modified.

These process-memory APIs may be relevant to executable security scanning.
This note does not establish why the site's quarantine occurred or assert that
any detection is a false positive. Nexus staff must determine acceptability.

The helper checks the game process name, matching installation directory,
version/module size, code signatures, expected width values, object identities,
weak-reference serial numbers, and memory write readback. A different-build
Yes/No prompt allows the user to try the same validated layout; it does not
disable the remaining checks. PID/start-time/build-bound approval is passed to
the background helper as an argument and is not saved permanently.

Detected EasyAntiCheat_EOS processes or EasyAntiCheat modules cause refusal.
The launcher locally sets EOS_USE_ANTICHEATCLIENTNULL=1 and SteamAppId=3010850
and starts GoWEDay-Steam.exe directly rather than start_protected_game.exe.
This behavior is disclosed for review. No anti-cheat approval or online safety
is claimed, and no anti-cheat files or services are modified.

The helper launches another copy of itself with --watch in a hidden window to
maintain camera adjustments until game exit. Named mutexes and events are
scoped to the game PID and start time. The high-resolution timer waits 1 ms
while cinematic fill is active and 20 ms when inactive. Configuration is read
approximately every 250 ms. Initialization retries can wait until ready or
game exit. There is no startup service, scheduled task, registry persistence,
network communication, telemetry, self-update, or downloaded code.

The launcher uses Windows PowerShell. Its environment settings are local to
the launcher and its children. It creates steam_appid.txt containing 3010850
only when absent, preserves existing correct files, refuses a differing ID,
waits for the game to exit even if the helper fails or is declined, and removes
only the ID file created by that launch. The helper reads its adjacent framing
text file. No permanent system environment settings are changed.

On mode-off, restoration, normal helper shutdown, or cutscene completion,
owned camera values are restored when identities and current values still
match. Closing the game discards all runtime memory changes. Abruptly killing
the helper may interrupt cleanup; closing the game still discards those values.

## Validation and limits

The user confirmed the 3.50 framing, elimination of the cut-change flash in
the tested sequence, and normal 16:9 behavior with cinematic ultrawide off.
Nine input prompt fixtures and thirteen launcher/helper fixtures passed for
the current build-choice revision. A dummy incompatible executable was refused
after Yes without changes. Different real game builds, every cutscene, all
resolutions, and online sessions have not been tested. No security audit or
Nexus approval has yet been obtained.

Code provenance: the helper was generated with OpenAI Codex and iterated using
the user's visual feedback and runtime observations. This disclosure is included
for staff review; applicable AI-content tagging should be handled on the mod page.

Nexus guidelines supplied in the review email:
https://help.nexusmods.com/article/28-file-submission-guidelines

This repository provides source for security review. Its creation does not
claim that Nexus has approved the mod or that reviewers already have access.
No explicit source licence is granted by this repository.
