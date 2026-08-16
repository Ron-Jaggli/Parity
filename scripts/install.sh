#!/usr/bin/env bash
#
# Push the built mod to the headset, and optionally read back the log.
#
#   ./scripts/install.sh          # build if needed, push Parity.dll
#   ./scripts/install.sh --log    # ...then pull MelonLoader's latest log
#
# LemonLoader keeps code mods in a top-level /sdcard/MelonLoader/<package>/Mods
# directory. Note that BONELAB *also* has a folder called Mods, under
# Android/data/<package>/files - that one is the Marrow pallet system for
# avatars, maps and spawnables, and a code mod dropped there does nothing.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

pkg="com.StressLevelZero.BONELAB"
mods_dir="/sdcard/MelonLoader/$pkg/Mods"
dll="src/bin/Release/Parity.dll"

want_log=false
[ "${1:-}" = "--log" ] && want_log=true

if ! command -v adb >/dev/null 2>&1; then
  echo "error: 'adb' is not on PATH." >&2
  exit 1
fi

shell() { adb shell "$@" | tr -d '\r'; }

devices=$(adb devices | tail -n +2 | grep -c "device$" || true)
if [ "$devices" -eq 0 ]; then
  echo "error: no authorised device. Connect the headset, enable developer" >&2
  echo "       mode, and accept the USB debugging prompt inside the headset." >&2
  adb devices >&2
  exit 1
fi

if [ ! -f "$dll" ]; then
  echo "$dll not built yet - building."
  ./scripts/build.sh
fi

if ! shell ls -d "$mods_dir" >/dev/null 2>&1; then
  echo "error: $mods_dir does not exist." >&2
  echo "       Either LemonLoader has not patched BONELAB, or this version of" >&2
  echo "       it uses a different layout. Look for the folder holding your" >&2
  echo "       other code mods:" >&2
  echo "         adb shell ls /sdcard/MelonLoader" >&2
  exit 1
fi

echo "Installing to $mods_dir"
adb push "$dll" "$mods_dir/Parity.dll"

echo
echo "Installed. Launch BONELAB, play for a few minutes, then:"
echo "  ./scripts/install.sh --log"
echo
echo "That pulls Parity-Report.txt, which contains only this mod's output: the"
echo "startup summary, the frame time reports, and any engine call that does not"
echo "exist in your build, reported once by name."

if [ "$want_log" = true ]; then
  echo

  # Parity writes its own report - only this mod's output, where MelonLoader's
  # log interleaves every mod installed. It picks the first directory it can
  # write, so look in the same order it does.
  report=""
  for root in "/sdcard/Parity" "/storage/emulated/0/Parity" "/sdcard/Android/data/$pkg"; do
    found=$(shell find "$root" -name 'Parity-Report.txt' 2>/dev/null | head -1 || true)
    if [ -n "$found" ]; then
      report="$found"
      break
    fi
  done

  if [ -n "$report" ]; then
    echo "Pulling $report"
    adb pull "$report" ./Parity-Report.txt >/dev/null
    echo
    cat ./Parity-Report.txt
    echo
    echo "Saved to ./Parity-Report.txt (previous session: Parity-Report.txt.prev on the headset)"
  else
    echo "No Parity-Report.txt found. Searched /sdcard/Parity,"
    echo "/storage/emulated/0/Parity and /sdcard/Android/data/$pkg."
  fi

  # MelonLoader's log is the fallback, and the place to look when the mod did not
  # load at all - in which case it never got to write a report of its own.
  log=$(shell find "/sdcard/MelonLoader/$pkg" -iname 'Latest.log' 2>/dev/null | head -1 || true)
  if [ -n "$log" ]; then
    adb pull "$log" ./Latest.log >/dev/null
    echo
    echo "MelonLoader log saved to ./Latest.log. Parity lines in it:"
    grep -i 'parity' ./Latest.log || echo "  (none - the mod did not load; check ./Latest.log for why)"
  fi
fi
