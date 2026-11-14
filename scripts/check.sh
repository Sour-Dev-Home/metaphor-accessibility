#!/usr/bin/env bash
# The one check command. Local hooks and CI both run it, so their checks never drift apart.
#
#   scripts/check.sh fast   repo rules on staged files (the pre-commit hook)
#   scripts/check.sh full   repo rules on every tracked file (CI)
#
# Rules judge the staged blobs in the index, not the working tree, so they see exactly what would be committed.
# Exit 0 when every rule passes, 1 when a rule fails, 2 on a usage error or when git fails.
set -uo pipefail

die() {
  echo "check: $*" >&2
  exit 2
}

# list_entries prints "<mode> <blob> <stage>\t<path>\0" for each file to check, in `git ls-files -s -z` form.
case "${1:-}" in
  fast)
    list_entries() {
      git diff --cached --name-only --diff-filter=ACMR -z |
        xargs -0 -r git --literal-pathspecs ls-files -s -z --
    } ;;
  full) list_entries() { git ls-files -s -z; } ;;
  *) echo "usage: scripts/check.sh fast|full" >&2; exit 2 ;;
esac

entries="$(mktemp)" || die "could not create a temporary file"
trap 'rm -f "$entries"' EXIT
list_entries > "$entries" || die "could not list files (is this a git repository?)"

# Policy, not a measurement: our code and notes have no reason to exceed this, so anything bigger needs a look.
max_bytes=$((1024 * 1024))

# Known game-file hashes, read from the index: one SHA-256 per line, then a label naming its game build.
known_hashes=""
if git cat-file -e :scripts/game-content.sha256 2>/dev/null; then
  known_hashes="$(git show :scripts/game-content.sha256 | tr -d '\r' | sed 's/#.*//' | awk 'NF { print tolower($1) }')" ||
    die "could not read scripts/game-content.sha256"
fi

failures=0

report_game_content() {
  echo "game content: $1: $2"
  failures=$((failures + 1))
}

while IFS= read -r -d '' entry; do
  meta="${entry%%$'\t'*}"
  path="${entry#*$'\t'}"
  read -r mode blob _ <<<"$meta"
  [ "$mode" = 160000 ] && continue # a submodule: its content lives in another repository

  # Game folders are the git-ignored ones at the repo root. Lower-cased because Windows paths are case-insensitive.
  lower="${path,,}"
  case "$lower" in
    game/* | extracted/* | dumps/* | captures/*) report_game_content "$path" "inside a game folder" ;;
  esac
  case "/$lower" in
    *.rep/*) report_game_content "$path" "inside a disassembler project folder" ;;
    *.cpk | *.pak) report_game_content "$path" "game archive extension" ;;
    *.exe | *.dll | *.asi) report_game_content "$path" "executable extension" ;;
    *.dmp | *.mdmp) report_game_content "$path" "memory dump extension" ;;
    *.gpr | *.idb | *.i64 | *.til | *.nam | *.id0 | *.id1 | *.id2 | *.bndb | *.dd64)
      report_game_content "$path" "disassembler database extension" ;;
  esac

  size="$(git cat-file -s "$blob")" || die "could not read $path"
  [ "$size" -le "$max_bytes" ] || report_game_content "$path" "larger than $max_bytes bytes ($size)"

  if [ -n "$known_hashes" ]; then
    hash="$(git cat-file blob "$blob" | sha256sum | cut -d' ' -f1)" || die "could not hash $path"
    grep -Fxq "$hash" <<<"$known_hashes" && report_game_content "$path" "hash matches a known game file"
  fi
done < "$entries"

[ "$failures" -eq 0 ]
