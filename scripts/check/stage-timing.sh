#!/usr/bin/env bash
# Stage timing (issue #18): how long a stage took, against its budget from issue #1. Over budget is a warning, never a
# failure.
#
#   scripts/check/stage-timing.sh report LABEL BUDGET START END [DETAIL]
#       One stage. START and END are Unix seconds; BUDGET is seconds, or - for none. DETAIL (the commit, the game build)
#       ends the line. scripts/check.sh times stages 1 and 4 with it; the scripts for stages 3 and 5 should too.
#   scripts/check/stage-timing.sh jobs LABEL BUDGET FILE
#       A CI run. FILE holds "<job>\t<started>\t<completed>" per finished job, in UTC ISO 8601 as the GitHub API gives
#       them. Prints a Markdown table of each job's duration, then the stage's line, timed from the first job's start to
#       the last one's finish, so time spent queued before any job starts doesn't count.
#
# In GitHub Actions an over-budget stage is also a ::warning:: annotation on the run's page, and the jobs table and
# stage line are added to the job summary. Exit 0, or 2 on a usage error.
set -uo pipefail

die() {
  echo "stage-timing: $*" >&2
  exit 2
}

is_seconds() { [[ "$1" =~ ^[0-9]+$ ]]; }

# duration_into VAR SECONDS: set VAR to "48 s", "5 min" or "4 min 48 s". A variable, not output, so `report` (which
# check.sh runs on every fast stage) needs no subshell: under Git Bash each one costs a process start (#328).
duration_into() {
  local -n into="$1"
  local min=$(($2 / 60)) sec=$(($2 % 60))
  if [ "$min" -eq 0 ]; then
    into="$sec s"
  elif [ "$sec" -eq 0 ]; then
    into="$min min"
  else
    into="$min min $sec s"
  fi
}
duration() { local text; duration_into text "$1"; echo "$text"; }

# summary LINE: print a line, and add it to the job summary in GitHub Actions.
summary() {
  echo "$1"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then echo "$1" >> "$GITHUB_STEP_SUMMARY"; fi
}

# report LABEL BUDGET START END [DETAIL]
report() {
  local label="$1" budget="$2" start="$3" end="$4" detail="${5:-}" took line
  is_seconds "$budget" || [ "$budget" = - ] || die "budget must be whole seconds or -, not '$budget'"
  is_seconds "$start" && is_seconds "$end" || die "start and end must be Unix seconds"
  [ "$end" -ge "$start" ] || die "end is before start"
  took=$((end - start))
  local from to took_text budget_text
  # UTC ISO 8601 from builtins, where a `date` per time was a start each.
  TZ=UTC0 printf -v from '%(%Y-%m-%dT%H:%M:%SZ)T' "$start"
  TZ=UTC0 printf -v to '%(%Y-%m-%dT%H:%M:%SZ)T' "$end"
  duration_into took_text "$took"
  line="timing: $label started $from, finished $to, took $took_text"
  [ "$budget" = - ] || { duration_into budget_text "$budget"; line+=" (budget $budget_text)"; }
  summary "$line${detail:+, $detail}"
  if [ "$budget" != - ] && [ "$took" -gt "$budget" ]; then
    local message="$label took $(duration "$took"), over its $(duration "$budget") budget"
    echo "warning: $message"
    if [ "${GITHUB_ACTIONS:-}" = true ]; then echo "::warning title=Stage over budget::$message"; fi
  fi
  return 0
}

# time_jobs LABEL BUDGET FILE
time_jobs() {
  local label="$1" budget="$2" file="$3" name started completed from to first="" last=""
  [ -f "$file" ] || die "no jobs file at $file"
  summary "| Job | Duration |"
  summary "|---|---|"
  while IFS=$'\t' read -r name started completed; do
    [ -n "$name" ] || continue
    from="$(date -u -d "$started" +%s 2> /dev/null)" && to="$(date -u -d "$completed" +%s 2> /dev/null)" ||
      die "job '$name' has an unreadable time: '$started' or '$completed'"
    [ "$to" -ge "$from" ] || die "job '$name' finishes before it starts"
    summary "| $name | $(duration $((to - from))) |"
    if [ -z "$first" ] || [ "$from" -lt "$first" ]; then first="$from"; fi
    if [ -z "$last" ] || [ "$to" -gt "$last" ]; then last="$to"; fi
  done < <(tr -d '\r' < "$file")
  summary ""
  if [ -z "$first" ]; then
    summary "timing: $label has no finished jobs to time"
    return 0
  fi
  report "$label" "$budget" "$first" "$last"
}

case "${1:-}" in
  report)
    [ "$#" -eq 5 ] || [ "$#" -eq 6 ] || die "usage: stage-timing.sh report LABEL BUDGET START END [DETAIL]"
    report "${@:2}" ;;
  jobs)
    [ "$#" -eq 4 ] || die "usage: stage-timing.sh jobs LABEL BUDGET FILE"
    is_seconds "$3" || [ "$3" = - ] || die "budget must be whole seconds or -, not '$3'"
    time_jobs "${@:2}" ;;
  *) die "usage: stage-timing.sh report LABEL BUDGET START END [DETAIL] | jobs LABEL BUDGET FILE" ;;
esac
