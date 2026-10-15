# E-Day updated mod: Gameplay FOV only

Steam build: +++fenix2+fairlight-gamma-release-CL-4909945
Module size: 324030464. Release prepared October 15, 2026.

## Installation and updating

Close the game before replacing an existing helper. Keep your current text
settings when updating. Copy these files beside GoWEDay-Steam.exe:

- E-Day-Gameplay-FOV.exe
- Launch-E-Day-Gameplay-FOV.bat
- E-Day-gameplay-fov.txt

Keep Steam running and double-click Launch-E-Day-Gameplay-FOV.bat. Leave its window open until the
game exits. Apply-E-Day-Gameplay-FOV.bat and Restore-E-Day-Gameplay-FOV.bat are optional apply/undo shortcuts.
Use only one of the three camera variants in a game session.

E-Day-gameplay-fov.txt accepts 60 through 150, including decimals, or 0 to follow
the game's original setting. Packaged default: 120. Your installed value is
preserved when updating only the EXE. This adjusts the gameplay settings input;
aiming, cover and special camera behavior continue through the engine's logic.

## Update resilience and limits

E-Day-GameLayout.cs searches the loaded executable for the camera code and native
class registrations. It resolves relative references and the object-array key,
reads class default vtables, and validates object identities and hierarchy data.
It discovers the cinematic fill field and gameplay FOV/target-selector fields
from their instructions, and controller ownership from native property metadata.

The complete required layout is validated before writes. Missing/ambiguous
signatures, invalid call targets, changed slider semantics, invalid field bounds
and incompatible native cinematic structures are rejected. There is no fallback
to the previous build's addresses. A successful build-consent answer does not
skip these checks. An untested build still asks Yes/No before attempting it.

This handles compatible address relocations and the validated field changes in
CL-4909945. It cannot guarantee compatibility with future code, class or camera
structure changes. Such updates can still require a new mod revision. This
release targets Steam; it does not establish compatibility with Game Pass.

The launcher retains its previous local-only environment and Steam-ID handling:
SteamAppId=3010850, EOS_USE_ANTICHEATCLIENTNULL=1, direct GoWEDay-Steam.exe launch,
preservation of existing steam_appid.txt, and cleanup only of its created file
after game exit. No permanent environment settings or executable disk patches.
The existing offline-session/anti-cheat checks remain enabled.

## Undo

Run Restore-E-Day-Gameplay-FOV.bat, or close the game to discard session changes. Normal Steam
launches do not start the helper. After closing, removing the runtime mod files
uninstalls it. Keep the text settings if you plan to install a newer release.

## Validation and source

All included fixtures passed. Layout fixtures include address-relocated images,
old/new manager fields, ambiguous/missing signatures, changed fields/constants,
metadata conflicts and 1000 masked-search comparisons. Camera/gameplay fixtures
execute only against their own test-process data using both member layouts.

The combined helper passed live testing on CL-4909945: the user confirmed 32:9
framing with no zoom flashes at camera changes, and correct gameplay FOV/aiming.
Undo and reapply were checked. The installed framing and FOV settings hashes
were preserved. Other variants share the tested resolver/runtime components
and passed their respective fixtures; they were not separately visually tested.
Full campaign and every camera/resolution remain unverified.

This folder contains the complete source, build instructions and fixtures for
this variant. Compiled executables are excluded from this source repository.
