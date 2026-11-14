#!/usr/bin/env bash
# Checks the committed repository settings against the repo itself: the main ruleset's required checks must name CI
# jobs (a renamed job never reports, so every PR would wait forever), and every CODEOWNERS path must exist (a moved
# folder silently loses its owner).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RULESET="$ROOT/.github/rulesets/main.json"
CI="$ROOT/.github/workflows/ci.yml"
CODEOWNERS="$ROOT/.github/CODEOWNERS"

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

check "the ruleset export exists" [ -f "$RULESET" ]
check "CODEOWNERS exists" [ -f "$CODEOWNERS" ]

# A job's check is named after its id unless the job sets a name, so job-level names are refused.
ci_jobs_section="$(sed -n '/^jobs:/,$p' "$CI")"
ci_jobs="$(sed -nE 's/^  ([A-Za-z0-9_-]+):[[:space:]]*$/\1/p' <<<"$ci_jobs_section")"
check "no CI job sets a name, so checks are named by job id" \
  bash -c '! grep -qE "^    name:" <<<"$1"' _ "$ci_jobs_section"
contexts="$(grep -oE '"context"[[:space:]]*:[[:space:]]*"[^"]*"' "$RULESET" | sed -E 's/.*"([^"]*)"$/\1/')"

check "the ruleset requires at least one check" [ -n "$contexts" ]
while IFS= read -r context; do
  [ -n "$context" ] || continue
  check "required check '$context' is a CI job" grep -qxF -- "$context" <<<"$ci_jobs"
done <<<"$contexts"

# CODEOWNERS entries are literal paths from the repo root, so each one can be checked for existence.
entries="$(grep -vE '^[[:space:]]*(#|$)' "$CODEOWNERS")"
check "CODEOWNERS has entries" [ -n "$entries" ]
while read -r path owners; do
  [ -n "$path" ] || continue
  check "CODEOWNERS path '$path' exists" [ -e "$ROOT/${path#/}" ]
  check "CODEOWNERS path '$path' has an owner" [ -n "$owners" ]
done <<<"$entries"

printf 'repo-config tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
