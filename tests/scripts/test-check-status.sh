#!/usr/bin/env bash
# Black-box tests for scripts/check-status.sh and the run log in scripts/check.sh (issue #269): the lock free, held,
# held by a dead process and with waiters, the run log's limit and its content, and the --short speed.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS GITHUB_ACTIONS SECURITY_REVIEW_SKIP

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATUS="$ROOT/scripts/check-status.sh"
CHECK="$ROOT/scripts/check.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0
export METAPHOR_CHECK_LOCK="$WORK/lock" METAPHOR_CHECK_NOW=10000 SKIP_SCRIPT_TESTS=1
export METAPHOR_CHECK_SLOTS=1 # the single-lock views below; the two-slot views unset it
export METAPHOR_CHECK_PS_CMD="cat $WORK/ps.txt"
: > "$WORK/ps.txt"

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '%s\n' "$out"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

# A throwaway repository, so the run logs live in a git dir of our own.
repo="$WORK/repo"
git init -q -b main "$repo" &&
  git -C "$repo" -c user.name=t -c user.email=t@example.com commit -q --allow-empty -m "chore: start" || exit 1
cd "$repo" || exit 1
git checkout -q -b 214-details-key

status() { out="$(bash "$STATUS" "$@" 2>&1)"; }

# Free.
status
check "free lock is reported" has "lock: free"
status --short
check "free lock, short" test "$out" = "lock: free"

# Held by a live process (this shell), for 6 minutes.
mkdir -p "$WORK/lock"
echo "$$" > "$WORK/lock/pid"
echo "214-details-key" > "$WORK/lock/branch"
echo 9640 > "$WORK/lock/since"
status
check "held lock names pid and branch" has "held by pid $$ on 214-details-key for 6 min"
status --short
check "held lock, short" test "$out" = "lock: 214-details-key · 6m"

# Waiters: two live, one dead.
mkdir -p "$WORK/lock.waiting"
echo "a 9800" > "$WORK/lock.waiting/$$"
echo "b 9700" > "$WORK/lock.waiting/$PPID"
echo "c 9000" > "$WORK/lock.waiting/999999"
status
check "waiters counted, dead ones ignored" has "waiting: 2 (longest 5 min)"
status --short
check "short shows waiters" test "$out" = "lock: 214-details-key · 6m · 2 waiting"

# Held by a dead process.
echo 999999 > "$WORK/lock/pid"
status
check "dead holder is stale" has "stale"
check "dead holder names the pid" has "999999"
status --short
check "dead holder, short" has "stale"
rm -rf "$WORK/lock" "$WORK/lock.waiting"

# Two slots (the default; #281): each slot's holder shown, free slots free, stale slots flagged.
unset METAPHOR_CHECK_SLOTS
status
check "two free slots listed" has "slot 2: free"
status --short
check "two free slots, short" test "$out" = "lock: free"
mkdir -p "$WORK/lock.2"
echo "$$" > "$WORK/lock.2/pid"
echo "281-check-slots" > "$WORK/lock.2/branch"
echo 9400 > "$WORK/lock.2/since"
status
check "slot 1 free, slot 2 held" has "slot 1: free"
check "slot 2 names pid and branch" has "slot 2: held by pid $$ on 281-check-slots for 10 min"
status --short
check "short shows both slots" test "$out" = "slots: free | 281-check-slots · 10m"
mkdir -p "$WORK/lock"
echo "$PPID" > "$WORK/lock/pid"
echo "214-details-key" > "$WORK/lock/branch"
echo 9640 > "$WORK/lock/since"
mkdir -p "$WORK/lock.waiting"
echo "a 9800" > "$WORK/lock.waiting/$$"
status
check "slot 1 held" has "slot 1: held by pid $PPID on 214-details-key for 6 min"
check "waiters shown with two slots" has "waiting: 1 (longest 3 min)"
status --short
check "short, both held, waiter" test "$out" = "slots: 214-details-key · 6m | 281-check-slots · 10m · 1 waiting"
echo 999999 > "$WORK/lock.2/pid"
status
check "stale slot 2" has "slot 2: stale, holder pid 999999 has exited"
status --short
check "stale slot 2, short" has "stale (pid 999999 gone)"
rm -rf "$WORK/lock" "$WORK/lock.2" "$WORK/lock.waiting"
export METAPHOR_CHECK_SLOTS=1

# Run logs per worktree.
log="$(git rev-parse --absolute-git-dir)/check-run-log"
echo "full 214-details-key 2026-11-17T10:00:00Z 2026-11-17T10:12:00Z pass built" > "$log"
status
check "last runs listed" has "full 214-details-key 2026-11-17T10:00:00Z 2026-11-17T10:12:00Z pass built"

# Twelve linked worktrees (#314): every one's last runs are listed, and the listing doesn't fork per worktree (it took
# about a second each under Git Bash on Windows, so 12 worktrees took over 10 s; the bound is loose for a busy machine).
for n in $(seq 1 12); do
  git worktree add -q --detach "$WORK/wt$n" &&
    echo "full branch-$n 2026-11-17T10:00:00Z 2026-11-17T10:12:00Z pass built" > "$(git -C "$WORK/wt$n" rev-parse --absolute-git-dir)/check-run-log"
done
t0=$(date +%s%N)
status
t1=$(date +%s%N)
check "all 12 linked worktrees listed" test "$(grep -c 'full branch-' <<< "$out")" -eq 12
out="12 worktrees: $(((t1 - t0) / 1000000)) ms"
check "12 worktrees list in under 6 s" test $(((t1 - t0) / 1000000)) -lt 6000
for n in $(seq 1 12); do git worktree remove --force "$WORK/wt$n"; done

# Idle workers, and the shutdown command.
printf '  1234 MSBuild.exe /nodeReuse:true\n  1235 VBCSCompiler.exe\n  1300 bash\n' > "$WORK/ps.txt"
status
check "idle workers counted" has "idle build workers: 2"
check "shutdown command given" has "dotnet build-server shutdown"
check "other processes are not workers" lacks "1300"
: > "$WORK/ps.txt"
status
check "no workers, no warning" lacks "idle build workers"

# --short is fast: under 200 ms on top of starting bash (which alone takes ~0.5 s under Git Bash on Windows). It forks
# nothing, so the margin holds.
t0=$(date +%s%N)
bash -c true
t1=$(date +%s%N)
bash "$STATUS" --short > /dev/null
t2=$(date +%s%N)
out="bash alone $(((t1 - t0) / 1000000)) ms, --short $(((t2 - t1) / 1000000)) ms"
check "short is under 200 ms beyond bash startup" test $(((t2 - t1 - (t1 - t0)) / 1000000)) -lt 200

# check.sh writes the run log: one line, last 50, no extra text.
mkdir -p "$WORK/bin"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/bin/dotnet"
chmod +x "$WORK/bin/dotnet"
export PATH="$WORK/bin:$PATH"
git checkout -q -b log-test
rm -f "$log"
for _ in $(seq 1 55); do echo "slow x s e pass built" >> "$log"; done
bash "$CHECK" slow > /dev/null 2>&1
out="$(cat "$log")"
check "run log keeps 50 lines" test "$(wc -l < "$log")" -eq 50
check "newest line first" test "$(head -n 1 "$log" | cut -d' ' -f1,2)" = "slow log-test"
check "line has result and built" test -n "$(head -n 1 "$log" | awk 'NF==6 && $5 ~ /^(pass|fail)$/ && $6=="built"')"
check "lock released afterwards" test ! -d "$WORK/lock"

printf 'check-status tests: %s passed, %s failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
