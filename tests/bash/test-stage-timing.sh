#!/usr/bin/env bash
# Black-box tests for scripts/check/stage-timing.sh: durations, the budget warning and the CI jobs table.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
TIMING="$ROOT/scripts/check/stage-timing.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0

run() { # run ARGS...: capture the exit code and combined output
  out="$(bash "$TIMING" "$@" 2>&1)"
  code=$?
}

expect() { # expect NAME CODE [TEXT...]: the last run exited with CODE and printed every TEXT
  local name="$1" want="$2" text ok=1
  shift 2
  [ "$code" -eq "$want" ] || ok=0
  for text in "$@"; do [[ "$out" == *"$text"* ]] || ok=0; done
  if [ "$ok" -eq 1 ]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n  want exit %s and: %s\n  got exit %s:\n%s\n' "$name" "$want" "$*" "$code" "$out"
  fi
}

absent() { # absent NAME TEXT: the last run didn't print TEXT
  if [[ "$out" == *"$2"* ]]; then
    failed=$((failed + 1))
    printf 'FAIL %s\n  did not want: %s\n  got:\n%s\n' "$1" "$2" "$out"
  else
    passed=$((passed + 1))
  fi
}

# 2026-11-16T20:04:55Z is 1794859495.
start=1794859495

run report "stage 1 (fast)" 10 "$start" $((start + 4)) "commit abc1234"
expect "within budget prints the times and the detail" 0 \
  "timing: stage 1 (fast) started 2026-11-16T20:04:55Z, finished 2026-11-16T20:04:59Z, took 4 s (budget 10 s), commit abc1234"
absent "within budget doesn't warn" "warning"

run report "stage 1 (fast)" 10 "$start" $((start + 10))
expect "exactly on budget is within it" 0 "took 10 s"
absent "exactly on budget doesn't warn" "warning"

run report "stage 4 (signatures)" 30 "$start" $((start + 95))
expect "over budget warns, but exits 0" 0 "took 1 min 35 s (budget 30 s)" \
  "warning: stage 4 (signatures) took 1 min 35 s, over its 30 s budget"
absent "outside Actions there's no annotation" "::warning"

GITHUB_ACTIONS=true run report "stage 2" 300 "$start" $((start + 301))
expect "over budget in Actions is an annotation" 0 "::warning title=Stage over budget::stage 2 took 5 min 1 s, over its 5 min budget"

run report "stage 6" - "$start" $((start + 3600))
expect "no budget never warns" 0 "took 60 min"
absent "no budget prints none" "budget"

run report "stage 1" ten "$start" "$start"
expect "a budget that isn't seconds is a usage error" 2 "budget must be whole seconds"
run report "stage 1" 10 $((start + 1)) "$start"
expect "end before start is a usage error" 2 "end is before start"
run report "stage 1" 10 "$start"
expect "missing arguments are a usage error" 2 "usage"

jobs="$WORK/jobs.tsv"
printf 'repo-rules\t2026-11-16T20:04:55Z\t2026-11-16T20:05:13Z\r\nbuild\t2026-11-16T20:04:55Z\t2026-11-16T20:09:43Z\r\nsecurity\t2026-11-16T20:04:56Z\t2026-11-16T20:08:15Z\r\n' > "$jobs"
summary="$WORK/summary.md"
GITHUB_ACTIONS=true GITHUB_STEP_SUMMARY="$summary" run jobs "stage 2 (CI)" 300 "$jobs"
expect "jobs prints each job's duration and times the stage from first start to last finish" 0 \
  "| repo-rules | 18 s |" "| build | 4 min 48 s |" "| security | 3 min 19 s |" \
  "timing: stage 2 (CI) started 2026-11-16T20:04:55Z, finished 2026-11-16T20:09:43Z, took 4 min 48 s (budget 5 min)"
absent "a stage within budget isn't annotated" "::warning"
out="$(cat "$summary")"
expect "the table and the stage line reach the job summary" 0 "| Job | Duration |" "| build | 4 min 48 s |" "timing: stage 2 (CI)"
absent "the job summary holds no warning annotation" "::warning"

GITHUB_ACTIONS=true run jobs "stage 2 (CI)" 240 "$jobs"
expect "a slow CI stage is annotated" 0 "::warning title=Stage over budget::stage 2 (CI) took 4 min 48 s, over its 4 min budget"

: > "$WORK/empty.tsv"
run jobs "stage 2 (CI)" 300 "$WORK/empty.tsv"
expect "no finished jobs says so and passes" 0 "has no finished jobs"

printf 'build\tnot-a-time\t2026-11-16T20:09:43Z\n' > "$WORK/bad.tsv"
run jobs "stage 2 (CI)" 300 "$WORK/bad.tsv"
expect "an unreadable time is a usage error" 2 "unreadable time"
run jobs "stage 2 (CI)" 300 "$WORK/missing.tsv"
expect "a missing file is a usage error" 2 "no jobs file"

printf 'stage-timing tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
