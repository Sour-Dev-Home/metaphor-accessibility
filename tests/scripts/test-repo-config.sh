#!/usr/bin/env bash
# Checks the committed repository settings against the repo itself: the main ruleset's required checks must name CI
# jobs (a renamed job never reports, so every PR would wait forever), and every CODEOWNERS path must exist (a moved
# folder silently loses its owner). It also holds the supply-chain rules: actions pinned to commits, read-only
# workflow tokens by default, CodeQL required, and weekly Dependabot updates.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
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
GATE="$ROOT/.github/workflows/review-gate.yml"
check "review-gate.yml exists" [ -f "$GATE" ]
ci_jobs_section="$(sed -n '/^jobs:/,$p' "$CI")"
gate_jobs_section="$(sed -n '/^jobs:/,$p' "$GATE")"
ci_jobs="$(sed -nE 's/^  ([A-Za-z0-9_-]+):[[:space:]]*$/\1/p' <<<"$ci_jobs_section"$'\n'"$gate_jobs_section")"
check "no CI job sets a name, so checks are named by job id" \
  bash -c '! grep -qE "^    name:" <<<"$1"' _ "$ci_jobs_section"$'\n'"$gate_jobs_section"
contexts="$(grep -oE '"context"[[:space:]]*:[[:space:]]*"[^"]*"' "$RULESET" | sed -E 's/.*"([^"]*)"$/\1/')"

check "the ruleset requires at least one check" [ -n "$contexts" ]
while IFS= read -r context; do
  [ -n "$context" ] || continue
  check "required check '$context' is a CI job" grep -qxF -- "$context" <<<"$ci_jobs"
done <<<"$contexts"

# Every CI job that gates a merge is required, so a skipped or split job can't drop out of the ruleset (#100). The
# change-detection step makes a skipped job report success, so a required check is never missing.
for required in repo-rules build scripts security review-gate; do
  check "'$required' is a required check" grep -qxF -- "$required" <<<"$contexts"
done
check "the build job runs on Linux for pull requests and on Windows otherwise" \
  grep -qF "runs-on: \${{ github.event_name == 'pull_request' && 'ubuntu-latest' || 'windows-latest' }}" "$CI"

# Security checks block merges: the CI job that runs the CodeQL analysis must be a required check.
codeql_jobs="$(awk '/^  [A-Za-z0-9_-]+:[[:space:]]*$/ { job = $1; sub(":", "", job) }
  /uses:[[:space:]]*github\/codeql-action\/analyze@/ { print job }' <<<"$ci_jobs_section")"
check "a CI job runs the CodeQL analysis" [ -n "$codeql_jobs" ]
while IFS= read -r job; do
  [ -n "$job" ] || continue
  check "CodeQL job '$job' is a required check" grep -qxF -- "$job" <<<"$contexts"
done <<<"$codeql_jobs"

# The vulnerability audit reads the committed lock files with a pinned scanner, never a restore or a build (#324), and
# the build's CI restore runs in locked mode so the lock files can't drift from the projects.
security_job="$(awk '/^  security:[[:space:]]*$/ { on = 1; next } /^  [A-Za-z0-9_-]+:[[:space:]]*$/ { on = 0 } on' \
  <<<"$ci_jobs_section")"
check "the security job audits with scripts/vuln-audit.sh" \
  grep -qF 'bash scripts/vuln-audit.sh scan' <<<"$security_job"
check "the security job runs no dotnet command" bash -c '! grep -qE "(^|[[:space:]])dotnet[[:space:]]" <<<"$1"' _ \
  "$security_job"
check "the audit pins OSV-Scanner by version and SHA-256" \
  grep -qE '^OSV_SHA256_LINUX=[0-9a-f]{64}$' "$ROOT/scripts/vuln-audit.sh"
check "check.sh restores in locked mode in CI" \
  grep -qF '[ -z "${GITHUB_ACTIONS:-}" ] || restore_flags=(--locked-mode)' "$ROOT/scripts/check/build.sh"
# Running isn't enough: a new high or critical CodeQL security alert blocks the merge.
check "the ruleset blocks merges on high or critical CodeQL security alerts" \
  bash -c 'tr -d "[:space:]" <"$1" | grep -qE "\"type\":\"code_scanning\",\"parameters\":\{\"code_scanning_tools\":\[\{\"tool\":\"CodeQL\",[^}]*\"security_alerts_threshold\":\"(high_or_higher|medium_or_higher|all)\""' _ "$RULESET"

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

# The review gate (#330) runs main's copy of its workflow through pull_request_target, with a token that can read the
# PR. It must never run the PR's code: no checkout of the PR's head or merge ref, no fetch of its commits, no action
# but checkout, no expression spliced into a script, no secrets and nothing written.
gate_triggers="$(awk '/^on:/ { on = 1; next } on && /^[^[:space:]#]/ { on = 0 } on && /^  [a-z_]+:/ { print $1 }' "$GATE")"
check "review-gate.yml runs on pull_request_target and merge_group only" \
  [ "$(sort <<<"$gate_triggers" | tr '\n' ' ')" = "merge_group: pull_request_target: " ]
gate_code="$(grep -vE '^[[:space:]]*#' "$GATE")"
check "review-gate.yml writes nothing" bash -c '! grep -qiE "write" <<<"$1"' _ "$gate_code"
check "review-gate.yml asks only for read access to contents, pull requests and its runs" \
  bash -c '! awk "/^[[:space:]]*permissions:/ { on = 1; ind = match(\$0, /[^ ]/); next }
    on && match(\$0, /[^ ]/) <= ind { on = 0 } on" <<<"$1" | grep -vE "^[[:space:]]+(contents|pull-requests|actions): read[[:space:]]*(#.*)?$" | grep -q .' \
  _ "$gate_code"
check "review-gate.yml never checks out or fetches the PR's code" \
  bash -c '! grep -qE "(^|[^a-z_])ref:|head\.(ref|repo)|head_ref|merge_commit_sha|refs/pull|git (fetch|checkout|switch|pull|worktree|clone)|gh (pr|repo|run|release)|tarball|zipball|/contents/|raw\.githubusercontent|curl|wget" <<<"$1"' \
  _ "$gate_code"
check "review-gate.yml uses no action but checkout" \
  bash -c '! grep -E "uses:" <<<"$1" | grep -vqE "uses:[[:space:]]*actions/checkout@"' _ "$gate_code"
check "review-gate.yml's checkouts keep no credentials" \
  bash -c '[ "$(grep -c "uses:[[:space:]]*actions/checkout@" <<<"$1")" = "$(grep -cE "persist-credentials:[[:space:]]*false" <<<"$1")" ]' \
  _ "$gate_code"
check "review-gate.yml reads no secrets and uses no environment" \
  bash -c '! grep -qE "secrets\.|^[[:space:]]+environment:" <<<"$1"' _ "$gate_code"
check "review-gate.yml splices no expression into a script, only into keys such as env" \
  bash -c '! grep -F "\${{" <<<"$1" | grep -vE "^[[:space:]]*(-[[:space:]]+)?[A-Za-z_][A-Za-z0-9_-]*:[[:space:]]" | grep -q . &&
    ! grep -F "\${{" <<<"$1" | grep -qE "^[[:space:]]*(-[[:space:]]+)?run:"' _ "$gate_code"

# check.sh loads its modules from scripts/check/ (ADR 0013), so the workflows that run the base branch's copy fetch
# that folder too, or the stage stops on a missing function.
check "review-gate.yml's sparse checkout takes check.sh's modules" \
  bash -c 'grep -qxE "[[:space:]]+scripts/check/" <<<"$1"' _ "$gate_code"
check "ci.yml's commits step copies check.sh's modules from the base branch" \
  grep -qF 'git ls-tree --name-only "$BASE" scripts/check/' "$CI"
# Actions runs bash -e without pipefail, so the step sets it before that pipe, or a failed git ls-tree goes unseen.
check "ci.yml's commits step sets pipefail before listing the modules" \
  bash -c 'awk "/set -o pipefail/ { on = 1 } /git ls-tree/ { found = on; exit } END { exit !found }" "$1"' _ "$CI"

# The slow suite reports and never gates: it runs on a schedule or by hand, never on a push or pull request, so it
# can't become a check a merge waits for.
SLOW="$WORKFLOWS/slow.yml"
check "slow.yml exists" [ -f "$SLOW" ]
slow_triggers="$(awk '/^on:/ { on = 1; next } on && /^[^[:space:]#]/ { on = 0 } on && /^  [a-z_]+:/ { print $1 }' "$SLOW")"
check "slow.yml runs weekly and on demand" [ "$(sort <<<"$slow_triggers" | tr '\n' ' ')" = "schedule: workflow_dispatch: " ]

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
