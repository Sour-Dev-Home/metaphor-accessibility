#!/usr/bin/env bash
# Black-box tests for scripts/dev/queue.sh with a fake `gh` that serves made-up fixtures and logs every call. The fake
# ignores --jq and prints each fixture already in the shape the script's filter produces.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$ROOT/scripts/dev/queue.sh"

mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fixtures in $FIX: queue (the queue query's output, tab separated position, PR, state), prs (the open PR numbers, one per
# line), view-NUM and queue-NUM (what pr-status.sh reads), fail-queue / fail-prs (that call fails). Calls go to $FIX/log.
all="$*"; echo "${all//$'\n'/ }" >> "$FIX/log" # one line per call, whatever newlines its arguments hold
case "$1 $2" in
  "repo view") echo "example-owner/example-repo" ;;
  "pr list") [ ! -f "$FIX/fail-prs" ] || exit 1; cat "$FIX/prs" ;;
  "pr view") cat "$FIX/view-$3" ;;
  "api graphql")
    n=""
    for arg in "$@"; do case "$arg" in number=*) n="${arg#number=}" ;; esac; done
    if [ -n "$n" ]; then cat "$FIX/queue-$n"; else [ ! -f "$FIX/fail-queue" ] || exit 1; cat "$FIX/queue"; fi ;;
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
printf '1\t#11\tAWAITING_CHECKS\n2\t#12\tQUEUED\n' > "$FIX/queue"
printf '11\n12\n13\n' > "$FIX/prs"
printf 'MERGEABLE\tCLEAN\ttrue\t\n' > "$FIX/view-11"
printf '1\n' > "$FIX/queue-11"
printf 'MERGEABLE\tCLEAN\ttrue\t\n' > "$FIX/view-12"
printf '2\n' > "$FIX/queue-12"
printf 'CONFLICTING\tDIRTY\tfalse\tbuild\n' > "$FIX/view-13"
printf 'none\n' > "$FIX/queue-13"

out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"
status=$?
check "it exits 0 when everything reads" test "$status" -eq 0
check "it lists the queue entries" grep -qx '  1 #11 AWAITING_CHECKS' <<<"$out"
check "it lists the second entry" grep -qx '  2 #12 QUEUED' <<<"$out"
check "it prints a status line for a queued PR" grep -qx '#11 mergeable=MERGEABLE state=CLEAN auto=on queue=1 failing=none' <<<"$out"
check "it prints a status line for a PR outside the queue" grep -qx '#13 mergeable=CONFLICTING state=DIRTY auto=off queue=none failing=build' <<<"$out"
check "the queue comes before the PR lines" test "$(grep -n '^merge queue:' <<<"$out" | cut -d: -f1)" -lt "$(grep -n '^#11 ' <<<"$out" | cut -d: -f1)"
check "the queue query never sends the {owner}/{repo} placeholders (#786)" bash -c '! grep -Eq "[{](owner|repo)[}]" "$1"' _ "$FIX/log"
check "it passes the resolved owner and name as variables (#788)" bash -c 'grep -q "owner=example-owner" "$1" && grep -q "name=example-repo" "$1"' _ "$FIX/log"
check "it only reads" bash -c '! grep -Ev "^(pr list|pr view|repo view|api graphql) " "$1" && ! grep -Eq "mutation| -X " "$1"' _ "$FIX/log"

: > "$FIX/queue"
printf '' > "$FIX/prs"
out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"
status=$?
check "an empty queue says so" grep -qx '  empty' <<<"$out"
check "no open PRs says so" grep -qx '  none' <<<"$out"
check "empty is not a failure" test "$status" -eq 0

touch "$FIX/fail-queue"
printf '11\n' > "$FIX/prs"
out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"
status=$?
check "an unreadable queue gets a line, the PRs still print" grep -qx '  could not read the merge queue' <<<"$out"
check "the PRs still print after a queue failure" grep -q '^#11 ' <<<"$out"
check "it exits non-zero when the queue could not be read" test "$status" -ne 0
rm "$FIX/fail-queue"

touch "$FIX/fail-prs"
out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" 2>&1)"
status=$?
check "an unreadable PR list gets a line" grep -qx '  could not list the open PRs' <<<"$out"
check "it exits non-zero when the PR list could not be read" test "$status" -ne 0

PATH="$WORK/bin:$PATH" bash "$SCRIPT" extra >/dev/null 2>&1; code=$?
check "an argument is refused" test "$code" -eq 2

echo "queue: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
