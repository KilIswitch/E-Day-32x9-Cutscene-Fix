# Nexus review: CL-4927986 startup fix and automatic build validation

Updated October 30, 2026. All three Steam variants target the tested build
+++fenix2+fairlight-omega-release-CL-4927986 (module size 323710976).
This guide identifies source and build evidence; it does not grant submission
or anti-cheat approval. Game Pass remains unverified.

| Variant | Complete source | Build instructions | Output |
| --- | --- | --- | --- |
| Cutscenes only | [Root](./) | [Build](BUILD-INSTRUCTIONS.md) | Build/E-Day-32x9.exe |
| Cutscenes + gameplay FOV | [Gameplay-FOV](Gameplay-FOV/) | [Build](Gameplay-FOV/BUILD-INSTRUCTIONS.md) | Build/E-Day-32x9-FOV.exe |
| Gameplay FOV only | [Gameplay-FOV-Only](Gameplay-FOV-Only/) | [Build](Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md) | Build/E-Day-Gameplay-FOV.exe |

## Changes under review

The previous resolver could see some native class registration code before the
required TCPlayerCameraManager_CamBridge registration became visible, causing
an intermittent fatal compatibility error. A missing required registration is
now an initialization condition. The helper retries without publishing a layout
or making game changes until the complete candidate passes validation.
It reports initialization-reason changes without repeating the discovery banner.

The Yes/No build-number question and watcher consent-token plumbing are removed.
Different build numbers automatically attempt required discovery/validation.
Signature, field, numeric-bound, native-identity and protected-session checks
remain mandatory. Contradictory or incompatible view/field signatures still stop
the helper. Missing class registration keeps initialization waiting until ready
or game exit. No old-address fallback or forced compatibility is introduced.

Cinematic framing, native wrapper bytes/relocations, gameplay FOV policy,
restoration ownership, launch-local environment and Steam-ID handling remain
unchanged from the prior revision. See [review notes](REVIEW-NOTES.md).

## Build and test

Download/clone the source, open the chosen variant folder, and run Build.bat or
`Build.bat --no-pause`. It uses the Windows Framework64 v4.0.30319 csc.exe,
/platform:x64 /target:exe /optimize+, and all E-Day-*.cs production files.
No SDK, game, assembler or downloaded dependency is required. Test.bat runs the
owned-process fixtures. BuildPolicyTests.cs replaces the obsolete prompt tests.

Cutscenes-only: 111 checks. Combined: 143 checks. Gameplay-only: 80 checks.
Each includes 36 layout checks, staged registration/retry coverage, rejection
of real signature mismatches, and 1000 masked-search comparisons. Build-policy
tests verify automatic validation without input and one announcement per build.

## Distributed executable SHA-256

| Executable | SHA-256 |
| --- | --- |
| E-Day-32x9.exe | `0a291a748b7a13445686d2ad331038aba7ecac3e06f0a739ba8006dab15dc5be` |
| E-Day-32x9-FOV.exe | `657608ca1346edaa6adf219bf2dcc168e4ec939360ca1b6ad58bc4152fe3bc78` |
| E-Day-Gameplay-FOV.exe | `34eb1e8648b237c4982070a39804f4ffe77ccb111b372ca1cd7337b4dd071266` |

Each folder contains DISTRIBUTED-BINARY-SHA256.txt and SHA256SUMS.txt. The
repository contains full source rather than compiled binaries. Compiler/PE
metadata may produce different rebuild hashes; byte-identical builds are not
claimed.

## Live evidence and limits

The old resolver reported the missing manager registration at 6.80 seconds;
the same fresh launch passed every layout check at 28.18 seconds when the
read-only diagnostic continued. The corrected resolver passed three fresh-start
checks at approximately 27-28 seconds with no incompatible results.

The installed combined launcher also encountered the previously failing stage,
waited, validated the complete layout, applied the cinematic limit and started
its background helper without a question. A subsequent check confirmed it active.
Existing framing/FOV text settings were preserved and the installed executable
matched the packaged hash. No new visual framing test was performed for this
startup-only change. Other variants share the corrected components and passed
their own fixtures; they were not separately live-launched.

Full campaign, every camera/resolution, Game Pass and future structural updates
remain unverified. See [root validation](VALIDATION.txt),
[combined validation](Gameplay-FOV/VALIDATION.txt), and
[gameplay-only validation](Gameplay-FOV-Only/VALIDATION.txt).
