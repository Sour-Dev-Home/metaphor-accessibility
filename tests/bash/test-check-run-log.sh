#!/usr/bin/env bash
# Black-box tests for check.sh's run log (issue #747): a failing run names its failing steps, a linked worktree's run
# reaches the common git dir's history, and both logs stay bounded under concurrent runs. A stand-in `dotnet` fails
# when its first argument (restore, build, test, format) is in DOTNET_FAIL_ON.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$WORK/gate"
cp -r "$ROOT/scripts" "$WORK/gate/"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/gate/scripts/setup/install-docs-tools.sh"
CHECK="$WORK/gate/scripts/check.sh"

passed=0
failed=0

mkdir -p "$WORK/bin"
cat > "$WORK/bin/dotnet" <<'STUB'
#!/usr/bin/env bash
case ",${DOTNET_FAIL_ON:-}," in *",$1,"*) exit 1 ;; esac
exit 0
STUB
chmod +x "$WORK/bin/dotnet"
export PATH="$WORK/bin:$PATH" SKIP_SCRIPT_TESTS=1
export METAPHOR_CHECK_LOCK="$WORK/lock"

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}
like() { [[ "$1" == $2 ]]; }
unlike() { [[ "$1" != $2 ]]; }
first_line() { head -n 1 "$1" 2> /dev/null; }
run() {
  out="$(bash "$CHECK" "$@" 2>&1 <<< "")"
  code=$?
  rm -rf "$WORK/lock"
}

rm -rf "$WORK/repo" && mkdir -p "$WORK/repo" && cd "$WORK/repo" || exit 2
git init -q -b main
git config user.name test && git config user.email test@example.invalid
git config core.autocrlf false && git config commit.gpgsign false
: > Metaphor.sln
echo one > notes.md
mkdir src && echo 'class A {}' > src/a.cs
git add -A && git commit -q -m "chore: seed"
git update-ref refs/remotes/origin/main HEAD
log="$(git rev-parse --absolute-git-dir)/check-run-log"
common="$(git rev-parse --absolute-git-dir)/check-run-history"

# A failing step is named, last on the line; a passing run has no failed= field.
DOTNET_FAIL_ON=build,format run full
check "failing full fails" [ "$code" -eq 1 ]
check "the line names the failing steps in order" like "$(first_line "$log")" "* fail * failed=build,format"
check "the line still holds the step times" like "$(first_line "$log")" "*restore=*failed=*"
DOTNET_FAIL_ON= run full
check "passing full passes" [ "$code" -eq 0 ]
check "a pass logs no failed=" unlike "$(first_line "$log")" "*failed=*"
check "the log names no path" unlike "$(cat "$log")" "*/*"

# The common history: the main clone's runs, and a linked worktree's, which survive its removal.
check "main clone runs are in the history" [ "$(grep -c '^full main ' "$common")" -ge 2 ]
git worktree add -q -b wt-branch "$WORK/wt" HEAD
echo two >> "$WORK/wt/notes.md"
git -C "$WORK/wt" add notes.md
wtlog="$(git -C "$WORK/wt" rev-parse --absolute-git-dir)/check-run-log"
(cd "$WORK/wt" && DOTNET_FAIL_ON=test bash "$CHECK" full > /dev/null 2>&1 <<< "")
rm -rf "$WORK/lock"
check "worktree run is in the worktree's own log" like "$(first_line "$wtlog")" "full wt-branch *"
check "worktree run reaches the common history" like "$(first_line "$common")" "full wt-branch * fail *failed=tests"
git worktree remove --force "$WORK/wt"
check "the history outlives the worktree" like "$(first_line "$common")" "full wt-branch *"

# Bounded: oversized logs are cut.
for i in $(seq 1 700); do echo "full old-$i 2026-11-17T10:00:00Z 2026-11-17T10:12:00Z pass built"; done > "$common"
for i in $(seq 1 60); do echo "full old-$i 2026-11-17T10:00:00Z 2026-11-17T10:12:00Z pass built"; done > "$log"
echo three >> notes.md && git add notes.md # a tree not yet passed, so the run is not reused
DOTNET_FAIL_ON= run full
check "the per-clone log stays at 50 lines" [ "$(wc -l < "$log")" -eq 50 ]
check "the history stays at 500 lines" [ "$(wc -l < "$common")" -eq 500 ]
check "the newest run is first in the history" like "$(first_line "$common")" "full main *"

# Concurrent runs from several worktrees: each line lands once and none is torn.
: > "$common"
for n in 1 2 3 4 5 6; do
  git worktree add -q --detach "$WORK/c$n" HEAD
  echo "$n" >> "$WORK/c$n/notes.md"
  git -C "$WORK/c$n" add notes.md
done
for n in 1 2 3 4 5 6; do
  (cd "$WORK/c$n" && METAPHOR_CHECK_LOCK="$WORK/lock$n" DOTNET_FAIL_ON=format bash "$CHECK" full > /dev/null 2>&1 <<< "") &
done
wait
check "every concurrent run is logged once" [ "$(grep -c 'failed=format$' "$common")" -eq 6 ]
check "no line is torn" [ "$(grep -vc '^full detached .* fail built .*failed=format$' "$common")" -eq 0 ]
check "no lock or temp file is left behind" [ -z "$(find "$(dirname "$common")" -maxdepth 1 -name 'check-run-*' ! -name check-run-log ! -name check-run-history)" ]
for n in 1 2 3 4 5 6; do git worktree remove --force "$WORK/c$n"; done

printf 'check run log tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
