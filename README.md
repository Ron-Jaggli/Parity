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

Built for **BONELAB on Quest**, standalone.

## Install

Quest needs **[LemonLoader](https://github.com/LemonLoader/LemonLoader)**, not
MelonLoader. MelonLoader is PC-only; LemonLoader is its Android port and runs
the same net6 mods, which is why this one DLL works on a headset at all.

1. Install LemonLoader and patch BONELAB with it, following its own
   instructions. This part is not specific to this mod — if BONELAB does not
   boot with LemonLoader installed and no mods, fix that before adding Parity.
2. Copy `Parity.dll` into the `Mods/` folder LemonLoader created in BONELAB's
   data directory on the headset. Typically that is under
   `Android/data/com.StressLevelZero.BONELAB/files/`, reachable over USB, `adb
   push`, or a file manager on the headset — the exact path depends on your
   LemonLoader version, so trust what LemonLoader created over what is written
   here.
3. Launch once. `UserData/MelonPreferences.cfg` appears next to `Mods/`.

Every setting can be edited between launches by pulling that file, editing it,
and pushing it back. On PC the mod picks up edits within about two seconds
without a restart; on a headset you will not usually be editing it live, but the
same mechanism means a changed file takes effect on the next launch with no
further steps.

### Reading the log

There is no console on a headset. MelonLoader writes to `MelonLoader/Logs/`
alongside `Mods/`, with the most recent run in `Latest.log`. That is where
Parity's startup summary, its frame time reports, and any "this call is not
available in your build" warnings go. Pull that file when you want to know what
the mod actually did.

---

## What it does

### On by default

These are the ones that are safe to just leave alone.

| Optimisation | What it does | Why it is invisible |
|---|---|---|
| **VR occlusion mesh** | Asks Unity to enable the headset's stencil mask so the GPU stops shading the corners of each eye texture. | The masked pixels sit outside the lens area. No human has ever seen them. See the caveat below. |
| **Log stack trace stripping** | Stops Unity capturing a managed stack trace for every `Debug.Log` and `Debug.LogWarning`. | A stack walk plus string building, per call, on the calling thread — and it draws nothing. Errors, asserts and exceptions keep their traces, so crash logs stay useful. |
| **Collision callback reuse** | Reuses one `Collision` object across callbacks instead of allocating a fresh one per contact. | Same collisions, same physics. Removes a major source of garbage in a game built on physics, which means fewer GC pauses. |
| **Offscreen animator culling** | Switches animators to `CullUpdateTransforms` so offscreen skeletons skip transform writes, IK and retargeting. | State machines still tick and animation events still fire, so scripted behaviour is unaffected. Only work on invisible skeletons is skipped, and Unity resumes on the frame the object becomes visible. |
| **Delta time clamp** | Caps how much simulation one frame may catch up on, to 3 fixed timesteps. | Prevents a stutter feedback loop (see below). Nothing renders differently; the world just declines to fast-forward through a stall. |
| **Async upload buffer** | Grows Unity's texture/mesh upload ring buffer from 4 MB to 8 MB and keeps it resident. | Same assets at the same quality, moved to the GPU in fewer steps. Fewer hitches walking into a new area. |
| **GC and asset unload on scene load** | Forces a collection and releases unreferenced assets while the loading screen is up. | Never runs during play. The point is *when* the pause lands, not whether it happens. |
| **Frame time telemetry** | Logs frame time percentiles to MelonLoader's log file every 2 minutes. | Log file only. Deliberately not an on-screen counter — that would break the premise and cost frames of its own. |

#### On the occlusion mesh

This is the one entry above whose payoff is least certain on Quest. On PC VR it
is worth roughly 10–17% of fragment work. On Quest the stencil mask is largely
the runtime's business, so `XRSettings.useOcclusionMesh` may already be on, or
may do nothing at all. Parity treats it accordingly: it never turns the setting
*off* if the game had it on, and it logs `VR occlusion mesh was already enabled
by the game - nothing to gain here` when there is nothing to do. Check the log
before counting on this one.

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
- **Fixed foveated rendering** — the biggest single lever on Quest, and the
  omission most likely to look like an oversight. Two reasons. Above the lowest
  level you can see it in your periphery, which is exactly what this mod refuses
  to trade. And foveation is the kind of setting the game drives itself, often
  varying it with load — a mod re-asserting a level on a timer would fight that
  and could make the foveation level visibly pulse. If BONELAB exposes a
  foveation option, use that one.

---

## Measuring it

Parity writes a line like this to `MelonLoader/Logs/Latest.log` every two
minutes:

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

### On GitHub (no PC needed)

Every push builds on GitHub Actions and uploads `Parity.dll` as an artifact —
open the run under the **Actions** tab and download it from the Artifacts
section at the bottom. You can also start a build manually from that tab
(**Build** → **Run workflow**), which lets you pick a different MelonLoader
release tag if the pinned one goes stale.

CI cannot use the real IL2CPP game assemblies — MelonLoader generates those from
your own copy of BONELAB and they are not redistributable. It downloads the real
`MelonLoader.dll` and `Il2CppInterop.Runtime.dll` from MelonLoader's releases,
and compiles the Unity surface against the reference assemblies in
[`refs/`](refs/README.md), which carry the real assembly names and signatures so
the emitted IL binds correctly at runtime.

Because that binding is the load-bearing assumption, the workflow checks it
rather than trusting it: after building, it reads the assembly references out of
`Parity.dll` and fails if any expected name is missing, and it fails if anything
other than `Parity.dll` ends up in the output folder.

### Locally

```bash
./scripts/build.sh
```

That fetches MelonLoader on first run, caches it in `.melonloader/`, builds, and
prints the resulting assembly references. Output lands in
`src/bin/Release/Parity.dll`. It needs `dotnet` and `python3` and nothing else —
no `jq`, no `unzip`.

#### Getting the .NET SDK on an immutable distro

On Bazzite, Silverblue, Steam Deck and friends, do **not** reach for
`rpm-ostree install` — layering a package onto the base image for one build is a
reboot and a permanent tax. Install the SDK into your home directory instead:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"        # add both to ~/.bashrc to persist
```

The mod targets `net6.0`, but a newer SDK builds it fine — CI uses .NET 10. If
you would rather keep it out of `$HOME` entirely, Bazzite ships `distrobox`, and
a throwaway Fedora container with `dotnet-sdk-8.0` inside works just as well.

### Building against your own headset's assemblies

This is worth doing once, and it is the one thing that closes the gap described
under [Status](#status).

When LemonLoader first patches BONELAB, it generates IL2CPP interop assemblies
from *your* copy of the game. Those are the assemblies the mod will actually
bind against at runtime. Build against them and every Unity call is checked for
real, rather than against the hand-written shapes in `refs/`:

```bash
./scripts/pull-il2cpp.sh          # finds them over adb and pulls to ./bonelab-asm
IL2CPP_DIR=./bonelab-asm ./scripts/build.sh
```

The path differs between LemonLoader versions, so the script searches for the
folder containing `UnityEngine.CoreModule.dll` rather than assuming one.

**These assemblies only exist after LemonLoader has patched BONELAB *and* the
patched game has been launched at least once** — that first launch is what
generates them. Before that, there is nothing to pull.

If the build succeeds, every member this mod touches exists in your build of the
game, and the only thing left to find out is whether the optimisations help.

If it fails, that is a genuinely useful result: it names exactly which call
differs on Quest, and it is a much better way to learn that than a line in
`Latest.log` after the fact.

Never commit those files — they are derived from the game. `Libs/` and `*.dll`
are gitignored for that reason.

### Building against a PC install

If you also have BONELAB on a PC, the real assemblies are picked up
automatically:

```bash
dotnet build src/Parity.csproj -c Release -p:BonelabDir="/path/to/BONELAB"
```

The build prints which reference source it used.

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

Written against MelonLoader 0.6.x and Unity 2021.3.

**What is verified:** CI compiles the mod on every push, against the real
MelonLoader and Il2CppInterop assemblies, and checks that the output binds to
the game's assemblies by the right names.

**What is not:** it has not been run in BONELAB, and the Unity side is checked
against reference assemblies rather than the interop assemblies IL2CPP produced
for your build of the game. If a member differs there, it shows up at runtime
rather than at compile time.

That gap is handled by design rather than by hope: every engine call runs behind
an exception guard that reports a failing call site once by name and then skips
it. A mismatch costs you one optimisation and a line in the log, not the mod. So
treat the first launch as the real test — and read `Latest.log`.
