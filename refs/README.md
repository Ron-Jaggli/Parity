# Reference assemblies

These projects exist so the mod can be **compiled without a BONELAB install** —
on a CI runner, or on a machine that does not have the game.

They are not stubs in the throwaway sense. Each one is built with the assembly
name of a real game assembly (`UnityEngine.CoreModule`, `UnityEngine.PhysicsModule`,
and so on) and declares the exact members the mod calls, with the exact
signatures. A .NET assembly reference records the assembly's *name* and the
member's *signature*, so IL compiled against these resolves against the real
assemblies at runtime. This is the same mechanism Unity's own reference
assemblies use.

Nothing in here ends up in `Parity.dll`, and none of these DLLs are shipped.

## Why not just download the real ones

`MelonLoader.dll` and `Il2CppInterop.Runtime.dll` *are* fetched for real — they
are Apache-2.0 and published on GitHub Releases, so CI downloads them and the
mod's use of the MelonLoader API is genuinely type-checked.

The Il2Cpp assemblies here are different. MelonLoader generates them from your
own copy of BONELAB the first time it runs. They are derived from copyrighted
game code, they differ between game versions, and they cannot be redistributed.
Hence hand-written references.

## What that does and does not prove

A green build proves the mod is internally consistent and that every Unity
member it calls matches the signature documented for Unity 2021.3. It does not
prove those members are present under the same names in the interop assemblies
IL2CPP produced for your build of the game.

That residual risk is handled at runtime rather than at compile time: every
engine call in the mod runs behind an exception guard that reports a failing
call site once by name and then skips it, so a mismatch costs you one
optimisation rather than the mod.

## Keeping these honest

Two rules when editing:

- **Signatures must match the real API exactly** — name, parameter types,
  return type, static-vs-instance. A mismatch turns into a runtime
  `MissingMethodException` instead of a compile error, which is the failure mode
  these files exist to prevent.
- **Enum values must be explicit and correct.** Enum constants are baked into
  the IL as integers, so `AnimatorCullingMode.CullUpdateTransforms` compiling to
  the wrong number would silently do the wrong thing with no error anywhere.

Add a member here only when the mod actually calls it.

## `Il2CppObjectBase`

`UnityEngine.Object` here derives from `Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase`,
and every Unity type below it carries the `(IntPtr pointer)` constructor that
implies. That is not decoration — it mirrors what IL2CPP interop assemblies
actually look like, and it is load-bearing: `Il2CppReferenceArray<T>` constrains
`T` to `Il2CppObjectBase`, so without it `Object.FindObjectsOfType<T>()` cannot
be declared at all.

Base classes never appear in method signatures, so this affects only whether
these projects compile — not the IL emitted into `Parity.dll`. Any new Unity
type added here needs to join the same chain.
