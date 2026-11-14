#!/usr/bin/env bash
# Checks the committed repository settings against the repo itself: the main ruleset's required checks must name CI
# jobs (a renamed job never reports, so every PR would wait forever), and every CODEOWNERS path must exist (a moved
# folder silently loses its owner).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RULESET="$ROOT/.github/rulesets/main.json"
CI="$ROOT/.github/workflows/ci.yml"

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

# A job's check is named after its id unless the job sets a name, and ci.yml's jobs don't.
ci_jobs="$(sed -n '/^jobs:/,$p' "$CI" | sed -nE 's/^  ([A-Za-z0-9_-]+):[[:space:]]*$/\1/p')"
contexts="$(grep -oE '"context"[[:space:]]*:[[:space:]]*"[^"]*"' "$RULESET" 2> /dev/null | sed -E 's/.*"([^"]*)"$/\1/')"

check "the ruleset requires at least one check" [ -n "$contexts" ]
while IFS= read -r context; do
  [ -n "$context" ] || continue
  check "required check '$context' is a CI job" grep -qxF -- "$context" <<<"$ci_jobs"
done <<<"$contexts"

# CODEOWNERS entries are literal paths from the repo root, so each one can be checked for existence.
CODEOWNERS="$ROOT/.github/CODEOWNERS"
entries="$(grep -vE '^[[:space:]]*(#|$)' "$CODEOWNERS" 2> /dev/null)"
check "CODEOWNERS has entries" [ -n "$entries" ]
while read -r path owners; do
  [ -n "$path" ] || continue
  check "CODEOWNERS path '$path' exists" [ -e "$ROOT/${path#/}" ]
  check "CODEOWNERS path '$path' has an owner" [ -n "$owners" ]
done <<<"$entries"

printf 'repo-config tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
