# shellcheck shell=bash
# check.sh's run bookkeeping (ADR 0013): the pass cache, the machine-wide build slots and their locks, step timing, the
# run log, the live-log hooks and `finish`, the EXIT trap every stage leaves through. Sourced by scripts/check.sh only;
# it uses die from report.sh, and live_line and live_end from scripts/check/live-log.sh once full has loaded it.
#
# Run globals, all set by check.sh's main each run: reads `stage`, `tmp`, `started`, `reused` (main also sets it to
# reused), `run_logged`, `timed` and `live_log`; appends to `step_times` and `failed_steps`; sets `locked` and `waited`, which main resets,
# and exports METAPHOR_CHECK_HELD, the slot it holds.
# `check_dir` is check.sh's constant: `timing_script` is computed from it when this file is sourced, so check.sh sets
# check_dir before loading it. Reads SKIP_SCRIPT_TESTS, METAPHOR_CHECK_LOCK, METAPHOR_CHECK_SLOTS, METAPHOR_CHECK_POLL
# and TMPDIR. Private to this module: git_local_env, which timed_step fills once.

# The pass cache (issue #241): "<stage> <tree>" lines in this clone's git dir, never a shared path. A pass is reused only
# for the identical tree, so any change, staged or committed, runs the checks again. A `full` pass also covers pre-push.
# A merge's pass (merge.sh, #728) adds "merge <parent tree|-> <parent tree|->": the parent passes it relied on.
pass_cache() { echo "$(git rev-parse --absolute-git-dir 2> /dev/null)/check-pass-cache"; }
cached_pass() { # cached_pass TREE STAGE...: exit 0 when one of the stages passed on TREE
  local tree="$1" kind file line
  shift
  file="$(pass_cache)"
  [ -f "$file" ] || return 1
  # Builtins: the file holds at most 50 short lines, and a grep per kind was a process start each.
  while IFS= read -r line; do
    for kind in "$@"; do [[ "$line" != "$kind $tree" && "$line" != "$kind $tree "* ]] || return 0; done
  done < "$file"
  return 1
}
record_pass() { # record_pass STAGE TREE [NOTE...]: keep the last 50 passes
  local file
  file="$(pass_cache)"
  { echo "$*"; cat "$file" 2> /dev/null || true; } | head -n 50 > "$file.new" && mv "$file.new" "$file"
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
# The run log (issue #269): one line per building run in this worktree's git dir, newest first, the last 50; each line also
# goes to the common git dir's check-run-history, the last 500 (#747).
# "<stage> <branch> <start> <end> <pass|fail> <built|reused> [steps] [failed=a,b]"; read by scripts/check/check-status.sh.
# Per-step times of a building stage (#314): "restore=12s build=80s ...", printed when the run ends and kept as the run
# log line's last field, so a slow step is named by data.
# timed_step NAME COMMAND...: run COMMAND and note how long it took; its exit code is returned. COMMAND runs without
# git's per-repository variables (`git rev-parse --local-env-vars`: GIT_DIR, GIT_INDEX_FILE, GIT_WORK_TREE, ...), which
# git sets for a hook: inherited, they pointed a test's `git -C <scratch> init` at this clone, which it made bare. The
# list also holds GIT_CONFIG_PARAMETERS, so `git -c` overrides don't reach steps either. Every test step runs through
# here, so the hooks need no other guard. The subshell's exec costs no extra process start.
timed_step() {
  local name="$1" began=$SECONDS code
  shift
  [ -n "${git_local_env:-}" ] ||
    git_local_env="$(git rev-parse --local-env-vars 2> /dev/null)" ||
    git_local_env="GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE GIT_PREFIX GIT_COMMON_DIR GIT_OBJECT_DIRECTORY"
  # shellcheck disable=SC2086 # one variable name per word
  (unset $git_local_env && exec "$@")
  code=$?
  step_times+=("$name=$((SECONDS - began))s")
  [ "$code" -eq 0 ] || failed_steps+=("$name")
  # full's steps go to the live log (scripts/check/live-log.sh) as they finish.
  [ -z "$live_log" ] || live_line "$name" "$code" "$((SECONDS - began))"
  return "$code"
}
# prepend_bounded FILE LINE MAX: put LINE first in FILE and keep MAX lines. Runs from worktrees at once, so the
# read-modify-write holds a mkdir lock (FILE.lock, with the holder's pid; a holder that exited is taken over, the same
# as take_lock) and writes through a per-process temp file. Gives up after about 10 s: a log line is never worth a hang.
prepend_bounded() {
  local file="$1" line="$2" max="$3" lock="$1.lock" tmpfile="$1.new.$BASHPID" tries=0 holder
  until mkdir "$lock" 2> /dev/null; do
    holder="$(cat "$lock/pid" 2> /dev/null || true)"
    if [ -n "$holder" ] && ! kill -0 "$holder" 2> /dev/null; then rm -rf "$lock"; continue; fi
    tries=$((tries + 1))
    [ "$tries" -lt 100 ] || return 0
    sleep 0.1
  done
  echo "$BASHPID" > "$lock/pid"
  {
    echo "$line"
    cat "$file" 2> /dev/null || true
  } | head -n "$max" > "$tmpfile" && mv "$tmpfile" "$file"
  rm -rf "$lock" "$tmpfile"
}
log_run() { # log_run EXIT_CODE
  local gitdir common result=pass times="" failed="" from to line
  [ "$1" -eq 0 ] || result=fail
  [ "${#step_times[@]}" -eq 0 ] || times=" ${step_times[*]}"
  # A failed run names its failing steps (#747); a passing one has no failed= field.
  [ "${#failed_steps[@]}" -eq 0 ] || failed=" failed=$(IFS=,; echo "${failed_steps[*]}")"
  gitdir="$(git rev-parse --absolute-git-dir 2> /dev/null)" || return 0
  [ -n "$gitdir" ] || return 0
  TZ=UTC0 printf -v from '%(%Y-%m-%dT%H:%M:%SZ)T' "$started"
  TZ=UTC0 printf -v to '%(%Y-%m-%dT%H:%M:%SZ)T' -1
  line="$stage $(current_branch) $from $to $result $reused$times$failed"
  prepend_bounded "$gitdir/check-run-log" "$line" 50
  # The history in the common git dir (shared by every worktree, kept after one is removed, #747): the last 500 runs.
  common="$(git rev-parse --path-format=absolute --git-common-dir 2> /dev/null)" || return 0
  [ -z "$common" ] || prepend_bounded "$common/check-run-history" "$line" 500
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
