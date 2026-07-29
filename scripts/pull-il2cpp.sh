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
# Android's toybox has find; fall back to a directory guess if it does not.
remote=$(shell find "$pkg_dir" -name "$marker" 2>/dev/null | head -1 || true)

if [ -z "$remote" ]; then
  echo "error: $marker is not anywhere under $pkg_dir." >&2
  echo >&2
  echo "       The likely reason is that LemonLoader has not generated the" >&2
  echo "       interop assemblies yet. They appear only after LemonLoader has" >&2
  echo "       patched BONELAB *and* the patched game has been launched once." >&2
  echo "       Launch it, quit, and run this again." >&2
  echo >&2
  echo "       To look around manually:" >&2
  echo "         adb shell ls -R $pkg_dir | head -50" >&2
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
