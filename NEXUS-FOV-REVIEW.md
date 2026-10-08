# Nexus review: current cinematic and gameplay versions

The root and combined cinematic variants now use synchronous view correction.
The gameplay-only variant is unchanged. This page identifies the source and
binaries for review; it does not establish submission approval.

| Variant | Full source | Build instructions | Output |
| --- | --- | --- | --- |
| Cutscenes only | [Root](./) | [Build](BUILD-INSTRUCTIONS.md) | Build/E-Day-32x9.exe |
| Cutscenes + gameplay FOV | [Gameplay-FOV](Gameplay-FOV/) | [Build](Gameplay-FOV/BUILD-INSTRUCTIONS.md) | Build/E-Day-32x9-FOV.exe |
| Gameplay FOV only | [Gameplay-FOV-Only](Gameplay-FOV-Only/) | [Build](Gameplay-FOV-Only/BUILD-INSTRUCTIONS.md) | Build/E-Day-Gameplay-FOV.exe |

Download/clone the source, open the chosen folder and run Build.bat, or
Build.bat --no-pause. The Windows Framework64 v4.0.30319 csc.exe compiler is used.
All source, launch scripts, settings and build instructions are included. No
game files, SDK, assembler or downloaded library is needed to compile.

## Distributed executable SHA-256

| Executable | SHA-256 |
| --- | --- |
| E-Day-32x9.exe | `12a684543ca83e1cc8f18d20896b23ce47f423f6cf4517d203b2a435ebc95ed1` |
| E-Day-32x9-FOV.exe | `45518f1f136f55d32b7c8a51be7a1745d442835774e1b020f3c17b1b95250fc5` |
| E-Day-Gameplay-FOV.exe | `00c5bf5d7659dfabce6b55a3ef79aaebacf5fab911977469f83c518624c73e1b` |

Each source location has its own DISTRIBUTED-BINARY-SHA256.txt and SHA256SUMS.txt.
Executables are excluded from GitHub. Compiler version and generated PE/module
metadata can produce different rebuild hashes; byte-identical builds are not
claimed. Compare the source, build inputs, managed code and native wrapper.

## Current change and validation

The cinematic helpers correct overscan before the original view calculation,
preserving 3.50 default framing and lens-aware wide-lens limiting. Serialized
camera assets are no longer prepared. The wrapper's readable assembly and
disassembly are included. One vtable data pointer is replaced temporarily and
own executable memory allocated; game instruction bytes and disk files remain
unchanged. See [review notes](REVIEW-NOTES.md) for lifetime and restoration.

Run Test.bat in the root or Gameplay-FOV folder. The cutscene-only build passed
31 frame-wrapper fixtures and 11 lens checks. The combined build passed those
42 checks plus 16 gameplay fixtures. Fixtures modify only their test process.
Live prototype tracing covered eight cameras, five focal lengths and 19,575
corrected calls, with no table overflow, read failure or recorded framing math
mismatch. The user confirmed no flashes and correct framing in two cutscenes.
The final asset-template exclusion additionally has fixture coverage.

See [root validation](VALIDATION.txt), [combined validation](Gameplay-FOV/VALIDATION.txt),
and [unchanged gameplay-only validation](Gameplay-FOV-Only/VALIDATION.txt).
The tested Steam build is +++fenix2+fairlight-omega-release-CL-4894958 at 5120x1440.
The entire campaign, other resolutions and future builds remain unverified.
The existing offline session checks, launch-local environment, optional build
consent and signature requirements remain. Publication does not confer
anti-cheat approval or Nexus approval.
