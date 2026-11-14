#!/usr/bin/env bash
# Packages the mod (the Release build of src/Metaphor.Hooks and its dependencies) into OUT_DIR and prints its SHA-256
# (scripts/tree-hash.sh). Later stages and releases use that output, never a rebuild.
#
#   scripts/package-mod.sh [--no-build] OUT_DIR
#
# --no-build packages the Release build already made by `scripts/check.sh full`, so the bytes shipped are the bytes
# tested. Without it, the mod is built first (used to check that a rebuild from clean gives the same bytes).
set -euo pipefail

build_flag=""
if [ "${1:-}" = --no-build ]; then
  build_flag="--no-build"
  shift
fi
out="${1:-}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
[ -n "$out" ] || { echo "usage: scripts/package-mod.sh [--no-build] OUT_DIR" >&2; exit 2; }

# OUT_DIR is deleted first, so refuse anything that could hold more than a package.
mkdir -p "$out"
out_abs="$(cd "$out" && pwd)"
case "$out_abs" in
  / | "$root" | "$HOME") echo "package-mod: refusing to use $out as the output folder" >&2; exit 2 ;;
esac
rm -rf "$out_abs"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet publish "$root/src/Metaphor.Hooks/Metaphor.Hooks.csproj" -c Release -o "$out_abs" -v q $build_flag >&2
bash "$root/scripts/tree-hash.sh" "$out_abs"
