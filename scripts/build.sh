#!/usr/bin/env bash
#
# Build Parity.dll locally. Fetches MelonLoader on first run and caches it.
#
#   ./scripts/build.sh
#
# By default this compiles against the reference assemblies in refs/, which is
# what you want when the game is not installed on this machine - see
# refs/README.md for what that does and does not prove.
#
# If you have pulled the real IL2CPP assemblies off your headset (see the README
# section "Building against your own headset's assemblies"), point at them and
# the build uses those instead, which removes the guesswork entirely:
#
#   IL2CPP_DIR=~/bonelab-asm ./scripts/build.sh
#
# Environment:
#   IL2CPP_DIR        directory of the game's Il2Cpp assemblies (optional)
#   MELONLOADER_TAG   MelonLoader release to build against (default v0.6.6)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

tag="${MELONLOADER_TAG:-v0.6.6}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: 'dotnet' is not on PATH." >&2
  echo "       See the README for installing the SDK without touching an" >&2
  echo "       immutable base system (Bazzite, Silverblue, Steam Deck)." >&2
  exit 1
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "error: 'python3' is required to fetch MelonLoader." >&2
  exit 1
fi

melon_dir="$(python3 scripts/fetch_melonloader.py --tag "$tag" --dest .melonloader)"

args=(
  build src/Parity.csproj
  --configuration Release
  "-p:MelonDir=$melon_dir"
)

if [ -n "${IL2CPP_DIR:-}" ]; then
  if [ ! -f "$IL2CPP_DIR/UnityEngine.CoreModule.dll" ]; then
    echo "error: IL2CPP_DIR is set to '$IL2CPP_DIR' but there is no" >&2
    echo "       UnityEngine.CoreModule.dll there." >&2
    exit 1
  fi
  echo "Building against the game's own assemblies in $IL2CPP_DIR"
  args+=("-p:Il2CppDir=$IL2CPP_DIR" "-p:UseReferenceAssemblies=false")
else
  echo "Building against the reference assemblies in refs/"
  args+=("-p:UseReferenceAssemblies=true")
fi

dotnet "${args[@]}"

output="$repo_root/src/bin/Release/Parity.dll"
echo
echo "Built $output"
echo
echo "Assembly references:"
dotnet run --project tools/DumpRefs --configuration Release --verbosity quiet --nologo -- "$output" \
  MelonLoader,Il2CppInterop.Runtime,Il2Cppmscorlib,UnityEngine.CoreModule,UnityEngine.PhysicsModule,UnityEngine.AnimationModule,UnityEngine.AudioModule,UnityEngine.VRModule
