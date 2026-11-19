# shellcheck shell=bash
# The check runners' one worker pool (ADR 0013, #367): scripts/check/run-pester.sh, scripts/check/run-check-suites.sh and
# tests/check/test-check.sh run their jobs through it. Sourced; it defines functions only, and they use builtins alone
# past the job starts (a process start costs about 0.3 s in Git Bash, #348). Needs bash 5.1 for `wait -n -p`.
#
#   pool_default_jobs              print how many jobs to run at once: METAPHOR_TEST_JOBS when it is a whole number,
#                                  else the cores (up to 8) shared among the runs holding a build slot now
#                                  (scripts/check/run.sh) and this one, so a lone `full` gets them all and two
#                                  concurrent ones half each; at least 1
#   pool_init DIR JOBS [ON_DONE]   start a pool: each job's output in a file in DIR, at most JOBS jobs at once, and
#                                  `ON_DONE ID CODE SECONDS` run as each job finishes (SECONDS to a tenth, "12.3")
#   pool_run ID COMMAND [ARG...]   wait for a free slot, then start COMMAND in the background, stdout and stderr in
#                                  pool_file[ID]; COMMAND may be a function, which runs in the job's own subshell
#   pool_wait                      wait for every job left
#   pool_failed                    print each failed job's ID, in start order
#   pool_print_failed              print each failed job as "FAIL ID (exit CODE):" and its output, indented
#
# Its interface also has these globals, which callers read and never set:
#   pool_ids        the IDs in start order; an ID is in it once pool_run started it
#   pool_file[ID]   the job's output file; set once pool_run started it
#   pool_code[ID]   the job's exit code, and pool_secs[ID] its seconds; set for an ID from its ON_DONE call on, and for
#                   every ID after pool_wait
# A job fails on any code but 0: its own, a crash or a kill (128 + the signal). A job whose code bash no longer knows
# gets 127, so it fails rather than hanging the pool or passing. Each job writes only its own file, so jobs running
# together can't interleave their output and hide a FAIL line.
#
# Run globals: none of check.sh's. Reads EPOCHREALTIME, METAPHOR_TEST_JOBS, METAPHOR_CHECK_SLOTS, METAPHOR_CHECK_LOCK,
# METAPHOR_CHECK_HELD (run.sh exports it) and TMPDIR. Everything else named pool_* (pool_began, pool_running,
# pool_dir, pool_jobs, pool_on_done and the functions pool_wait_one, pool_track and pool_finished) is private to it.

declare -gA pool_code=() pool_secs=() pool_file=() pool_began=() pool_running=()
pool_ids=() pool_dir="" pool_jobs=1 pool_on_done=""

pool_default_jobs() {
  local jobs="${METAPHOR_TEST_JOBS:-}" slots="${METAPHOR_CHECK_SLOTS:-2}" base lock pid n runs=1
  if [[ "$jobs" =~ ^[1-9][0-9]*$ ]]; then echo "$jobs" && return 0; fi
  [[ "$slots" =~ ^[1-9][0-9]*$ ]] || slots=2
  # The slots as take_lock (scripts/check/run.sh) names them. This run counts once, holding a slot or not; each slot
  # another live run holds adds one. A slot whose holder has exited is free; one with no pid yet is being taken.
  base="${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}"
  for ((n = 1; n <= slots; n++)); do
    lock="$base"
    [ "$n" -eq 1 ] || lock="$base.$n"
    [ -d "$lock" ] && [ "$lock" != "${METAPHOR_CHECK_HELD:-}" ] || continue
    pid=""
    { read -r pid < "$lock/pid"; } 2> /dev/null || true
    if [ -z "$pid" ] || kill -0 "$pid" 2> /dev/null; then runs=$((runs + 1)); fi
  done
  jobs="$(nproc 2> /dev/null || echo 2)"
  [[ "$jobs" =~ ^[1-9][0-9]*$ ]] || jobs=2
  [ "$jobs" -le 8 ] || jobs=8
  jobs=$((jobs / runs))
  echo "$((jobs > 0 ? jobs : 1))"
}

pool_init() {
  [[ "$2" =~ ^[1-9][0-9]*$ ]] || { echo "pool: JOBS must be a whole number above 0, not '$2'" >&2; return 2; }
  [ -d "$1" ] || { echo "pool: no folder $1" >&2; return 2; }
  pool_dir="$1" pool_jobs="$2" pool_on_done="${3:-}"
  pool_code=() pool_secs=() pool_file=() pool_began=() pool_running=() pool_ids=()
}

pool_run() {
  local id="$1"
  shift
  while [ "${#pool_running[@]}" -ge "$pool_jobs" ]; do pool_wait_one; done
  # Files by start number: an ID may be a path or hold spaces.
  pool_file[$id]="$pool_dir/${#pool_ids[@]}.out"
  pool_ids+=("$id")
  pool_began[$id]=$EPOCHREALTIME
  "$@" > "${pool_file[$id]}" 2>&1 &
  pool_track "$!" "$id"
}

# pool_track PID ID (private): note a started job. A PID still listed belongs to a job that ended unseen (bash dropped
# it, killed) and whose PID the system has given out again: that job's code is gone, so it fails with 127.
pool_track() {
  [ -z "${pool_running[$1]+set}" ] || pool_finished "$1" 127
  pool_running[$1]="$2"
}

pool_wait() {
  while [ "${#pool_running[@]}" -gt 0 ]; do pool_wait_one; done
}

# pool_wait_one (private): wait for any running job and note it. `wait -n` misses a job bash has already cleaned up:
# one killed by a signal (bash reports "Killed" and drops it from its job list) or one something else waited for. With
# nothing left to wait for while jobs are still listed, each listed job's code comes from `wait PID`, which bash keeps
# for a job it cleaned up, and is 127 for one it doesn't know.
pool_wait_one() {
  local pid="" code
  wait -n -p pid # with nothing to wait for, it leaves pid unset
  code=$?
  if [ -z "${pid:-}" ]; then
    for pid in "${!pool_running[@]}"; do
      wait "$pid"
      pool_finished "$pid" "$?"
    done
  elif [ -n "${pool_running[$pid]+set}" ]; then
    pool_finished "$pid" "$code"
  fi
}

pool_finished() { # pool_finished PID CODE (private): record the job's code and time, then run ON_DONE
  local id="${pool_running[$1]}" tenths
  unset "pool_running[$1]"
  # EPOCHREALTIME is "seconds.microseconds" (a comma in some locales): tenths of a second from its digits.
  tenths=$(((10#${EPOCHREALTIME//[.,]/} - 10#${pool_began[$id]//[.,]/}) / 100000))
  pool_code[$id]="$2" pool_secs[$id]="$((tenths / 10)).$((tenths % 10))"
  [ -z "$pool_on_done" ] || "$pool_on_done" "$id" "$2" "${pool_secs[$id]}"
}

pool_failed() {
  local id
  for id in "${pool_ids[@]}"; do [ "${pool_code[$id]}" = 0 ] || echo "$id"; done
}

pool_print_failed() {
  local id line
  for id in "${pool_ids[@]}"; do
    [ "${pool_code[$id]}" != 0 ] || continue
    echo "FAIL $id (exit ${pool_code[$id]}):"
    while IFS= read -r line || [ -n "$line" ]; do echo "  $line"; done < "${pool_file[$id]}"
  done
}
