#!/usr/bin/env bash
# Checks that a release package never carries a session switch (#783): scripts/release/package-mod.sh --list refuses a
# folder holding an opt-in-*.txt file, which turns on testing features and development hooks. No build, no network.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

mkdir -p "$WORK/clean"
echo x > "$WORK/clean/Metaphor.Hooks.dll"
bash "$ROOT/scripts/release/package-mod.sh" --list "$WORK/clean" > /dev/null 2>&1
status=$?
check "a package without a switch is listed" [ "$status" -eq 0 ]
check "the list holds the dll" grep -qx 'Metaphor.Hooks.dll' "$WORK/clean/package-files.txt"

for name in opt-in-testing.txt opt-in-inspector.txt; do
  rm -rf "$WORK/bad"
  mkdir -p "$WORK/bad/sub"
  echo x > "$WORK/bad/Metaphor.Hooks.dll"
  : > "$WORK/bad/sub/$name"
  out="$(bash "$ROOT/scripts/release/package-mod.sh" --list "$WORK/bad" 2>&1)"
  status=$?
  check "a package holding $name is refused" [ "$status" -eq 1 ]
  check "the refusal names $name" grep -q "$name" <<<"$out"
  check "a refused package gets no list" [ ! -e "$WORK/bad/package-files.txt" ]
done

printf 'package-mod tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
