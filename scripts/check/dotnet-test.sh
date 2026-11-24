#!/usr/bin/env bash
# dotnet test, judged by what it printed as well as by its exit code (#858): a check that ran tests must fail whenever a
# test failed, and `dotnet test` has been seen to exit 0 with nothing run (`--no-build` with no built test assembly).
#
#   bash scripts/check/dotnet-test.sh [--expect-tests] ARGS...   runs `dotnet test ARGS...`, printing its output
#
# Exits with dotnet's code when that is non-zero. Otherwise exits 1 when a summary line reports a failed test, or, with
# --expect-tests (a whole-solution run), when no project printed a Passed or Failed summary at all.
set -uo pipefail

expect=""
if [ "${1:-}" = --expect-tests ]; then expect=1; shift; fi

out="$(mktemp)" || { echo "tests: could not create a temporary file" >&2; exit 2; }
trap 'rm -f "$out"' EXIT

dotnet test "$@" 2>&1 | tee "$out"
code="${PIPESTATUS[0]}"
[ "$code" -eq 0 ] || exit "$code"

if grep -qE 'Failed: +[1-9]' "$out"; then
  echo "tests: dotnet test exited 0 but its summary reports failed tests"
  exit 1
fi
if [ -n "$expect" ] && ! grep -qE '(Passed|Failed)! ' "$out"; then
  echo "tests: dotnet test exited 0 but printed no test summary, so no tests ran"
  exit 1
fi
exit 0
