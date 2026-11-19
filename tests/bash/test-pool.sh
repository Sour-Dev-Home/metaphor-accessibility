#!/usr/bin/env bash
# Tests for scripts/check/pool.sh (#367), the worker pool scripts/check/run-pester.sh, scripts/check/run-check-suites.sh and
# tests/check/test-check.sh share: at most JOBS jobs at once, each job's output in its own file, a callback with each
# job's exit code and time as it finishes, and every way a job can go wrong (a failure, a crash, a kill, a job the pool
# lost track of) counted as a failure.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS GITHUB_ACTIONS SECURITY_REVIEW_SKIP SKIP_SCRIPT_TESTS

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
unset METAPHOR_LIVE_LOG METAPHOR_LIVE_PID
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
matches() { [[ "$1" =~ $2 ]]; } # matches VALUE REGEX
lacks() { [[ "$out" != *"$1"* ]]; }

# shellcheck source=../../scripts/check/pool.sh
source "$ROOT/scripts/check/pool.sh" || { echo "FAIL pool.sh loads"; exit 1; }

# Stand-in jobs. busy NAME SECONDS: mark itself running while it sleeps, recording how many were running at once.
busy() {
  : > "$WORK/running.$1"
  ls "$WORK" | grep -c '^running\.' >> "$WORK/concurrent"
  echo "$1 started"
  sleep "$2"
  rm -f "$WORK/running.$1"
  echo "$1 done"
}
fails() { echo "$1 is failing"; return "$2"; }
killed() { echo "$1 before the kill"; kill -9 "$BASHPID"; echo "never printed"; }
# A program that crashes (bash itself handles a SEGV sent to it, so a child process stands in).
crashed() { exec bash -c 'kill -SEGV $$'; }
chatty() { # chatty NAME: print ten lines, slowly enough to overlap with another chatty job
  local n
  for n in 1 2 3 4 5 6 7 8 9 10; do echo "$1 line $n"; sleep 0.05; done
}
done_log=()
noted() { done_log+=("$1 $2 $3"); } # the ON_DONE callback: ID CODE SECONDS

new_pool() { # new_pool JOBS [ON_DONE]: a fresh pool in a fresh folder
  rm -rf "$WORK/out" "$WORK"/running.* "$WORK/concurrent"
  mkdir "$WORK/out"
  done_log=()
  pool_init "$WORK/out" "$@"
}

# The bound: at most JOBS at once, and every job runs.
new_pool 2 noted
for name in a b c d e; do pool_run "$name" busy "$name" 0.3; done
pool_wait
check "bound: never more than two at once" [ "$(sort -n "$WORK/concurrent" | tail -n 1)" -le 2 ]
check "bound: two did run at once" [ "$(sort -n "$WORK/concurrent" | tail -n 1)" -eq 2 ]
check "bound: every job ran" [ "$(wc -l < "$WORK/concurrent")" -eq 5 ]
check "bound: the start order is kept" [ "${pool_ids[*]}" = "a b c d e" ]
check "bound: a callback per job" [ "${#done_log[@]}" -eq 5 ]
out="$(printf '%s\n' "${done_log[@]}")"
check "bound: the callback gets the id, the exit code and the seconds" grep -qE '^c 0 [0-9]+\.[0-9]$' <<< "$out"
check "bound: each job's time" matches "${pool_secs[a]}" '^(0\.[3-9]|[1-9][0-9]*\.[0-9])$'
check "bound: each job's code" [ "${pool_code[e]}" = 0 ]
out="$(cat "${pool_file[c]}")"
check "bound: each job's output in its own file" [ "$out" = "$(printf 'c started\nc done')" ]

new_pool 1
for name in a b c; do pool_run "$name" busy "$name" 0.1; done
pool_wait
check "one job: one at a time" [ "$(sort -n "$WORK/concurrent" | tail -n 1)" -eq 1 ]

# Every kind of failure is a non-zero code, passed to the callback; nothing else.
new_pool 4 noted
pool_run good busy good 0.1
pool_run failing fails failing 3
pool_run killed killed killed
pool_run crashed crashed
pool_run empty true
pool_wait
check "codes: a passing job is 0" [ "${pool_code[good]}" = 0 ]
check "codes: a failing job's own code" [ "${pool_code[failing]}" = 3 ]
check "codes: a killed job fails" [ "${pool_code[killed]}" = 137 ]
check "codes: a crashed job fails" [ "${pool_code[crashed]}" = 139 ]
check "codes: an empty job's code is its own" [ "${pool_code[empty]}" = 0 ]
out="$(printf '%s\n' "${done_log[@]}")"
check "codes: the callback sees the kill" grep -qE '^killed 137 ' <<< "$out"
check "codes: the failed list" [ "$(pool_failed)" = "$(printf '%s\n' failing killed crashed)" ]
out="$(pool_print_failed)"
check "printed: each failed job, named with its code" has "FAIL failing (exit 3):"
check "printed: its output, indented" has "  failing is failing"
check "printed: a killed job's output up to the kill" has "  killed before the kill"
check "printed: a crashed job" has "FAIL crashed (exit 139):"
check "printed: passing jobs stay out" lacks "good"
check "printed: in start order" [ "$(grep '^FAIL' <<< "$out" | cut -d ' ' -f 2)" = \
  "$(printf '%s\n' failing killed crashed)" ]

# Jobs running together can't interleave their output, so one job's FAIL line can't hide in another's.
new_pool 2
pool_run left chatty left
pool_run right chatty right
pool_wait
check "interleaving: one job's file holds only its lines" [ -z "$(grep -v '^left ' "${pool_file[left]}")" ]
check "interleaving: every line kept" [ "$(wc -l < "${pool_file[right]}")" -eq 10 ]

# Ids that aren't file names (a suite's path) still get a file each.
new_pool 2
pool_run tests/bash/test-a.sh echo a
pool_run "two words" echo b
pool_wait
check "ids: a path as id" [ "$(cat "${pool_file[tests/bash/test-a.sh]}")" = a ]
out="${pool_file[two words]}"
check "ids: a file inside the pool's folder" [ -f "$WORK/out/${out#"$WORK/out/"}" ]
check "ids: holding the job's output" [ "$(cat "$out")" = b ]

# A job something else waited for (here the callback, as no caller should) gets either its own code, when bash still
# has it, or 127: never a pass for a job that failed, and no hang. Which one is bash's choice: it sometimes keeps the
# last-started job's ($!) code after a bare `wait` and drops the others' (#379 failed on Ubuntu on that), so the jobs
# fail with codes of their own and either answer is accepted.
reaper() { [ "$1" != first ] || wait; }
slow_fail() { sleep 2; return "$1"; } # long enough to be running still when first's callback waits, on a loaded machine
new_pool 3 reaper
pool_run first true
pool_run second slow_fail 5
pool_run third slow_fail 6
pool_wait 2> /dev/null # bash's "not a child of this shell"
check "lost: a failed job never passes" matches "${pool_code[second]:-unset}" '^(5|127)$'
check "lost: nor does the last one started" matches "${pool_code[third]:-unset}" '^(6|127)$'

# A PID given out again while an unseen job still holds it in the list: that old job fails with 127, and the new one is
# tracked. The reuse can't be forced, so the stale entry is planted under a running job's PID.
new_pool 2 noted
pool_run real busy real 0.3
pid="${!pool_running[*]}"
pool_running[$pid]=ghost pool_began[ghost]=$EPOCHREALTIME
pool_ids+=(ghost)
pool_track "$pid" real
pool_wait
check "PID reused: the old job fails" [ "${pool_code[ghost]:-unset}" = 127 ]
check "PID reused: its callback ran" grep -q '^ghost 127 ' <<< "$(printf '%s\n' "${done_log[@]}")"
check "PID reused: the new job is still tracked" [ "${pool_code[real]:-unset}" = 0 ]

# The default bound: METAPHOR_TEST_JOBS, else the cores up to 8 shared among this run and the other runs holding a
# build slot now. The slots are stand-in lock folders as scripts/check/run.sh's take_lock makes them.
cores="$(nproc 2> /dev/null || echo 2)"
[ "$cores" -le 8 ] || cores=8
half=$(((cores / 2) > 0 ? cores / 2 : 1))
slot_dir="$WORK/slots"
true &
dead=$!
wait "$dead"
slots() { # slots PID...: slot n held by the nth PID ("-" for one with no pid yet)
  local n=1 pid lock
  rm -rf "$slot_dir" && mkdir "$slot_dir"
  for pid in "$@"; do
    lock="$slot_dir/lock"
    [ "$n" -eq 1 ] || lock="$lock.$n"
    mkdir "$lock"
    [ "$pid" = - ] || echo "$pid" > "$lock/pid"
    n=$((n + 1))
  done
}
jobs_now() { # jobs_now [HELD]: pool_default_jobs over the stand-in slots, this run holding HELD
  METAPHOR_TEST_JOBS='' METAPHOR_CHECK_SLOTS="${SLOTS:-2}" METAPHOR_CHECK_LOCK="$slot_dir/lock" \
    METAPHOR_CHECK_HELD="${1:-}" pool_default_jobs
}
check "default: METAPHOR_TEST_JOBS wins" [ "$(METAPHOR_TEST_JOBS=3 pool_default_jobs)" = 3 ]
slots
check "default: no slot held, every core (up to 8)" [ "$(jobs_now)" = "$cores" ]
check "default: a bad METAPHOR_TEST_JOBS is ignored" [ "$(METAPHOR_TEST_JOBS=x jobs_now)" = "$cores" ]
slots $$
check "default: a lone full, holding its slot, gets every core" [ "$(jobs_now "$slot_dir/lock")" = "$cores" ]
slots $$ $$
check "default: two fulls at once get half each" [ "$(jobs_now "$slot_dir/lock")" = "$half" ]
check "default: a run holding no slot beside one full gets half" [ "$(SLOTS=1 jobs_now)" = "$half" ]
slots $$ "$dead"
check "default: a slot whose holder exited is free" [ "$(jobs_now "$slot_dir/lock")" = "$cores" ]
slots $$ -
check "default: a slot being taken counts" [ "$(jobs_now "$slot_dir/lock")" = "$half" ]
slots $$ $$ $$ $$ $$ $$ $$ $$ $$
check "default: never below 1" [ "$(SLOTS=9 jobs_now)" = 1 ]

# A pool reused after another starts empty.
new_pool 2
pool_run only true
pool_wait
check "reuse: only the new pool's jobs" [ "${pool_ids[*]} ${#pool_code[@]}" = "only 1" ]

# A bad bound or a missing folder is the caller's mistake: pool_init says so and fails.
out="$(pool_init "$WORK/out" 0 2>&1)"
check "bad bound: fails" [ $? -eq 2 ]
check "bad bound: says so" has "pool: JOBS must be a whole number"
out="$(pool_init "$WORK/missing" 2 2>&1)"
check "missing folder: fails" [ $? -eq 2 ]

echo "pool tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
