#!/usr/bin/env bash
# Black-box tests for scripts/dev/prune-worktrees.sh in a temporary repository with a bare "origin" and a fake `gh` that
# serves the merged PRs from $FIX/merged (already in the shape the script's filter produces: branch, tab, commit).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$ROOT/scripts/dev/prune-worktrees.sh"

mkdir -p "$WORK/bin" "$WORK/fix"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
case "$1 $2" in
  "pr list") cat "$FIX/merged" ;;
  *) exit 1 ;;
esac
FAKE
chmod +x "$WORK/bin/gh"
export FIX="$WORK/fix"
: > "$FIX/merged"

passed=0
failed=0
check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

g() { git -c user.name=t -c user.email=t@example.invalid -c commit.gpgsign=false -c core.hooksPath=/dev/null "$@"; }

# A repository with origin/main, and agent worktrees under .claude/worktrees (the `from` branch point is main).
REPO="$WORK/repo"
git init -q --bare "$WORK/origin.git"
git init -q -b main "$REPO"
cd "$REPO" || exit 1
echo base > file.txt
g add file.txt
g commit -q -m base
git remote add origin "$WORK/origin.git"
g push -q origin main
git fetch -q origin

new_wt() { g worktree add -q -b "$1" ".claude/worktrees/$1" main; } # new_wt NAME: a worktree on a new branch at main
commit_in() { echo "$1" > ".claude/worktrees/$2/$1.txt"; g -C ".claude/worktrees/$2" add "$1.txt"; g -C ".claude/worktrees/$2" commit -q -m "$1"; }
exists() { [ -d "$REPO/.claude/worktrees/$1" ]; }

new_wt on-main # HEAD is origin/main's: removed
new_wt on-remote # a commit pushed to a remote branch: removed
commit_in remote-work on-remote
g -C .claude/worktrees/on-remote push -q origin on-remote
new_wt merged-pr # an unpushed commit whose branch and commit are a merged PR's head (a squash merge): removed
commit_in pr-work merged-pr
printf 'merged-pr\t%s\n' "$(git -C .claude/worktrees/merged-pr rev-parse HEAD)" > "$FIX/merged"
new_wt pr-moved-on # the PR merged, but the branch has a commit past the PR's head: kept as unmerged
commit_in pr-head pr-moved-on
printf 'merged-pr\t%s\npr-moved-on\t%s\n' "$(git -C .claude/worktrees/merged-pr rev-parse HEAD)" "$(git -C .claude/worktrees/pr-moved-on rev-parse HEAD)" > "$FIX/merged"
commit_in later pr-moved-on
new_wt dirty-file # a changed tracked file: kept
echo changed > .claude/worktrees/dirty-file/file.txt
new_wt untracked # an untracked file: kept
echo new > .claude/worktrees/untracked/new.txt
new_wt unmerged # a commit nobody has: kept
commit_in lonely unmerged
new_wt locked-wt # clean and merged, but locked: kept
g worktree lock .claude/worktrees/locked-wt
g worktree add -q -b outside "$WORK/outside" main # merged, but not under .claude/worktrees: never touched

out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" --dry-run 2>&1)"
status=$?
check "dry run exits 0" test "$status" -eq 0
check "dry run says what it would remove" grep -qx 'would remove on-main' <<<"$out"
check "dry run removes nothing" bash -c 'cd "$1" && [ "$(git worktree list | wc -l)" -eq 10 ]' _ "$REPO"

out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"
status=$?
check "it exits 0" test "$status" -eq 0
check "a worktree on origin/main is removed" bash -c '! test -d "$1"' _ "$REPO/.claude/worktrees/on-main"
check "a commit in a remote branch is removed" bash -c '! test -d "$1"' _ "$REPO/.claude/worktrees/on-remote"
check "a merged PR's head is removed" bash -c '! test -d "$1"' _ "$REPO/.claude/worktrees/merged-pr"
check "a PR branch with later commits is kept" exists pr-moved-on
check "a changed file keeps it" exists dirty-file
check "an untracked file keeps it" exists untracked
check "an unmerged commit keeps it" exists unmerged
check "a locked worktree is kept" exists locked-wt
check "a worktree outside .claude/worktrees is untouched" test -d "$WORK/outside"
check "it reports the removals" grep -qx 'removed merged-pr' <<<"$out"
check "it names the reason for each kept one" bash -c 'grep -qx "kept dirty-file (dirty)" <<<"$1" && grep -qx "kept untracked (dirty)" <<<"$1" && grep -qx "kept unmerged (unmerged)" <<<"$1" && grep -qx "kept pr-moved-on (unmerged)" <<<"$1" && grep -qx "kept locked-wt (locked)" <<<"$1"' _ "$out"
check "the summary has the counts" grep -qx 'removed 3, kept 5 (dirty 2, unmerged 2, locked 1, current 0, refused 0)' <<<"$out"
check "the record of a removed worktree is gone" bash -c '! git worktree list --porcelain | grep -q "on-main"'
check "it never forces a removal" bash -c '! grep -Eq "worktree (remove|prune).* (-f|--force)" "$1"' _ "$SCRIPT"

out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"
check "a second run removes nothing more" grep -q '^removed 0, kept 5' <<<"$out"

PATH="$WORK/bin:$PATH" bash "$SCRIPT" --force >/dev/null 2>&1; code=$?
check "an unknown option is refused" test "$code" -eq 2

echo "prune-worktrees: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
