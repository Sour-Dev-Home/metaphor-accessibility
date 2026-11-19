#!/usr/bin/env bash
# Black-box tests for scripts/ci/conflict-alert.sh, with a fake `gh` that serves fixtures and logs what it was asked to
# do. The fake ignores --jq and prints the fixture (already in the shape the jq filter produces).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
SCRIPT="$ROOT/scripts/ci/conflict-alert.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fixtures in $FIX: list.N (the Nth `pr list`, the last one repeats), alerted-NUM, pushed-NUM; calls go to $FIX/log.
echo "$*" >> "$FIX/log"
[ ! -f "$FIX/fail-list" ] || { [ "$1 $2" != "pr list" ] || exit 1; }
case "$1 $2" in
  "pr list")
    n=$(($(cat "$FIX/count" 2>/dev/null || echo 0) + 1))
    echo "$n" > "$FIX/count"
    while [ "$n" -gt 1 ] && [ ! -f "$FIX/list.$n" ]; do n=$((n - 1)); done
    cat "$FIX/list.$n" ;;
  "pr view")
    case "$*" in
      *comments*) cat "$FIX/alerted-$3" 2>/dev/null || true ;;
      *commits*) cat "$FIX/pushed-$3" ;;
    esac ;;
  "pr comment") echo "COMMENT $3 :: $*" >> "$FIX/comments" ;;
esac
FAKE
chmod +x "$WORK/bin/gh"

passed=0
failed=0

setup() { # setup: a fresh fixture folder
  FIX="$WORK/fix"
  rm -rf "$FIX"
  mkdir -p "$FIX"
  export FIX
}

run() { # run: the script against the fixtures, no waiting between retries
  out="$(PATH="$WORK/bin:$PATH" RETRY_DELAY=0 RETRIES=3 bash "$SCRIPT" owner/repo 2>&1)"
  code=$?
  comments="$(cat "$FIX/comments" 2>/dev/null || true)"
}

expect() { # expect NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '%s\n' "$out" "$comments"; fi
}

commented() { [ "$(grep -c "^COMMENT $1 " <<<"$comments")" -eq 1 ]; }
silent() { ! grep -q "^COMMENT $1 " <<<"$comments"; }

setup
printf '1\talice\tCONFLICTING\n2\tbob\tMERGEABLE\n3\tcarol\tCONFLICTING\n4\tdave\tUNKNOWN\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-3"
run
expect "exits zero" [ "$code" -eq 0 ]
expect "a conflicting PR gets one comment" commented 1
expect "a second conflicting PR (a draft looks the same) gets one" commented 3
expect "a mergeable PR gets none" silent 2
expect "the comment mentions the author" grep -q '@alice' <<<"$comments"
expect "the comment names main" grep -q 'main' <<<"$comments"
expect "the comment carries the marker" grep -qF '<!-- conflict-alert -->' <<<"$comments"
expect "a PR that stays UNKNOWN is skipped" silent 4

setup
printf '1\talice\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-1"
echo 2026-11-02T00:00:00Z > "$FIX/alerted-1"
run
expect "no repeat comment while it stays conflicting" silent 1

setup
printf '1\talice\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-03T00:00:00Z > "$FIX/pushed-1"
echo 2026-11-02T00:00:00Z > "$FIX/alerted-1"
run
expect "a new push that conflicts again gets a new comment" commented 1

setup
printf '5\terin\tUNKNOWN\n' > "$FIX/list.1"
printf '5\terin\tCONFLICTING\n' > "$FIX/list.2"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-5"
run
expect "an UNKNOWN state is re-read until GitHub has computed it" commented 5

setup
printf '6\tfrank\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-6"
run
expect "it never touches labels or merges" bash -c '! grep -qE "^pr (merge|edit)|--add-label" "$1"' _ "$FIX/log"

setup
touch "$FIX/fail-list"
run
expect "a failed listing fails the run" [ "$code" -eq 1 ]

setup
out="$(PATH="$WORK/bin:$PATH" env -u GITHUB_REPOSITORY bash "$SCRIPT" 2>&1)"
code=$?
comments=""
expect "no repo is a usage error" [ "$code" -eq 2 ]

echo "conflict-alert tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
