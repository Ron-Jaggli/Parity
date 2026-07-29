#!/usr/bin/env bash
#
# Pull the IL2CPP interop assemblies LemonLoader generated from your copy of
# BONELAB, so the mod can be compiled against the assemblies it will actually
# bind to at runtime rather than against the reference shapes in refs/.
#
#   ./scripts/pull-il2cpp.sh                 # pulls to ./bonelab-asm
#   ./scripts/pull-il2cpp.sh ~/somewhere     # or wherever you like
#
# Then:
#
#   IL2CPP_DIR=./bonelab-asm ./scripts/build.sh
#
# These files are derived from the game. Do not commit or redistribute them;
# the repository gitignores *.dll for exactly this reason.
#
# Prerequisite: LemonLoader must have patched BONELAB *and* the patched game
# must have been launched at least once. The assemblies do not exist until that
# first launch generates them.
set -euo pipefail

dest="${1:-./bonelab-asm}"
pkg="com.StressLevelZero.BONELAB"
marker="UnityEngine.CoreModule.dll"

# LemonLoader keeps its data in a top-level /sdcard/MelonLoader/<package>
# directory rather than under Android/data, which is what lets it stay readable
# under scoped storage. The Android/data path is checked too in case a different
# loader version uses it.
ROOTS=(
  "/sdcard/MelonLoader/$pkg"
  "/sdcard/Android/data/$pkg/files"
)

if ! command -v adb >/dev/null 2>&1; then
  echo "error: 'adb' is not on PATH." >&2
  echo "       On an immutable distro it usually comes from distrobox or" >&2
  echo "       SideQuest rather than the base image." >&2
  exit 1
fi

# Strip CR: adb shell output is CRLF.
shell() { adb shell "$@" | tr -d '\r'; }

devices=$(adb devices | tail -n +2 | grep -c "device$" || true)
if [ "$devices" -eq 0 ]; then
  echo "error: no authorised device. Connect the headset, enable developer" >&2
  echo "       mode, and accept the USB debugging prompt inside the headset." >&2
  adb devices >&2
  exit 1
fi

remote=""
searched=()
for root in "${ROOTS[@]}"; do
  if ! shell ls -d "$root" >/dev/null 2>&1; then
    continue
  fi
  searched+=("$root")
  echo "Searching $root ..."
  found=$(shell find "$root" -name "$marker" 2>/dev/null | head -1 || true)
  if [ -n "$found" ]; then
    remote="$found"
    break
  fi
done

if [ -z "$remote" ]; then
  echo "error: $marker not found on the device." >&2
  echo >&2

  if [ ${#searched[@]} -eq 0 ]; then
    echo "       None of these directories exist:" >&2
    printf '         %s\n' "${ROOTS[@]}" >&2
    echo >&2
    echo "       That means LemonLoader has not patched BONELAB. Run the" >&2
    echo "       LemonLoader installer app on the headset and point it at" >&2
    echo "       BONELAB." >&2
  else
    echo "       Searched:" >&2
    printf '         %s\n' "${searched[@]}" >&2
    echo >&2
    echo "       LemonLoader is installed, so what is missing is the interop" >&2
    echo "       assemblies themselves. They are generated on the *first" >&2
    echo "       launch* of the patched game, not by patching. Launch BONELAB," >&2
    echo "       let it reach the menu, quit, and try again." >&2
    echo >&2
    for root in "${searched[@]}"; do
      echo "       $root:" >&2
      shell ls -A "$root" 2>/dev/null | sed 's/^/         /' >&2
    done
  fi
  exit 1
fi

remote_dir=$(dirname "$remote")
echo "Found $remote_dir"

mkdir -p "$dest"
echo "Pulling to $dest ..."
adb pull "$remote_dir/." "$dest" >/dev/null

if [ ! -f "$dest/$marker" ]; then
  echo "error: pull completed but $dest/$marker is missing." >&2
  exit 1
fi

count=$(find "$dest" -name '*.dll' | wc -l)
echo
echo "Pulled $count assemblies to $dest"
echo
echo "Now build against them:"
echo "  IL2CPP_DIR=$dest ./scripts/build.sh"
