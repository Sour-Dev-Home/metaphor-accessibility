#!/usr/bin/env bash
# Black-box tests for scripts/dev/session-marks.sh with a made-up session folder and report log.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$ROOT/scripts/dev/session-marks.sh"

passed=0
failed=0
check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

SESSION="$WORK/session"
mkdir -p "$SESSION"
printf 'first spoken line\nsecond spoken line\n' > "$SESSION/transcript.txt"
LOG="$WORK/report-log.txt"
{
  echo "1000 ms request kind=Line label=far-before"
  echo "165000 ms request kind=Line label=near-before"
  echo "172230 ms mark number=1 session=0:02:52"
  echo "173000 ms silent change=Entered screen=Shop index=0 count=3"
  echo "400000 ms request kind=Line label=between"
  echo "600000 ms mark number=2 session=0:10:00"
  echo "900000 ms request kind=Line label=far-after"
} > "$LOG"

out="$(bash "$SCRIPT" "$SESSION" "$LOG" 2>&1)"; code=$?
DIGEST="$SESSION/session-marks.txt"
check "it succeeds" test "$code" -eq 0
check "it writes the digest beside the session logs" test -f "$DIGEST"
check "mark 1 is listed with its time" grep -q 'mark 1 .*0:02:52' "$DIGEST"
check "mark 2 is listed with its time" grep -q 'mark 2 .*0:10:00' "$DIGEST"
check "report-log lines before a mark are kept" grep -q 'near-before' "$DIGEST"
check "report-log lines after a mark are kept" grep -q 'Shop' "$DIGEST"
check "lines far from every mark are left out" bash -c '! grep -q "far-before\|far-after\|between" "$1"' _ "$DIGEST"
check "the transcript is not in the digest" bash -c '! grep -q "spoken line" "$1"' _ "$DIGEST"
check "the digest says the transcript has no timestamps" grep -qi 'transcript.*no timestamps' "$DIGEST"
check "no local path in the digest" bash -c '! grep -qF "$1" "$2"' _ "$WORK" "$DIGEST"
check "no local path in the output" bash -c '! grep -qF "$1" <<<"$2"' _ "$WORK" "$out"

mkdir -p "$WORK/repo/logs"
git -C "$WORK/repo" init -q
check "a folder inside a git repository is refused" bash -c '! bash "$1" "$2" "$3" >/dev/null 2>&1' _ "$SCRIPT" "$WORK/repo/logs" "$LOG"
check "no digest lands in the repository" test ! -e "$WORK/repo/logs/session-marks.txt"

out="$(bash "$SCRIPT" 2>&1)"; code=$?
check "no arguments prints the usage" grep -q '^usage: session-marks.sh' <<<"$out"
check "no arguments exits 2" test "$code" -eq 2
bash "$SCRIPT" "$SESSION" "$WORK/missing.txt" >/dev/null 2>&1; code=$?
check "a missing report log exits 2" test "$code" -eq 2

: > "$WORK/empty.txt"
out="$(bash "$SCRIPT" "$SESSION" "$WORK/empty.txt" 2>&1)"
check "a log with no marks says so" grep -q 'no marks' <<<"$out"

echo "session-marks: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
