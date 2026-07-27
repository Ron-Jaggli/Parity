# Parity

A BONELAB code mod that makes the game run better without changing how it looks.

Most VR performance mods buy frames by lowering render scale, pulling in shadow
distance, dropping LOD bias or cutting texture quality. Those work, and you can
see every one of them. Parity takes the opposite constraint: **if a change could
alter a pixel, a sound, or a gameplay outcome, it either does not ship or it
ships switched off with an explanation.**

What is left is the work the game does that never reaches your eyes — shading
pixels the headset lens physically cannot show you, animating skeletons nobody
is looking at, building stack traces for log lines nobody reads, allocating
collision objects that are discarded on the next line.

---

## Install

1. Install [MelonLoader](https://melonwiki.xyz/) 0.6.x into BONELAB.
2. Drop `Parity.dll` into `BONELAB/Mods/`.
3. Launch once. Settings appear in `BONELAB/UserData/MelonPreferences.cfg`.

Every setting can be edited while the game is running — changes are picked up
within about two seconds, no restart needed.

---

## What it does

### On by default

These are the ones that are safe to just leave alone.

| Optimisation | What it does | Why it is invisible |
|---|---|---|
| **VR occlusion mesh** | Enables the headset's stencil mask so the GPU stops shading the corners of each eye texture. | The masked pixels sit outside the lens area. No human has ever seen them. Typically ~10–17% of fragment work depending on headset. |
| **Log stack trace stripping** | Stops Unity capturing a managed stack trace for every `Debug.Log` and `Debug.LogWarning`. | A stack walk plus string building, per call, on the calling thread — and it draws nothing. Errors, asserts and exceptions keep their traces, so crash logs stay useful. |
| **Collision callback reuse** | Reuses one `Collision` object across callbacks instead of allocating a fresh one per contact. | Same collisions, same physics. Removes a major source of garbage in a game built on physics, which means fewer GC pauses. |
| **Offscreen animator culling** | Switches animators to `CullUpdateTransforms` so offscreen skeletons skip transform writes, IK and retargeting. | State machines still tick and animation events still fire, so scripted behaviour is unaffected. Only work on invisible skeletons is skipped, and Unity resumes on the frame the object becomes visible. |
| **Delta time clamp** | Caps how much simulation one frame may catch up on, to 3 fixed timesteps. | Prevents a stutter feedback loop (see below). Nothing renders differently; the world just declines to fast-forward through a stall. |
| **Async upload buffer** | Grows Unity's texture/mesh upload ring buffer from 4 MB to 16 MB and keeps it resident. | Same assets at the same quality, moved to the GPU in fewer steps. Fewer hitches walking into a new area. |
| **GC and asset unload on scene load** | Forces a collection and releases unreferenced assets while the loading screen is up. | Never runs during play. The point is *when* the pause lands, not whether it happens. |
| **Frame time telemetry** | Logs frame time percentiles to the MelonLoader console every 2 minutes. | Console only. Deliberately not an on-screen counter — that would break the premise and cost frames of its own. |

#### On the delta time clamp

This is the least obvious one, so it is worth spelling out. Unity ships
`Time.maximumDeltaTime` at 1/3 s. After a 200 ms hitch, the next frame runs
every fixed timestep it missed — a dozen full physics steps crammed into one
frame. That frame is therefore also long, which queues more steps, and the
stutter sustains itself. In VR this is the difference between one dropped frame
and a second of judder. Clamping the ceiling means the simulation briefly runs
slower than real time instead of trying to make it all up at once.

### Off by default

These ship disabled because each one can change something you would notice.
They are here because they are real wins if they happen to be safe in your
setup — try them one at a time.

| Setting | Possible win | What could go wrong |
|---|---|---|
| `DisableAutoSyncTransforms` | Meaningful, in scenes with many raycasts. | Code that moves a transform and immediately raycasts against it can observe a stale position. |
| `AggressiveAnimatorCulling` | Larger than the default culling mode. | Uses `CullCompletely`, so animation events stop firing while offscreen. That can stall scripted behaviour. |
| `DisableClothInterCollision` | Cheap, where cloth inter-collision is unused. | If any garment relies on it, you will see cloth pass through cloth. This is the one setting here that is openly visual. |
| `RemoveDuplicateAudioListeners` | Small. | If the mod keeps the wrong listener the game goes quiet. It refuses to act unless it can positively identify which listener to keep, but the risk is not zero. |
| `DisableUnityLogger` | Slightly more than stack trace stripping alone. | Hides genuine errors from you and from other mods. |
| `TuneIncrementalGc` | Situational. | Only does anything if the game shipped with incremental GC enabled, and the shipped value is usually already sensible. |

### Explicitly not done

Named here so it is clear these were decisions, not oversights:

- **Render scale / `XRSettings.eyeTextureResolutionScale`** — the biggest single
  lever in VR, and immediately visible. That is the entire thing this mod is
  refusing to do.
- **Shadow distance, LOD bias, texture quality, mip streaming** — all visible.
- **Fixed timestep / solver iterations** — changes how physics behaves, which in
  a physics sandbox is the game.
- **Particle system culling** — Unity's automatic mode changes how looping
  effects look when they re-enter view.
- **`asyncUploadTimeSlice`** — trades main-thread time per frame for faster
  uploads. Not obviously a win.

---

## Measuring it

Parity writes a line like this to the MelonLoader console every two minutes:

```
[Parity] Frame time over 120 s: 7204 frames, median 11.2 ms, p95 13.8 ms, p99 16.5 ms, worst 42.1 ms.
```

Percentiles, not averages: in VR it is the worst 1% of frames you actually
notice. To get a fair before/after, set `Enabled = false` and play the same area
— telemetry keeps running while the mod is switched off, so both numbers come
from the same measurement.

Frame timing resets on every scene load, so a report describes steady-state play
rather than the loading hitch.

---

## Building

The mod references assemblies from your BONELAB install, which are copyrighted
and therefore not vendored in this repository.

```bash
dotnet build src/Parity.csproj -c Release
```

If BONELAB is not at the default Steam path:

```bash
dotnet build src/Parity.csproj -c Release -p:BonelabDir="D:\Games\BONELAB"
```

Alternatively, copy these into a top-level `Libs/` folder, which takes
precedence and is gitignored:

- From `BONELAB/MelonLoader/net6/`: `MelonLoader.dll`, `Il2CppInterop.Runtime.dll`
- From `BONELAB/MelonLoader/Il2CppAssemblies/`: `Il2Cppmscorlib.dll`,
  `UnityEngine.CoreModule.dll`, `UnityEngine.PhysicsModule.dll`,
  `UnityEngine.AnimationModule.dll`, `UnityEngine.AudioModule.dll`,
  `UnityEngine.VRModule.dll`

Output lands in `src/bin/Release/Parity.dll`.

---

## How the code is organised

```
src/
  ParityMod.cs              MelonMod entry point; owns the tweak list and the update loop
  ParityPreferences.cs      Every setting, with the reasoning in its description text
  ParityLog.cs              Console output and the exception guard every tweak runs behind
  Tweak.cs                  Base class: apply, revert, per-scene, per-frame
  FrameTimeMonitor.cs       Allocation-free frame time histogram
  Tweaks/                   One file per optimisation
```

Three conventions worth knowing if you extend this:

- **Every tweak is reversible.** `Apply()` reads its own preference and either
  applies the optimisation or restores the value it found. That is what makes
  toggling a setting take effect without a restart, and what makes the master
  switch meaningful.
- **Compare before writing.** `Apply()` re-runs every two seconds. Some engine
  setters do real work even when handed the value they already hold —
  assigning `asyncUploadBufferSize` reallocates the buffer.
- **Nothing unbounded on the hot path.** Scene sweeps are budgeted per frame,
  telemetry uses a fixed histogram, and hot loops use hand-written `try`/`catch`
  rather than `ParityLog.Try`, because a capturing lambda allocates on every
  call.

---

## Status

Written against MelonLoader 0.6.x and Unity 2021.3 APIs. **It has not been
compiled or run against a real BONELAB install** — see the build instructions
above. Treat the first launch as the real test, and check the MelonLoader
console: any engine call that does not exist in your build is caught, reported
once by name, and skipped, so a mismatch degrades a single tweak rather than
taking down the mod.
