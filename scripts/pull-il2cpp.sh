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
marker="UnityEngine.CoreModule.dll"

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

echo "Looking for BONELAB's data directory..."
pkg_dir=""
for candidate in $(shell ls /sdcard/Android/data 2>/dev/null | grep -i -e bonelab -e stresslevelzero || true); do
  pkg_dir="/sdcard/Android/data/$candidate"
  echo "  found $pkg_dir"
  break
done

if [ -z "$pkg_dir" ]; then
  echo "error: no BONELAB package directory under /sdcard/Android/data." >&2
  echo "       Either the game is not installed, or this headset stores app" >&2
  echo "       data elsewhere. Look for it yourself with:" >&2
  echo "         adb shell ls /sdcard/Android/data" >&2
  exit 1
fi

echo "Searching for $marker..."
# Android's toybox has find. If it is missing or scoped storage blocks the
# walk, treat that as "not found" and let the diagnostics below show why.
remote=$(shell find "$pkg_dir" -name "$marker" 2>/dev/null | head -1 || true)

if [ -z "$remote" ]; then
  echo "error: $marker is not anywhere under $pkg_dir." >&2
  echo >&2

  # Show what is actually there rather than making the reader go and look.
  contents=$(shell ls -A "$pkg_dir" 2>/dev/null || true)
  if [ -z "$contents" ]; then
    echo "       $pkg_dir is empty or unreadable, which means BONELAB has not" >&2
    echo "       been patched by LemonLoader yet - an unpatched game does not" >&2
    echo "       create this." >&2
  else
    echo "       $pkg_dir contains:" >&2
    echo "$contents" | sed 's/^/         /' >&2
    echo >&2
    if echo "$contents" | grep -qi 'melonloader\|mods\|userdata'; then
      echo "       LemonLoader has been here, so the patch worked - but the" >&2
      echo "       interop assemblies are generated on the *first launch* of" >&2
      echo "       the patched game, not by patching. Launch BONELAB once," >&2
      echo "       let it reach the menu, quit, and run this again." >&2
      echo >&2
      echo "       Deeper listing:" >&2
      shell ls -R "$pkg_dir" 2>/dev/null | head -60 | sed 's/^/         /' >&2
    else
      echo "       Nothing here looks like LemonLoader output, so BONELAB has" >&2
      echo "       most likely not been patched yet. Run the LemonLoader" >&2
      echo "       installer app on the headset and point it at BONELAB." >&2
    fi
  fi
  exit 1
fi

remote_dir=$(dirname "$remote")
echo "Found $remote_dir"

mkdir -p "$dest"
echo "Pulling to $dest ..."
adb pull "$remote_dir/." "$dest" >/dev/null

count=$(find "$dest" -name '*.dll' | wc -l)
if [ ! -f "$dest/$marker" ]; then
  echo "error: pull completed but $dest/$marker is missing." >&2
  exit 1
fi

echo
echo "Pulled $count assemblies to $dest"
echo
echo "Now build against them:"
echo "  IL2CPP_DIR=$dest ./scripts/build.sh"
