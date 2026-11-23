#!/usr/bin/env bash
# Removes the agent worktrees that are safe to remove (issue #801). Only worktrees under <repo>/.claude/worktrees/ are
# considered, and a worktree goes only when all of these hold:
#   - it is clean (no changes, no untracked files) and not locked, and isn't the one you run this from; and
#   - its HEAD is reachable from origin/main, or contained in a remote branch, or it is the head commit of a merged PR
#     (`gh pr list --state merged`, matched on branch name and commit, so a squash merge counts).
# Removal is plain `git worktree remove` (never forced), then `git worktree prune`. Nothing else is written: it doesn't
# fetch, so run `git fetch origin` first for a current view of origin.
#
# Usage: prune-worktrees.sh [--dry-run]
# Prints one line per worktree (by folder name) and a summary with counts: removed, kept and why.
set -uo pipefail

dry=0
case "${1:-}" in
  '') ;;
  --dry-run) dry=1 ;;
  *) echo "usage: prune-worktrees.sh [--dry-run]" >&2; exit 2 ;;
esac
[ "$#" -le 1 ] || { echo "usage: prune-worktrees.sh [--dry-run]" >&2; exit 2; }

common="$(git rev-parse --path-format=absolute --git-common-dir)" || { echo "not in a git repository" >&2; exit 1; }
common="${common//$'\r'/}"
main_root="$(dirname "$common")"
base="$main_root/.claude/worktrees"
git rev-parse --verify --quiet origin/main >/dev/null || { echo "no origin/main: run git fetch origin" >&2; exit 1; }
here="$(git rev-parse --show-toplevel)"
here="${here//$'\r'/}"

# Merged PR heads as "branch<TAB>commit" lines. If gh can't answer, only the two git rules apply.
merged=""
if merged="$(gh pr list --state merged --limit 300 --json headRefName,headRefOid --jq '.[] | [.headRefName, .headRefOid] | @tsv' 2>/dev/null)"; then
  merged="${merged//$'\r'/}"
else
  merged=""
  echo "warning: gh could not list merged PRs; only commits on origin are checked" >&2
fi

removed=0 kept_dirty=0 kept_unmerged=0 kept_locked=0 kept_current=0 kept_failed=0

consider() { # consider PATH HEAD BRANCH LOCKED
  local path="$1" head="$2" branch="$3" locked="$4" name
  [ -n "$path" ] || return 0
  case "$path" in "$base"/*) ;; *) return 0 ;; esac # nothing outside .claude/worktrees
  name="${path#"$base"/}"
  [ -d "$path" ] || return 0 # already gone: `git worktree prune` clears the record
  if [ "$path" = "$here" ]; then echo "kept $name (current worktree)"; kept_current=$((kept_current + 1)); return 0; fi
  if [ -n "$locked" ]; then echo "kept $name (locked)"; kept_locked=$((kept_locked + 1)); return 0; fi
  if [ -n "$(git -C "$path" status --porcelain --untracked-files=all 2>/dev/null)" ]; then
    echo "kept $name (dirty)"; kept_dirty=$((kept_dirty + 1)); return 0
  fi
  local ok=0
  if git merge-base --is-ancestor "$head" origin/main 2>/dev/null; then ok=1
  elif [ -n "$(git branch -r --contains "$head" 2>/dev/null)" ]; then ok=1
  elif [ -n "$branch" ] && grep -qxF "$branch"$'\t'"$head" <<<"$merged"; then ok=1
  fi
  if [ "$ok" -eq 0 ]; then echo "kept $name (unmerged)"; kept_unmerged=$((kept_unmerged + 1)); return 0; fi
  if [ "$dry" -eq 1 ]; then echo "would remove $name"; removed=$((removed + 1)); return 0; fi
  if git worktree remove "$path" 2>/dev/null; then echo "removed $name"; removed=$((removed + 1))
  else echo "kept $name (git worktree remove refused)"; kept_failed=$((kept_failed + 1)); fi
}

path="" head="" branch="" locked=""
while IFS= read -r line || [ -n "$line" ]; do
  line="${line//$'\r'/}"
  case "$line" in
    "worktree "*) consider "$path" "$head" "$branch" "$locked"; path="${line#worktree }"; head=""; branch=""; locked="" ;;
    "HEAD "*) head="${line#HEAD }" ;;
    "branch refs/heads/"*) branch="${line#branch refs/heads/}" ;;
    locked*) locked="yes" ;;
  esac
done < <(git worktree list --porcelain)
consider "$path" "$head" "$branch" "$locked"

[ "$dry" -eq 1 ] || git worktree prune
verb=removed
[ "$dry" -eq 1 ] && verb="would remove"
kept=$((kept_dirty + kept_unmerged + kept_locked + kept_current + kept_failed))
echo "$verb $removed, kept $kept (dirty $kept_dirty, unmerged $kept_unmerged, locked $kept_locked, current $kept_current, refused $kept_failed)"
