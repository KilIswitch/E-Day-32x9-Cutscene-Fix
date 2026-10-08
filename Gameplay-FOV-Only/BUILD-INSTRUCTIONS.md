# Build the gameplay-only helper

Use 64-bit Windows with the .NET Framework 4.x C# compiler at
`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
No game files, package manager, downloaded libraries, or administrator privileges
are needed for compilation.

Extract the source package into a writable folder. Double-click `Build.bat`, or
run `Build.bat --no-pause` in Command Prompt there. Output:
`Build\E-Day-Gameplay-FOV.exe`. Building does not launch the game or helper.

Equivalent command from the source folder:

```bat
if not exist Build mkdir Build
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /out:"Build\E-Day-Gameplay-FOV.exe" "E-Day-Gameplay-FOV.cs" "E-Day-GameplayRuntime.cs" "E-Day-GameplayFov.cs"
```

Copy the built executable, `Launch-E-Day-Gameplay-FOV.bat`, and
`E-Day-gameplay-fov.txt` beside `GoWEDay-Steam.exe` for runtime use.

## Source map

- `E-Day-Gameplay-FOV.cs`: entry point, process checks, different-build consent,
  read-only compatibility validation, and initialization retrying.
- `E-Day-GameplayRuntime.cs`: memory access, object identities, camera manager
  discovery, background watcher, session guard, and stop/restore signaling.
- `E-Day-GameplayFov.cs`: independent configuration and ownership tracking;
  adjusts/restores only gameplay multiplier offset `+0x5ea8`, with gameplay
  target validation and exclusion during a valid cinematic target.
- Batch files: readable source for launch, optional apply, and optional restore.
- `GameplayFovTests.cs`: synthetic fixtures, excluded from the production build.

This version does not compile or depend on the cinematic framing/template code.
All dependencies are framework assemblies or Windows kernel32 APIs. There are
no external DLLs, NuGet packages, obfuscation, generated-source prerequisites,
embedded payloads, or post-build steps. Independently compiled binaries can
have different hashes due to compiler version, timestamps, and module metadata.

## Optional fixture tests

After building, run these commands from the source folder:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /main:GameplayFovTests /out:"Build\GameplayFovTests.exe" "E-Day-Gameplay-FOV.cs" "E-Day-GameplayRuntime.cs" "E-Day-GameplayFov.cs" "GameplayFovTests.cs"
"Build\GameplayFovTests.exe"
```

These tests allocate their own memory and do not touch a running game. Keep the
test executable in `Build` as shown: it writes its temporary configuration in
its own directory. Do not run it beside the game's gameplay configuration.
