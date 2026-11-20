#!/usr/bin/env bash
# Runs the check-script suites (#336): tests/check/test-check.sh and every tests/bash/test-*.sh. It's the one place
# that runs them: CI's repo-rules job and `check.sh full` both call it, and the list is the folder itself, so a suite
# can't run in one and not the other.
#
#   bash scripts/check/run-check-suites.sh            run the suites in check.sh's worker pool (scripts/check/pool.sh,
#                                               METAPHOR_TEST_JOBS at once); print a progress line as each finishes,
#                                               also to the live log, then each passing one's summary line, each
#                                               failing one's whole output and the total; exit 1 when any fails (a
#                                               non-zero exit, a crash, a kill, or no output at all)
#   bash scripts/check/run-check-suites.sh --select   the same, except in Git Bash without WSL (`full` passes it): there
#                                               test-check.sh takes minutes, so it runs only when scripts/ci/what-runs.sh
#                                               says a check script or check test changed since origin/main (#243)
#   bash scripts/check/run-check-suites.sh --list     print the suites, one per line
#
# From Git Bash the whole run goes to WSL once when it's set up (scripts/check/wsl-run.sh), and each suite then runs where it
# is. The runner and test-check.sh write the live log (scripts/check/live-log.sh), so of the suites only test-check.sh gets
# it (METAPHOR_LIVE_LOG and METAPHOR_LIVE_PID): the other suites' cases start check runs of their own, which would
# write to it.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
suites=()
suite_list() { # suite_list: fill suites, paths from the root
  local file
  suites=(tests/check/test-check.sh)
  for file in "$ROOT"/tests/bash/test-*.sh; do
    [ ! -f "$file" ] || suites+=("${file#"$ROOT/"}")
  done
}

if [ "${1:-}" = --list ]; then
  suite_list
  printf '%s\n' "${suites[@]}"
  exit 0
fi

# shellcheck source=wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh"
cd "$ROOT" || exit 2
suite_list

# skip_check_tests: exit 0 when --select applies and nothing test-check.sh covers changed. Only Git Bash without WSL
# (the source above came back there), outside CI and without METAPHOR_FULL_TESTS; without origin/main the change is
# unknown, so it runs.
skip_check_tests() {
  local base selected
  [ "${1:-}" = --select ] || return 1
  case "${OSTYPE:-}" in msys* | cygwin*) ;; *) return 1 ;; esac
  [ "${GITHUB_ACTIONS:-}" != true ] && [ -z "${METAPHOR_FULL_TESTS:-}" ] || return 1
  base="$(git merge-base HEAD origin/main 2> /dev/null)" || return 1
  # Capture first: under pipefail, `! ... | grep -q` would turn a failed git diff, or grep -q's early exit, into a skip.
  selected="$(git diff --name-only "$base" -- | bash scripts/ci/what-runs.sh)" || return 1
  ! grep -q '^check-tests ' <<< "$selected"
}
if skip_check_tests "$@"; then
  suites=("${suites[@]:1}")
  echo "skipped tests/check/test-check.sh: no check script or check test changed since origin/main, and it takes" \
    "minutes in Git Bash (scripts/setup/setup-wsl.ps1 sets WSL up, where it takes seconds)"
fi

logs="$(mktemp -d)" || { echo "run-check-suites: could not create a temporary folder" >&2; exit 2; }
trap 'rm -rf "$logs"' EXIT
# shellcheck source=pool.sh
source "$ROOT/scripts/check/pool.sh" || exit 2
# shellcheck source=live-log.sh
source "$ROOT/scripts/check/live-log.sh" || exit 2
jobs="$(pool_default_jobs)"

# run_suite SUITE: the pool's job, replaced by the suite (exec: no second fork). test-check.sh appends to this run's
# live log under the caller's pid, which live_start has taken out of the environment.
caller_pid="${METAPHOR_LIVE_PID:-}"
run_suite() {
  if [ "$1" = tests/check/test-check.sh ]; then
    [ -z "$caller_pid" ] || export METAPHOR_LIVE_PID="$caller_pid"
  else
    unset METAPHOR_LIVE_LOG METAPHOR_LIVE_PID
  fi
  exec bash "$1"
}
# suite_done SUITE CODE SECONDS: the pool's callback, a progress line, also in the live log. Every suite ends with its
# summary line, so one that exited 0 having printed nothing fails too.
declare -A empty=()
suite_done() {
  local code="$2"
  [ "$code" != 0 ] || [ -s "${pool_file[$1]}" ] || { empty[$1]=1 code=1; }
  live_line "$1" "$code" "$3"
}

started=$SECONDS
live_start check-suites "${#suites[@]}" "$ROOT"
pool_init "$logs" "$jobs" suite_done || exit 2
for suite in "${suites[@]}"; do pool_run "$suite" run_suite "$suite"; done
pool_wait

passed=0 failed=0
for suite in "${suites[@]}"; do
  if [ -n "${empty[$suite]:-}" ]; then
    failed=$((failed + 1))
    echo "FAIL $suite: printed nothing, not even its summary line"
  elif [ "${pool_code[$suite]}" = 0 ]; then
    passed=$((passed + 1))
    echo "ok   $suite: $(tail -n 1 "${pool_file[$suite]}")"
  else
    failed=$((failed + 1))
  fi
done
pool_print_failed
echo "check-script suites: $passed passed, $failed failed ($((SECONDS - started)) s)"
live_end "$((failed > 0))"
[ "$failed" -eq 0 ]
