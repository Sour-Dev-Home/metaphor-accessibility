#!/usr/bin/env bash
# Prints one SHA-256 for a folder, computed from each file's relative path and bytes only (never timestamps), so the
# same build gives the same hash on any machine. Used to identify the packaged mod.
#
#   scripts/tree-hash.sh DIR
#
# The hash is the SHA-256 of a manifest with one line per file, sorted by path: "<file sha256>  <relative path>".
set -euo pipefail

dir="${1:-}"
[ -d "$dir" ] || { echo "tree-hash: not a folder: ${dir:-<none>}" >&2; exit 2; }

cd "$dir"
find . -type f -print0 | LC_ALL=C sort -z | while IFS= read -r -d '' file; do
  hash="$(sha256sum < "$file")"
  printf '%s  %s\n' "${hash%% *}" "${file#./}"
done | sha256sum | cut -d' ' -f1
