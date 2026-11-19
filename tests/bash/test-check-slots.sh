#!/usr/bin/env bash
# Black-box tests for the build-check slots in scripts/check.sh (issue #281): METAPHOR_CHECK_SLOTS slots (default 2),
# slot 1 at METAPHOR_CHECK_LOCK itself and slot N at "<lock>.N", a waiter when all are held, dead-holder takeover per
# slot, and SLOTS=1 behaving as the single lock did. A stand-in `dotnet` records which slots exist while it runs.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
# A copy of the scripts whose docs-tools installer, which `full` runs (#353), does nothing.
mkdir -p "$WORK/gate"
cp -r "$ROOT/scripts" "$WORK/gate/"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/gate/scripts/setup/install-docs-tools.sh"
CHECK="$WORK/gate/scripts/check.sh"
passed=0
failed=0
export METAPHOR_CHECK_LOCK="$WORK/lock" METAPHOR_CHECK_POLL=1 SKIP_SCRIPT_TESTS=1
export SEEN="$WORK/seen"

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

mkdir -p "$WORK/bin"
cat > "$WORK/bin/dotnet" <<'STUB'
#!/usr/bin/env bash
# Record which slot directories hold a pid while the "build" runs.
for d in "$METAPHOR_CHECK_LOCK" "$METAPHOR_CHECK_LOCK.2" "$METAPHOR_CHECK_LOCK.3"; do
  [ -f "$d/pid" ] && [ "$(cat "$d/pid")" != "$HOLDER" ] && [ "$(cat "$d/pid")" != "$HOLDER2" ] && echo "${d##*/}" >> "$SEEN"
done
exit 0
STUB
chmod +x "$WORK/bin/dotnet"
export PATH="$WORK/bin:$PATH"
export HOLDER=$$ HOLDER2=$PPID


hold() { mkdir -p "$1" && echo "$2" > "$1/pid"; } # hold DIR PID
reset() { # a fresh seeded repo each time, so `full` never reuses a recorded pass
  rm -rf "$WORK"/lock* "$SEEN" "$WORK/out" "$WORK/code" "$WORK/repo"
  mkdir -p "$WORK/repo" && cd "$WORK/repo" || exit 2
  git init -q -b main
  git config user.name test && git config user.email test@example.invalid
  git config core.autocrlf false && git config commit.gpgsign false
  : > Metaphor.sln
  echo one > notes.md
  mkdir src && echo "class A {}" > src/a.cs
  git add -A && git commit -q -m "chore: seed"
  git update-ref refs/remotes/origin/main HEAD
}
run() { bash "$CHECK" full > "$WORK/out" 2>&1; echo $? > "$WORK/code"; }
sawonly() { [ "$(sort -u "$SEEN" 2> /dev/null | tr '\n' ' ')" = "$1 " ]; }
quiet() { ! grep -q waiting "$WORK/out"; }
waitfor() { local i; for i in $(seq 1 30); do grep -q "$1" "$WORK/out" 2> /dev/null && return 0; sleep 1; done; }

# Free: the run takes slot 1, the original lock path.
reset
run
check "free: exits 0" test "$(cat "$WORK/code")" = 0
check "free: uses the original lock path" sawonly lock
check "free: slots released" test ! -e "$WORK/lock" -a ! -e "$WORK/lock.2"

# One slot held live: the run does not wait, and takes slot 2.
reset
hold "$WORK/lock" $$
run
check "slot 1 held: run uses slot 2" sawonly lock.2
check "slot 1 held: no waiting message" quiet
check "slot 1 held: holder's slot untouched" test "$(cat "$WORK/lock/pid")" = "$$"

# Slot 1 free, slot 2 held live: takes slot 1.
reset
hold "$WORK/lock.2" $$
run
check "slot 2 held: run uses slot 1" sawonly lock

# Dead holder in slot 1, live in slot 2: takes over slot 1.
reset
hold "$WORK/lock" 999999
hold "$WORK/lock.2" $$
run
check "dead holder in slot 1 is taken over" sawonly lock
# Dead holder in slot 2, live in slot 1: takes over slot 2.
reset
hold "$WORK/lock" $$
hold "$WORK/lock.2" 999999
run
check "dead holder in slot 2 is taken over" sawonly lock.2

# Both held live: a third run waits, then starts once a slot is freed.
reset
hold "$WORK/lock" $$
hold "$WORK/lock.2" $PPID
run &
bg=$!
waitfor "waiting for another" # Windows process starts are slow
for _ in 1 2 3 4 5; do [ -n "$(ls "$WORK/lock.waiting" 2> /dev/null)" ] && break; sleep 1; done
check "both held: third run waits" kill -0 "$bg"
check "both held: says it waits" grep -q "waiting for another build check" "$WORK/out"
check "both held: registered as a waiter" test -n "$(ls "$WORK/lock.waiting" 2> /dev/null)"
check "both held: has not built" test ! -e "$SEEN"
rm -rf "$WORK/lock.2"
wait "$bg"
check "freed slot: third run finishes" test "$(cat "$WORK/code")" = 0
check "freed slot: it took slot 2" sawonly lock.2
check "freed slot: waiter entry removed" test -z "$(ls "$WORK/lock.waiting" 2> /dev/null)"

# METAPHOR_CHECK_SLOTS=1 is the single lock: a held slot 1 makes the run wait, and slot 2 is never used.
reset
hold "$WORK/lock" $$
METAPHOR_CHECK_SLOTS=1 run &
bg=$!
waitfor "waiting for another" # Windows process starts are slow
for _ in 1 2 3 4 5; do [ -n "$(ls "$WORK/lock.waiting" 2> /dev/null)" ] && break; sleep 1; done
check "SLOTS=1: waits behind a held lock" kill -0 "$bg"
rm -rf "$WORK/lock"
wait "$bg"
check "SLOTS=1: runs after release" test "$(cat "$WORK/code")" = 0
check "SLOTS=1: only the original path used" sawonly lock

# A larger setting adds slots.
reset
hold "$WORK/lock" $$
hold "$WORK/lock.2" $PPID
METAPHOR_CHECK_SLOTS=3 run
check "SLOTS=3: third slot used" sawonly lock.3

printf 'check-slots tests: %s passed, %s failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
