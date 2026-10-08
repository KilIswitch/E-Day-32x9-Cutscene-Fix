# Synchronous camera correction review notes

Production cinematic code: E-Day-32x9.cs (launch checks, cap, entry point),
E-Day-CameraFraming.cs (session guard, identities, configuration, lifecycle),
E-Day-CameraFrameGate.cs (validated virtual dispatch pointer, owned allocation,
embedded wrapper and restoration). camera-frame-gate.asm and its disassembly
document all native wrapper bytes and the two pointer relocations. The combined
folder additionally includes E-Day-GameplayFov.cs; gameplay-only is unchanged.

This replaces periodic camera/template adjustment with correction before each
compatible GetCameraView call. One eight-byte vtable data pointer is temporarily
changed in memory. The wrapper lives in separately allocated memory, becomes
read/execute after writing, adjusts overscan/flags, and tail-calls the original
method with its arguments and return path intact. Installation/restoration
briefly parks the game around the dispatch pointer write. The game's executable
instructions and disk game files are not patched. This does inject the helper's
own executable code into the running process, rather than only changing data.

Serialized MovieScene camera templates are excluded. Native object identities,
manager/camera vtables, signatures, numeric bounds and restoration ownership are
checked. Owned allocations remain disabled until game exit after restoration to
avoid freeing code while a thread could still be leaving it. No network calls,
remote payloads, dependency downloads, packing or obfuscation are included.

The launch-local Steam/EOS settings and existing protected-session checks remain.
Build/test scripts require no game and tests modify only their own process.
The whole campaign and future layouts remain unverified. See VALIDATION.txt,
BUILD-INSTRUCTIONS.md and NEXUS-FOV-REVIEW.md for evidence and binary identities.
