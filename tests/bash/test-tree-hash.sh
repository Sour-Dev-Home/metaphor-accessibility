#!/usr/bin/env bash
# Black-box tests for scripts/check/tree-hash.sh: the hash must depend on file paths and bytes only.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
HASH="$ROOT/scripts/check/tree-hash.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

make_tree() { # make_tree DIR: two files, one nested
  mkdir -p "$1/sub"
  printf 'alpha' > "$1/a.dll"
  printf 'beta' > "$1/sub/b.json"
}

make_tree "$WORK/one"
sleep 1
make_tree "$WORK/two" # same content, later timestamps
cp -r "$WORK/one" "$WORK/changed" && printf 'alphA' > "$WORK/changed/a.dll"
cp -r "$WORK/one" "$WORK/renamed" && mv "$WORK/renamed/a.dll" "$WORK/renamed/c.dll"

one="$(bash "$HASH" "$WORK/one")"
check "prints a SHA-256" grep -Eqx '[0-9a-f]{64}' <<<"$one"
check "same bytes and paths give the same hash" [ "$one" = "$(bash "$HASH" "$WORK/two")" ]
check "a changed byte changes the hash" [ "$one" != "$(bash "$HASH" "$WORK/changed")" ]
check "a renamed file changes the hash" [ "$one" != "$(bash "$HASH" "$WORK/renamed")" ]
bash "$HASH" "$WORK/missing" > /dev/null 2>&1
check "a missing folder is an error" [ $? -eq 2 ]

printf 'tree-hash tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
