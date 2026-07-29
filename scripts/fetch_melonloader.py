#!/usr/bin/env python3
"""Download MelonLoader's assemblies and print the directory holding them.

MelonLoader is Apache-2.0 and published as GitHub release assets, so unlike the
game's IL2CPP assemblies it can simply be fetched. Both CI and scripts/build.sh
use this, so there is one definition of "which MelonLoader are we building
against".

Only the resolved path goes to stdout; everything else goes to stderr, so the
caller can capture it directly:

    MELON_DIR=$(python3 scripts/fetch_melonloader.py --tag v0.6.6 --dest .melonloader)

Deliberately uses nothing outside the standard library - no jq, no unzip - so it
behaves the same on a CI runner and on an immutable desktop distro where
installing tools is a chore.
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
import zipfile

API = "https://api.github.com/repos/LavaGang/MelonLoader/releases/tags/{tag}"

# Files the build actually needs, and what we search the archive for.
REQUIRED = ("MelonLoader.dll", "Il2CppInterop.Runtime.dll")


def log(message):
    print(message, file=sys.stderr)


def http_get(url, token=None, accept=None):
    request = urllib.request.Request(url)
    request.add_header("User-Agent", "parity-build")
    if accept:
        request.add_header("Accept", accept)
    if token:
        request.add_header("Authorization", "Bearer " + token)
    return urllib.request.urlopen(request).read()


def resolve_asset(tag, token):
    try:
        payload = json.loads(http_get(API.format(tag=tag), token, "application/vnd.github+json"))
    except urllib.error.HTTPError as error:
        if error.code == 404:
            log("error: MelonLoader release '%s' does not exist." % tag)
            log("       Pick one from https://github.com/LavaGang/MelonLoader/releases")
        else:
            log("error: GitHub API returned %s for release '%s'." % (error.code, tag))
        raise SystemExit(1)

    assets = payload.get("assets", [])
    log("Assets in %s:" % tag)
    for asset in assets:
        log("  " + asset["name"])

    # The canonical Windows x64 archive, then anything else x64-shaped. The
    # assemblies inside are platform-agnostic IL either way; we only ever read
    # them as compile-time references.
    for match in (lambda n: n.lower() == "melonloader.x64.zip",
                  lambda n: "x64" in n.lower() and n.lower().endswith(".zip")):
        for asset in assets:
            if match(asset["name"]):
                return asset["name"], asset["browser_download_url"]

    log("error: no x64 .zip asset found in release '%s' (listed above)." % tag)
    raise SystemExit(1)


def find_assemblies(root):
    """Locate a directory containing every required assembly, preferring net6."""
    candidates = []
    for dirpath, _, filenames in os.walk(root):
        if all(name in filenames for name in REQUIRED):
            candidates.append(dirpath)

    if not candidates:
        return None

    # 0.6.x archives ship both net6 and net35 builds of MelonLoader.dll; mods
    # target net6.
    for path in candidates:
        if "net6" in path.replace(os.sep, "/").lower():
            return path
    return candidates[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", default="v0.6.6", help="MelonLoader release tag (default: v0.6.6)")
    parser.add_argument("--dest", default=".melonloader", help="where to extract (default: .melonloader)")
    parser.add_argument("--force", action="store_true", help="re-download even if already present")
    args = parser.parse_args()

    dest = os.path.abspath(args.dest)
    extracted = os.path.join(dest, "extracted")

    if not args.force:
        cached = find_assemblies(extracted) if os.path.isdir(extracted) else None
        if cached:
            log("Using cached MelonLoader in %s" % cached)
            print(cached)
            return

    os.makedirs(dest, exist_ok=True)
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")

    name, url = resolve_asset(args.tag, token)
    log("Downloading %s" % name)
    archive = os.path.join(dest, "melonloader.zip")
    with open(archive, "wb") as handle:
        handle.write(http_get(url))

    log("Extracting to %s" % extracted)
    with zipfile.ZipFile(archive) as zf:
        zf.extractall(extracted)

    found = find_assemblies(extracted)
    if not found:
        log("error: could not find %s together in the archive." % " and ".join(REQUIRED))
        raise SystemExit(1)

    log("MelonLoader assemblies: %s" % found)
    print(found)


if __name__ == "__main__":
    main()
