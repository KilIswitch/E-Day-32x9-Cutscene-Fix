# E-Day 32:9 cutscenes + customizable gameplay FOV

## Lens-aware cinematic framing

This update reduces the extra cinematic widening on already-wide lenses. Normal and tighter lenses retain the configured framing multiplier. The reference is a 35 mm lens on the game's 24.892 mm filmback width; equivalent lens angles on other filmback widths use the same rule. The base aspect-ratio correction is always retained. The helper reads the animated focal length and filmback width, and adjusts overscan only. It does not alter authored focal length or filmback.

Keep your existing E-Day-32x9-framing.txt when updating. The included preset remains 3.50; the live test used the user's custom 3.00 value. The user confirmed improved framing on the tested 21 mm shot. Results for the whole campaign have not been checked.

This folder contains the full source for the lens-aware revision. The distributed executable is identified by DISTRIBUTED-BINARY-SHA256.txt.


This is a separate version of the offline Steam cutscene fix. It retains the
3.50 cinematic framing preset and adds a gameplay FOV input of **120**.
The gameplay control uses the game's own gameplay settings multiplier; it does
not change cinematic lenses or overwrite the final camera view every frame.
Normal aiming and zoom calculations continue through the game's camera logic.

## Install and launch

1. Extract the archive, then copy these four required runtime files beside
   `GoWEDay-Steam.exe` in `FairlightConcept\Binaries\Win64`:
   - Launch-E-Day-32x9-FOV.bat
   - E-Day-32x9-FOV.exe
   - E-Day-32x9-framing.txt
   - E-Day-gameplay-fov.txt
2. Keep Steam running and double-click **Launch-E-Day-32x9-FOV.bat**.
3. Enable **Cinematic Ultrawide Support** for the cutscene fix.
4. Leave the launcher window open until the game exits.

**Apply-E-Day-32x9-FOV.bat** and **Restore-E-Day-32x9-FOV.bat** are optional
shortcuts; they are not required for launching the mod.

For an already running offline session, use **Apply-E-Day-32x9-FOV.bat**.
If the original cutscene helper is active, restore it first before applying
this version. The two versions share a session guard and should not run
simultaneously. Existing original-version files can remain beside these files.
Both versions use the same cinematic framing text file; preserve your custom
framing value when installing if you have changed it.

## Customize gameplay FOV

Edit **E-Day-gameplay-fov.txt** beside the game executable. It contains one
number, initially `120`. Accepted values are `60` through `150`, including
decimals. Save the file to apply a change while the helper is running; it reads
the setting about every 250 milliseconds. Resume gameplay after pausing so the
camera can refresh.

Use `0` to disable only the gameplay FOV override and restore the game's own
setting. The cutscene fix continues running. Invalid or incomplete edits keep
the last valid value; a missing configuration uses the default 120 on startup.
This setting replaces the game's gameplay FOV input. The game's UI slider
still displays and stores its normal 60-90 value. Aiming, sprinting, and special
cameras can have their own dynamic framing, so the rendered FOV need not stay
at the configured number in every state.

Edit **E-Day-32x9-framing.txt** separately to tune cutscenes. Its included value
is `3.50`; accepted values are `1.00` through `8.00`. Larger values show more of
the cinematic scene. This file does not control gameplay FOV.

## Restore and uninstall

**Restore-E-Day-32x9-FOV.bat** stops the helper and restores its gameplay and
cinematic memory changes. Closing the game also discards all process-memory
changes. For a comparison of gameplay only, set E-Day-gameplay-fov.txt to `0`.

To uninstall this variant, restore it or close the game, then remove its three
FOV batch files, E-Day-32x9-FOV.exe, and E-Day-gameplay-fov.txt. The shared
E-Day-32x9-framing.txt can remain if you use the original cutscene version.
For a session without either helper, launch through Steam's normal Play button.

## Compatibility and behavior

Tested on the Steam full-game build
`+++fenix2+fairlight-omega-release-CL-4894958` at 5120 x 1440. The user confirmed
the 120 option visibly widened gameplay. Tests cover configuration parsing,
cinematic-target gating, live configuration changes, restoration, and object
identity checks. Every weapon, every camera mode, the full campaign, other
resolutions, and future builds have not been checked.

Other builds present the same Yes/No prompt. Yes allows a compatibility attempt;
all cinematic and gameplay code signatures and object/value checks remain
enabled. No skips the mod and keeps the launched game running. Initialization
has no time limit and stops when ready or the game exits. Protected sessions
are refused. Offline use only; no anti-cheat approval or online safety is claimed.

The launcher starts GoWEDay-Steam.exe directly, requires Steam, and locally sets
EOS_USE_ANTICHEATCLIENTNULL=1 and SteamAppId=3010850. It creates steam_appid.txt
only if absent, preserves an existing correct ID, refuses a different ID,
waits for game exit, and removes only the ID it created.

This version changes the four-byte cinematic width constant, cinematic
overscan/flags, and the validated gameplay camera manager's FOV multiplier in
memory. It restores its own values when safe to do so and skips gameplay
adjustment while a valid cinematic target is selected. It does not patch
executable instructions, game files on disk, or permanent environment settings.
The helper does not perform network communication or downloads.

Windows PowerShell and .NET Framework 4.x are used. The high-resolution camera
timer requires Windows 10 version 1803 or newer, plus the game's requirements.
Full source and rebuild instructions for this revision are included in this folder and the separate source package. See BUILD-INSTRUCTIONS.md to compile it.
