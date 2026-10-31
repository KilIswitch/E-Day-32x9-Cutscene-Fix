# CL-4927986 startup and memory review

All variants include E-Day-GameLayout.cs. It opens no additional process handle;
it reads the validated target through the caller's existing handle, captures
readable image regions and resolves/validates native classes, object-array
decoding, code signatures, field bounds, relative calls and metadata. It does
not write game data. A complete candidate is published only after validation.
The entry point checks the required original view dispatch before any cap write.

Cinematic production: E-Day-32x9.cs, E-Day-CameraFraming.cs,
E-Day-CameraFrameGate.cs and E-Day-GameLayout.cs. The combined folder adds
E-Day-GameplayFov.cs. Gameplay-only uses E-Day-Gameplay-FOV.cs,
E-Day-GameplayRuntime.cs, E-Day-GameplayFov.cs and E-Day-GameLayout.cs.

Cinematic mode changes the existing four-byte cinematic limit and temporarily
replaces one eight-byte vtable data pointer. Its separately allocated executable
wrapper adjusts overscan/two camera flags before tail-calling the original view
function. Readable assembly/disassembly accompany the embedded wrapper. Its two
pointer relocations plus cinematic fill-field displacement are checked before
use. The game instruction bytes and executable disk file are not patched.
This does allocate/inject the helper's own executable code into the process;
it is not a data-only change. Dispatch replacement briefly parks/resumes the
process. Disabled owned allocations remain until game exit to protect code
lifetime. Restore verifies identities/ownership and restores original dispatch.

Gameplay FOV changes the validated settings multiplier, preserving original
values and checking target type/object identity. It restores its owned input
when disabled, on cinematic targets, and on helper restoration. It does not
patch final-view caches or cinematic focal lengths.

Launch scripts retain local Steam/EOS environment handling, Steam process/ID
checks, direct launch, exit waiting and owned-ID cleanup. Protected-session
checks remain. There are no network calls, downloaded payloads, packing,
obfuscation, service installs or permanent environment changes.

All three sources and tests are included. See NEXUS-FOV-REVIEW.md for build
instructions, binary hashes, test counts and explicit validation limits.

## Current startup policy change

Missing native class registration now throws GameLayout.InitializingException
instead of IncompatibleException. UntilReady retries this condition until the
requested wait ends or the game exits; the normal launcher requests forever.
The resolver publishes only a fully validated candidate. Other incompatibility
conditions remain fatal. The discovery banner is announced once per search,
and initialization-reason changes are reported by the readiness loop.

All three entry points automatically announce an untested build and proceed to
mandatory validation without Console.ReadLine or approval-token arguments.
BuildPolicyTests replaces the removed BuildPromptTests. The framing wrapper,
camera writes, gameplay behavior and restoration/lifetime policy are unchanged.
