#!/usr/bin/env bash
# Packages the mod (the Release build of src/Metaphor.Hooks and its dependencies) into OUT_DIR and prints its SHA-256
# (scripts/check/tree-hash.sh). Later stages and releases use that output, never a rebuild.
#
#   scripts/release/package-mod.sh [--no-build] OUT_DIR
#   scripts/release/package-mod.sh --list FOLDER
#
# --no-build packages the Release build already made by `scripts/check.sh full`, so the bytes shipped are the bytes
# tested. Without it, the mod is built first (used to check that a rebuild from clean gives the same bytes). --list
# only writes FOLDER's package-files.txt, as a package gets it, so the installer's tests read the list a release ships
# (#408).
set -euo pipefail

usage="usage: scripts/release/package-mod.sh [--no-build] OUT_DIR | --list FOLDER"

# The package's own files, which the players' installer copies and nothing else (#303): every file in FOLDER, in byte
# order, with forward slashes, the list itself left out.
write_list() { # write_list FOLDER
  (cd "$1" && find . -type f ! -name package-files.txt | sed 's#^\./##' | LC_ALL=C sort) > "$1/package-files.txt"
}

# A package never carries a session switch (#783): opt-in-*.txt files turn on testing features and development hooks, so a
# release that held one would hand players what a by-ear session is for.
refuse_session_switches() { # refuse_session_switches FOLDER
  local found
  found="$(cd "$1" && find . -type f -name 'opt-in-*.txt')"
  if [ -n "$found" ]; then
    echo "package-mod: the package holds a session switch, which a release never ships: $found" >&2
    exit 1
  fi
}

if [ "${1:-}" = --list ]; then
  [ -n "${2:-}" ] && [ -d "$2" ] || { echo "$usage" >&2; exit 2; }
  refuse_session_switches "$2"
  write_list "$2"
  exit 0
fi

build_flag=""
if [ "${1:-}" = --no-build ]; then
  build_flag="--no-build"
  shift
fi
out="${1:-}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
[ -n "$out" ] || { echo "$usage" >&2; exit 2; }

# OUT_DIR is deleted first, so refuse anything that could hold more than a package.
mkdir -p "$out"
out_abs="$(cd "$out" && pwd)"
case "$out_abs" in
  / | "$root" | "$HOME") echo "package-mod: refusing to use $out as the output folder" >&2; exit 2 ;;
esac
rm -rf "$out_abs"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet publish "$root/src/Metaphor.Hooks/Metaphor.Hooks.csproj" -c Release -o "$out_abs" -v q $build_flag >&2
refuse_session_switches "$out_abs"
write_list "$out_abs"
bash "$root/scripts/check/tree-hash.sh" "$out_abs"
