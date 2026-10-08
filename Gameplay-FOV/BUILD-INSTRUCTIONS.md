# Build the gameplay FOV variant

Use 64-bit Windows with the .NET Framework 4.x C# compiler available at
`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

Double-click Build.bat in the Gameplay-FOV folder of the downloaded repository, or open Command Prompt
in that folder and run `Build.bat --no-pause`. It builds
`Build\E-Day-32x9-FOV.exe`. The script does not launch the game or helper.
Copy the resulting executable beside the runtime files and GoWEDay-Steam.exe
before applying it.

Equivalent command from the Gameplay-FOV folder:

```bat
if not exist Build mkdir Build
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /out:"Build\E-Day-32x9-FOV.exe" "E-Day-32x9.cs" "E-Day-CameraFraming.cs" "E-Day-CameraSources.cs" "E-Day-GameplayFov.cs"
```

All four C# files are required. There are no NuGet packages, third-party DLLs,
downloaded dependencies, generated source files, or post-build steps. The batch
launchers are readable source. Independent builds may have different binary
hashes because of compiler version, timestamps, and module metadata.

E-Day-GameplayFov.cs validates the gameplay FOV setter/getter signatures and
adjusts the gameplay camera manager's settings multiplier. It checks object
serials, the manager vtable, live gameplay targets, absence of cinematic targets,
and expected original values. It reads the independent FOV text file and
restores owned values when disabled, suspended for cinematics, or stopped.

The other three files retain cinematic framing, camera-template preparation,
build prompts, and process validation. The watcher starts the current variant
executable rather than the original helper. Both versions are available in the GitHub source repository. Uploading source
does not mean the executable has been approved by Nexus.

## Optional fixture tests

From Command Prompt in Gameplay-FOV, after building the helper:

```bat
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /main:GameplayFovTests /out:"Build\GameplayFovTests.exe" "E-Day-32x9.cs" "E-Day-CameraFraming.cs" "E-Day-CameraSources.cs" "E-Day-GameplayFov.cs" "GameplayFovTests.cs"
"Build\GameplayFovTests.exe"
```

The fixtures allocate their own process memory and do not modify a running game.
Run them in the Build folder as shown so their temporary configuration does not
replace your gameplay configuration beside the game.
