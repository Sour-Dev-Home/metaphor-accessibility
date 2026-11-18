#!/usr/bin/env bash
# A local view of the build checks (issue #269): who holds the build lock, who waits on it, each worktree's last runs
# and idle build workers. `--short` prints one line for the Claude Code status line, and reads only the lock files.
#
#   bash scripts/check-status.sh [--short]
#
# The lock and its waiters are written by take_lock in scripts/check.sh; run logs by its `finish`. Nothing here is
# game content or personal information: a stage, a branch name, times and a result.
set -uo pipefail

lock="${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}"
# Bash builtins only on the --short path: a fork costs hundreds of ms under Git Bash on Windows, and the status line
# runs this often.
printf -v now '%(%s)T' -1
now="${METAPHOR_CHECK_NOW:-$now}"
short=0
[ "${1:-}" != "--short" ] || short=1

alive() { kill -0 "$1" 2> /dev/null; }

holder="" branch="" since=""
if [ -d "$lock" ]; then
  read -r holder < "$lock/pid" 2> /dev/null || true
  read -r branch < "$lock/branch" 2> /dev/null || true
  read -r since < "$lock/since" 2> /dev/null || true
  : "${branch:=unknown}"
fi
held_min=0
[ -z "$since" ] || held_min=$(((now - since) / 60))

waiting=0 longest=0
for w in "$lock.waiting"/*; do
  [ -f "$w" ] || continue
  alive "${w##*/}" || continue
  waiting=$((waiting + 1))
  read -r _ wsince < "$w" || true
  [ -z "${wsince:-}" ] || [ $(((now - wsince) / 60)) -le "$longest" ] || longest=$(((now - wsince) / 60))
done

if [ "$short" -eq 1 ]; then
  if [ ! -d "$lock" ]; then line="lock: free"
  elif [ -n "$holder" ] && ! alive "$holder"; then line="lock: stale (pid $holder gone)"
  else line="lock: $branch · ${held_min}m"; fi
  [ "$waiting" -eq 0 ] || line="$line · $waiting waiting"
  echo "$line"
  exit 0
fi

if [ ! -d "$lock" ]; then
  echo "lock: free"
elif [ -n "$holder" ] && ! alive "$holder"; then
  echo "lock: stale, holder pid $holder has exited; the next check takes it over"
else
  echo "lock: held by pid ${holder:-?} on $branch for $held_min min"
fi
if [ "$waiting" -gt 0 ]; then echo "waiting: $waiting (longest $longest min)"; else echo "waiting: none"; fi

echo "last runs (stage branch start end result built|reused):"
git worktree list --porcelain 2> /dev/null | sed -n 's/^worktree //p' | while IFS= read -r wt; do
  gd="$(git -C "$wt" rev-parse --absolute-git-dir 2> /dev/null)" || continue
  [ -f "$gd/check-run-log" ] || continue
  echo "  $(basename "$wt"):"
  head -n 3 "$gd/check-run-log" | sed 's/^/    /'
done

# Build workers left running while no check holds the lock are idle. ps -W lists Windows processes under Git Bash.
ps_cmd="${METAPHOR_CHECK_PS_CMD:-ps -W}"
workers="$($ps_cmd 2> /dev/null | grep -ciE 'msbuild|vbcscompiler|dotnet.*(build-server|MSBuild)')" || true
if [ "${workers:-0}" -gt 0 ] && [ ! -d "$lock" ]; then
  echo "idle build workers: $workers; shut them down with: dotnet build-server shutdown"
fi
exit 0
