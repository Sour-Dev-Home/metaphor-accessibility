#!/usr/bin/env bash
# Decides whether a CI job has anything to do. Usage: ci-changes.sh JOB [BASE HEAD]; prints "run=true" or "run=false"
# (and appends it to $GITHUB_OUTPUT when set). A job that gets "false" skips its steps and still finishes green, so its
# required check always reports.
#
# Only pull requests skip anything. Any other event (the merge queue, a push to main) runs everything, as does a
# diff that can't be read. Jobs:
#   code     build and security: anything except docs/ and Markdown files
#   scripts  the Pester tests: scripts/, tests/smoke/, the stand-in game, and the workflows
set -uo pipefail

job="${1:-}"
base="${2:-}"
head="${3:-}"
event="${EVENT_NAME:-}"

emit() {
  echo "run=$1"
  [ -z "${GITHUB_OUTPUT:-}" ] || echo "run=$1" >> "$GITHUB_OUTPUT"
  exit 0
}

case "$job" in
  code) pattern='^(docs/|.*\.md$)' invert=1 ;;
  scripts) pattern='^(scripts/|tests/smoke/|tests/Metaphor\.StandInGame/|\.github/workflows/)' invert=0 ;;
  *) echo "usage: ci-changes.sh code|scripts [BASE HEAD]" >&2; exit 2 ;;
esac

[ "$event" = pull_request ] || emit true
[ -n "$base" ] && [ -n "$head" ] || emit true
files="$(git diff --name-only "$base...$head")" || emit true
[ -n "$files" ] || emit true

if [ "$invert" -eq 1 ]; then
  # Run unless every changed file matches the pattern.
  if grep -qvE "$pattern" <<<"$files"; then emit true; else emit false; fi
else
  if grep -qE "$pattern" <<<"$files"; then emit true; else emit false; fi
fi
