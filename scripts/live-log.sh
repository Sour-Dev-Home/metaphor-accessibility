#!/usr/bin/env bash
# The live log (#328): long runs (`check.sh full`, tests/check/test-check.sh, scripts/run-pester.sh) write their
# progress to one fixed file in the clone's git dir, check-live-log, so it can be followed while they run
# (`tail -f "$(git rev-parse --absolute-git-dir)/check-live-log"`) and scripts/check-status.sh can show it. Sourced;
# it defines functions only, and they use builtins alone past live_start.
#
#   == start <suite> <total> <unix start> <pid>      a run began (a run inside another, as full's Pester, appends)
#   [<done>/<total>] <item> ok|FAIL (<seconds> s)    an item finished
#   == end <suite> <exit code> <seconds>             the run ended
#
# Nothing in it is game content or personal information: suite and item names, counts and times.

live_log="" live_suite="" live_total=0 live_done=0 live_began=0

# live_start SUITE TOTAL [DIR]: start a run of TOTAL items in the clone holding DIR (default: the current folder). A run
# started from inside another one's process tree (METAPHOR_LIVE_LOG, which this exports) appends to its log.
live_start() {
  local gitdir
  live_suite="$1" live_total="$2" live_done=0 live_began=$EPOCHSECONDS
  if [ -n "${METAPHOR_LIVE_LOG:-}" ] && [ -f "$METAPHOR_LIVE_LOG" ]; then
    live_log="$METAPHOR_LIVE_LOG"
  else
    gitdir="$(git -C "${3:-.}" rev-parse --absolute-git-dir 2> /dev/null)" || { live_log=""; return 0; }
    live_log="$gitdir/check-live-log"
    : > "$live_log" 2> /dev/null || { live_log=""; return 0; }
  fi
  export METAPHOR_LIVE_LOG="$live_log"
  # A suite run in WSL (scripts/wsl-run.sh) logs the pid of the Git Bash shell waiting for it, which check-status.sh
  # can see; the runs a test case starts log their own.
  printf '== start %s %s %s %s\n' "$live_suite" "$live_total" "$live_began" "${METAPHOR_LIVE_PID:-$BASHPID}" \
    >> "$live_log"
  unset METAPHOR_LIVE_PID
}

# live_line ITEM EXIT_CODE SECONDS: print and log "[done/total] ITEM ok|FAIL (SECONDS s)".
live_line() {
  local result=ok line
  [ "$2" -eq 0 ] || result=FAIL
  live_done=$((live_done + 1))
  line="[$live_done/$live_total] $1 $result ($3 s)"
  echo "$line"
  [ -z "$live_log" ] || echo "$line" >> "$live_log"
}

# live_end EXIT_CODE: log the run's end.
live_end() {
  [ -n "$live_log" ] || return 0
  printf '== end %s %s %s\n' "$live_suite" "$1" "$((EPOCHSECONDS - live_began))" >> "$live_log"
  live_log=""
}
