#!/usr/bin/env bash
# Tests scripts/ci/ci-changes.sh against a throwaway repository: which changed files let a CI job skip (#100).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
SCRIPT="$ROOT/scripts/ci/ci-changes.sh"
passed=0
failed=0

tmp="$(mktemp -d)" || exit 2
trap 'rm -rf "$tmp"' EXIT
cd "$tmp" || exit 2
git init -q .
git config user.email t@example.invalid
git config user.name t
git config commit.gpgsign false
mkdir docs scripts src
echo a > README.md
git add -A
git commit -q -m "chore: base"
base="$(git rev-parse HEAD)"

# expect NAME EXPECTED JOB EVENT PATH...: commit the paths on top of the base, then ask the script.
expect() {
  local name="$1" expected="$2" job="$3" event="$4"
  shift 4
  git checkout -q --detach "$base"
  for path in "$@"; do
    mkdir -p "$(dirname "$path")"
    echo "$RANDOM" >> "$path"
  done
  git add -A
  git commit -q -m "chore: change"
  local got
  got="$(EVENT_NAME="$event" GITHUB_OUTPUT= bash "$SCRIPT" "$job" "$base" "$(git rev-parse HEAD)")"
  if [ "$got" = "run=$expected" ]; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name: got $got"; fi
}

expect "docs-only PR skips code jobs" false code pull_request docs/a.md README.md
expect "raw-source PR skips code jobs" false code pull_request docs/raw-sources/x.txt
expect "a code PR runs code jobs" true code pull_request src/a.cs docs/a.md
expect "a workflow change runs code jobs" true code pull_request .github/workflows/ci.yml
expect "docs-only PR skips Pester" false scripts pull_request docs/a.md
expect "a source-only PR skips Pester" false scripts pull_request src/a.cs
expect "a script change runs Pester" true scripts pull_request scripts/x.sh
expect "a smoke-test change runs Pester" true scripts pull_request tests/pester/a.Tests.ps1
expect "the merge queue skips nothing (code)" true code merge_group docs/a.md
expect "the merge queue skips nothing (scripts)" true scripts merge_group docs/a.md
expect "a push to main skips nothing" true scripts push docs/a.md

printf 'ci-changes tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
