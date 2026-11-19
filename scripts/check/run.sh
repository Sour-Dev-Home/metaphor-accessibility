# shellcheck shell=bash
# check.sh's run bookkeeping (ADR 0013): the pass cache, the machine-wide build slots and their locks, step timing, the
# run log, the live-log hooks and `finish`, the EXIT trap every stage leaves through. Sourced by scripts/check.sh only;
# it uses die from report.sh, and live_line and live_end from scripts/check/live-log.sh once full has loaded it.
#
# Run globals, all set by check.sh's main each run: reads `stage`, `tmp`, `started`, `reused` (main also sets it to
# reused), `run_logged`, `timed` and `live_log`; appends to `step_times`; sets `locked` and `waited`, which main resets,
# and exports METAPHOR_CHECK_HELD, the slot it holds.
# `check_dir` is check.sh's constant: `timing_script` is computed from it when this file is sourced, so check.sh sets
# check_dir before loading it. Reads SKIP_SCRIPT_TESTS, METAPHOR_CHECK_LOCK, METAPHOR_CHECK_SLOTS, METAPHOR_CHECK_POLL
# and TMPDIR.

# The pass cache (issue #241): "<stage> <tree>" lines in this clone's git dir, never a shared path. A pass is reused only
# for the identical tree, so any change, staged or committed, runs the checks again. A `full` pass also covers pre-push.
pass_cache() { echo "$(git rev-parse --absolute-git-dir 2> /dev/null)/check-pass-cache"; }
cached_pass() { # cached_pass TREE STAGE...: exit 0 when one of the stages passed on TREE
  local tree="$1" kind file
  shift
  file="$(pass_cache)"
  [ -f "$file" ] || return 1
  for kind in "$@"; do grep -qxF "$kind $tree" "$file" && return 0; done
  return 1
}
record_pass() { # record_pass STAGE TREE: keep the last 50 passes
  local file
  file="$(pass_cache)"
  { echo "$1 $2"; cat "$file" 2> /dev/null || true; } | head -n 50 > "$file.new" && mv "$file.new" "$file"
}
# full_kinds: print the kind this full records, then the kinds it may reuse. A full with SKIP_SCRIPT_TESTS=1 (CI's,
# whose own jobs run the script tests) records full-skipped, which serves only another skipped run, never a plain full.
full_kinds() {
  if [ -n "${SKIP_SCRIPT_TESTS:-}" ]; then echo "full-skipped full"; else echo full; fi
}
# full_tree: print the index's tree, only when the working tree matches it (the build reads the working tree).
full_tree() {
  git diff --quiet 2> /dev/null && git write-tree 2> /dev/null
}

# Stages with a budget from issue #1 print their duration on exit, and a warning when over budget (stage-timing.sh).
timing_script="$check_dir/check/stage-timing.sh"
current_branch() {
  local branch
  branch="$(git branch --show-current 2> /dev/null)"
  echo "${branch:-detached}"
}
# The run log (issue #269): one line per building run in this clone's git dir, newest first, the last 50.
# "<stage> <branch> <start> <end> <pass|fail> <built|reused>"; read by scripts/check/check-status.sh.
# Per-step times of a building stage (#314): "restore=12s build=80s ...", printed when the run ends and kept as the run
# log line's last field, so a slow step is named by data.
timed_step() { # timed_step NAME COMMAND...: run COMMAND and note how long it took; its exit code is returned
  local name="$1" began=$SECONDS code
  shift
  "$@"
  code=$?
  step_times+=("$name=$((SECONDS - began))s")
  # full's steps go to the live log (scripts/check/live-log.sh) as they finish.
  [ -z "$live_log" ] || live_line "$name" "$code" "$((SECONDS - began))"
  return "$code"
}
log_run() { # log_run EXIT_CODE
  local file result=pass times="" from to
  [ "$1" -eq 0 ] || result=fail
  [ "${#step_times[@]}" -eq 0 ] || times=" ${step_times[*]}"
  file="$(git rev-parse --absolute-git-dir 2> /dev/null)/check-run-log" || return 0
  [ "$file" != "/check-run-log" ] || return 0
  TZ=UTC0 printf -v from '%(%Y-%m-%dT%H:%M:%SZ)T' "$started"
  TZ=UTC0 printf -v to '%(%Y-%m-%dT%H:%M:%SZ)T' -1
  {
    echo "$stage $(current_branch) $from $to $result $reused$times"
    cat "$file" 2> /dev/null || true
  } | head -n 50 > "$file.new" && mv "$file.new" "$file"
}
finish() {
  local code=$?
  rm -rf "${tmp:-}"
  # github-outage.sh's classify matches this line's start (#321).
  [ "${#step_times[@]}" -eq 0 ] || echo "step times: ${step_times[*]}"
  [ -z "$run_logged" ] || log_run "$code" || true
  [ -z "${waited:-}" ] || rm -f "${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}.waiting/$BASHPID"
  [ -z "${locked:-}" ] || rm -rf "$locked"
  [ -z "$live_log" ] || live_end "$code"
  [ "${#timed[@]}" -eq 0 ] ||
    bash "$timing_script" report "${timed[0]}" "${timed[1]}" "$started" "$EPOCHSECONDS" \
      "${timed[@]:2}"
  exit "$code"
}

# Stages that build take turns through one lock per machine, with no shared MSBuild nodes: several worktrees building
# at once stalled each other for over an hour (2026-11-17). METAPHOR_CHECK_SLOTS (default 2) runs may build at once, one
# lock directory each; a slot whose holder has exited is taken over (#281).
take_lock() {
  [ -z "${locked:-}" ] || return 0
  export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
  local base lock holder slots n
  base="${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}"
  slots="${METAPHOR_CHECK_SLOTS:-2}"
  [[ "$slots" =~ ^[1-9][0-9]*$ ]] || slots=2
  lock=""
  while [ -z "$lock" ]; do
    # Slot 1 is the original lock path (check-status.sh and older tools read it); slot N is "<path>.N".
    for ((n = 1; n <= slots; n++)); do
      lock="$base"
      [ "$n" -eq 1 ] || lock="$base.$n"
      if mkdir "$lock" 2> /dev/null; then break; fi
      holder="$(cat "$lock/pid" 2> /dev/null || true)"
      if [ -n "$holder" ] && ! kill -0 "$holder" 2> /dev/null; then
        rm -rf "$lock"
        if mkdir "$lock" 2> /dev/null; then break; fi
      fi
      lock=""
    done
    [ -z "$lock" ] || break
    # The pre-push hook must not wait: GitHub closes an idle push connection and git exits 141 (#284).
    # github-outage.sh's classify matches "every build slot is busy", "waiting for another build check" and "step
    # times:" to call a push cut after them a real failure, not an outage (#321): keep that wording in step.
    [ "${1:-}" != nowait ] || die "every build slot is busy (see scripts/check/check-status.sh), so this push would wait with its connection open and be cut. Run 'bash scripts/check.sh full' first, then push: the hook reuses the recorded pass."
    if [ -z "${waited:-}" ]; then
      echo "check: waiting for another build check to finish ($base, $slots slots)" >&2
      local now
      printf -v now '%(%s)T' -1
      mkdir -p "$base.waiting" 2> /dev/null && echo "$(current_branch) $now" > "$base.waiting/$BASHPID"
    fi
    waited=1
    sleep "${METAPHOR_CHECK_POLL:-5}"
  done
  rm -f "$base.waiting/$BASHPID"
  # scripts/check/check-status.sh reads these; pid goes last, since a lock with no pid yet is never taken over. $BASHPID is $$
  # in a run of the script, and the subshell's own pid when the tests run main in one.
  current_branch > "$lock/branch"
  echo "$EPOCHSECONDS" > "$lock/since"
  echo "$BASHPID" > "$lock/pid"
  locked="$lock"
  # The runners this run starts size their worker pools by the slots others hold (pool_default_jobs, pool.sh).
  export METAPHOR_CHECK_HELD="$lock"
}
