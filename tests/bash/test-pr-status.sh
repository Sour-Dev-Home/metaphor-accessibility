#!/usr/bin/env bash
# Black-box tests for scripts/dev/pr-status.sh with a fake `gh` that serves made-up fixtures and logs every call. The fake
# ignores --jq and prints the fixture already in the shape the script's filter produces.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$ROOT/scripts/dev/pr-status.sh"

mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fixtures in $FIX: view-NUM (the `pr view` output: mergeable, state, auto-merge, failing checks, tab separated),
# queue-NUM (the graphql output: the queue position, or none), fail-NUM (any call for NUM fails). Calls go to $FIX/log.
all="$*"; echo "${all//$'\n'/ }" >> "$FIX/log" # one line per call, whatever newlines its arguments hold
case "$1 $2" in
  "pr view") n="$3"; [ ! -f "$FIX/fail-$n" ] || exit 1; cat "$FIX/view-$n" ;;
  "repo view") echo "example-owner/example-repo" ;; # the repository the script runs in, as owner/name
  "api graphql")
    for arg in "$@"; do case "$arg" in number=*) n="${arg#number=}" ;; esac; done
    [ ! -f "$FIX/fail-$n" ] || exit 1
    cat "$FIX/queue-$n" ;;
  *) exit 1 ;;
esac
FAKE
chmod +x "$WORK/bin/gh"

passed=0
failed=0
check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

export FIX="$WORK/fix"
mkdir -p "$FIX"
printf 'MERGEABLE\tCLEAN\ttrue\t\n' > "$FIX/view-1"
printf '2\n' > "$FIX/queue-1"
printf 'CONFLICTING\tDIRTY\tfalse\tbuild,docs\n' > "$FIX/view-2"
printf 'none\n' > "$FIX/queue-2"
printf 'MERGEABLE\tBLOCKED\ttrue\t\n' > "$FIX/view-3"
printf 'none\n' > "$FIX/queue-3"
printf 'MERGEABLE\tCLEAN\ttrue\t\n' > "$FIX/view-4"
touch "$FIX/fail-4"

out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 1 2 3 4 2>&1)"
status=$?

check "one line per PR" test "$(wc -l <<<"$out")" -eq 4
check "a queued PR shows its position and auto-merge" grep -qx '#1 mergeable=MERGEABLE state=CLEAN auto=on queue=2 failing=none' <<<"$out"
check "a failing PR lists its checks" grep -qx '#2 mergeable=CONFLICTING state=DIRTY auto=off queue=none failing=build,docs' <<<"$out"
check "an unqueued PR shows queue=none" grep -qx '#3 mergeable=MERGEABLE state=BLOCKED auto=on queue=none failing=none' <<<"$out"
check "a PR it can't read gets a line, not a stop" grep -qx '#4 could not read its state' <<<"$out"
check "it exits non-zero when a PR could not be read" test "$status" -ne 0
check "the query never sends the {owner}/{repo} placeholders (#786)" bash -c '! grep -Eq "[{](owner|repo)[}]" "$1"' _ "$FIX/log"
check "it passes the resolved owner and name as variables (#786)" bash -c 'grep -q "owner=example-owner" "$1" && grep -q "name=example-repo" "$1"' _ "$FIX/log"
check "it only reads" bash -c '! grep -Ev "^(pr view|repo view|api graphql) " "$1" && ! grep -Eq "mutation| -X " "$1"' _ "$FIX/log"

out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"; code=$?
check "no arguments prints the usage" grep -q '^usage: pr-status.sh' <<<"$out"
check "no arguments exits 2" test "$code" -eq 2
PATH="$WORK/bin:$PATH" bash "$SCRIPT" abc >/dev/null 2>&1; code=$?
check "a non-number is refused" test "$code" -eq 2

echo "pr-status: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
