#!/usr/bin/env bash
# The flake rule (#655): CI's test steps run through this. A failed test command runs once more; a pass the second time
# passes the step and records a flake, which .github/workflows/flake-report.yml reads after the run and comments on
# #629; a second failure fails the step with the command's code. Never more than one retry.
#
#   bash scripts/ci/retry-test.sh LABEL COMMAND [ARG...]
#
# LABEL names the job, or the job and its matrix entry ("pester (onboard)"). The record goes to
# $FLAKE_DIR/flake.txt (default $RUNNER_TEMP/flake), one "job: LABEL" line and a "test: ..." line per failed test the
# first attempt printed (a Pester "[-] " line or a check suite's "FAIL " line), and a warning annotation to the log.
# Only test steps use it: a build, security or repo-rules guard failure that isn't a test is never retried
# (tests/bash/test-flake-rule.sh checks which steps call it).
set -uo pipefail

[ "$#" -ge 2 ] || { echo "usage: retry-test.sh LABEL COMMAND [ARG...]" >&2; exit 2; }
label="$1"
shift
dir="${FLAKE_DIR:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/flake}"
mkdir -p "$dir" || exit 2
first="$dir/first-attempt.log"

# The first attempt's output still reaches the log, and a copy goes to a file to name the tests that failed.
"$@" 2>&1 | tee "$first"
code="${PIPESTATUS[0]}"
[ "$code" -ne 0 ] || exit 0
echo "::warning title=Test failed once::$label failed (exit $code); running it once more (the flake rule, #655)"
"$@"
code=$?
if [ "$code" -ne 0 ]; then
  echo "::error title=Test failed twice::$label failed again (exit $code): not a flake, so the job fails"
  exit "$code"
fi
{
  echo "job: $label"
  # Pester colours its output; the colour codes go first.
  sed -E $'s/\x1b\\[[0-9;]*m//g' "$first" | grep -E '^[[:space:]]*\[-\] |^FAIL ' | sed -E 's/^[[:space:]]+//; s/^/test: /' |
    head -n 20
  true
} > "$dir/flake.txt"
echo "::warning title=Flaky test::$label failed once and passed on the retry; flake-report.yml comments on #629"
exit 0
