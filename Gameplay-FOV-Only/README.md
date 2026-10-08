# E-Day gameplay FOV only

A separate offline Steam version that adjusts gameplay FOV, default **120**.
Cutscenes use the game's own framing and Cinematic Ultrawide Support setting.
This version contains no cinematic width-limit, overscan, crop, lens, or camera
template adjustment. It suspends its gameplay override during cinematics.

## Install and launch

Copy these **three required files** beside `GoWEDay-Steam.exe` in
`FairlightConcept\Binaries\Win64`:

- `E-Day-Gameplay-FOV.exe`
- `Launch-E-Day-Gameplay-FOV.bat`
- `E-Day-gameplay-fov.txt`

Keep Steam running, double-click `Launch-E-Day-Gameplay-FOV.bat`, and keep the
launcher window open until game exit. There is no cutscene framing text file.
The existing Cinematic Ultrawide Support option can be on or off.

The launcher requires Steam, starts the full-game executable directly from its
own directory, and sets `EOS_USE_ANTICHEATCLIENTNULL=1` and `SteamAppId=3010850`
only within that launch. It creates `steam_appid.txt` containing `3010850` only
if absent, preserves an existing correct file, and refuses a different ID. It
waits for game exit and removes only the ID file it created.

## Switching from another version

Close the game before switching, or restore the currently active helper using
that version's Restore shortcut. Helpers share a session guard and cannot run
simultaneously. This version does not undo a different helper's cutscene changes.
Closing the game discards all runtime memory changes.

The gameplay text file has the same name as the combined version's file. Keep
your existing custom value if you want to preserve it instead of copying the
included 120 preset.

## Customize

Put one number in `E-Day-gameplay-fov.txt`, initially `120`. Accepted values:
`60` through `150`, including decimals. Use `0` to restore the game's own
gameplay setting while leaving the helper available for later edits.

Save changes while playing; the helper reads the text about every 250 ms.
Invalid edits retain the last valid value. A missing file defaults to 120.
Resume after pausing to let the camera refresh.

This is the game's gameplay FOV input. The UI slider still stores its normal
60-90 setting. The game's aiming, sprinting, and other dynamic camera logic can
adjust the rendered view; aiming may also be wider with a higher input.

## Restore and optional shortcuts

Closing the game restores all changes by discarding process memory. For a
gameplay comparison without closing it, set the text file to `0`.

The source package includes two optional shortcuts. Copy them beside the helper
if needed: `Apply-E-Day-Gameplay-FOV.bat` applies to an already running offline
session; `Restore-E-Day-Gameplay-FOV.bat` stops this helper and restores its own
gameplay multiplier. They are not needed for the usual double-click launch.

To uninstall, close the game and remove the three installed files and any
optional shortcuts. Launch through Steam's normal Play button for an unmodded
session.

## Compatibility and validation

Uses the validated gameplay settings multiplier from Steam full-game build
`+++fenix2+fairlight-omega-release-CL-4894958`. Default 120 uses the same gameplay
adjustment previously tested visually at 5120 x 1440 in the combined version.
The separate version passed 21 synthetic memory/configuration/session fixtures
and 13 launcher/helper checks; it has not had a separate live visual test.

Other builds present a Yes/No choice. Yes permits a compatibility attempt;
signature, object identity, original-value, and protected-session checks remain
enabled. No skips the helper while the launched game can continue. Initialization
waits without a time limit until ready or game exit. Updates may require new
signatures or offsets. Every weapon, mode, resolution, and future build is not
verified.

Offline use only. No online safety or anti-cheat approval is claimed. The helper
modifies only the validated gameplay manager's settings multiplier in process
memory, restores its own values when safe, and does not modify game files,
executable instructions, or permanent environment settings. It performs no
network communication or downloads.

## Source

The separate source package includes all three C# files, readable batch files,
build instructions, fixture tests, and checksums. See `BUILD-INSTRUCTIONS.md`.
