#!/usr/bin/env bash
# Checks the committed repository settings against the repo itself: the main ruleset's required checks must name CI
# jobs (a renamed job never reports, so every PR would wait forever), and every CODEOWNERS path must exist (a moved
# folder silently loses its owner). It also holds the supply-chain rules: actions pinned to commits, read-only
# workflow tokens by default, CodeQL required, and weekly Dependabot updates.
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

# Security checks block merges: the CI job that runs the CodeQL analysis must be a required check.
codeql_jobs="$(awk '/^  [A-Za-z0-9_-]+:[[:space:]]*$/ { job = $1; sub(":", "", job) }
  /uses:[[:space:]]*github\/codeql-action\/analyze@/ { print job }' <<<"$ci_jobs_section")"
check "a CI job runs the CodeQL analysis" [ -n "$codeql_jobs" ]
while IFS= read -r job; do
  [ -n "$job" ] || continue
  check "CodeQL job '$job' is a required check" grep -qxF -- "$job" <<<"$contexts"
done <<<"$codeql_jobs"

# Third-party actions are pinned to a full commit, because a tag can be moved to other code. Local actions are exempt.
WORKFLOWS="$ROOT/.github/workflows"
for workflow in "$WORKFLOWS"/*.yml "$WORKFLOWS"/*.yaml; do
  [ -f "$workflow" ] || continue
  name="${workflow##*/}"
  # Read-only by default: the top-level token is read-only, and a job that needs to write asks for it itself.
  top_permissions="$(awk '/^permissions:/ { on = 1; print; next } on && /^[^[:space:]#]/ { on = 0 } on' "$workflow")"
  check "$name sets top-level permissions" [ -n "$top_permissions" ]
  check "$name's top-level permissions are read-only" \
    bash -c '! grep -q "write" <<<"$1"' _ "$top_permissions"
  while IFS= read -r ref; do
    check "$name: '$ref' is pinned to a commit" bash -c '[[ "$1" == ./* || "$1" =~ @[0-9a-f]{40}$ ]]' _ "$ref"
  done < <(sed -nE "s/^[[:space:]]*(-[[:space:]]+)?uses:[[:space:]]*['\"]?([^'\" #]+).*/\2/p" "$workflow")
done

# Dependabot keeps the pins moving: NuGet packages and Actions, each checked weekly.
DEPENDABOT="$ROOT/.github/dependabot.yml"
check "dependabot.yml exists" [ -f "$DEPENDABOT" ]
for ecosystem in nuget github-actions; do
  block="$(awk -v e="$ecosystem" '/package-ecosystem:/ { on = ($0 ~ "\"?" e "\"?[[:space:]]*$") } on' "$DEPENDABOT")"
  check "Dependabot updates $ecosystem" [ -n "$block" ]
  check "Dependabot checks $ecosystem weekly" grep -qE 'interval:[[:space:]]*"?weekly' <<<"$block"
done

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
