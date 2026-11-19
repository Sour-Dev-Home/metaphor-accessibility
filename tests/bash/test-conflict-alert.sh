#!/usr/bin/env bash
# Black-box tests for scripts/ci/conflict-alert.sh: a real throwaway git repo as `origin` for the sync pass, and a fake
# `gh` that serves fixtures and logs what it was asked to do. The fake ignores --jq and prints the fixture (already in
# the shape the jq filter produces).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$WORK/scripts/ci/conflict-alert.sh"

mkdir -p "$WORK/bin" "$WORK/scripts/ci"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fixtures in $FIX: list.N (the Nth plain `pr list`, the last one repeats), drafts (the `pr list --draft` output),
# alerted-NUM and pushed-NUM (alert pass), last-NUM (key of the latest sync marker); calls go to $FIX/log.
echo "$*" >> "$FIX/log"
[ ! -f "$FIX/fail-list" ] || { [ "$1 $2" != "pr list" ] || exit 1; }
case "$1 $2" in
  "pr list")
    case " $* " in *" --draft "*) cat "$FIX/drafts" 2>/dev/null || true; exit 0 ;; esac
    n=$(($(cat "$FIX/count" 2>/dev/null || echo 0) + 1))
    echo "$n" > "$FIX/count"
    while [ "$n" -gt 1 ] && [ ! -f "$FIX/list.$n" ]; do n=$((n - 1)); done
    cat "$FIX/list.$n" 2>/dev/null || true ;;
  "pr view")
    case "$*" in
      *integration-sync*) cat "$FIX/last-$3" 2>/dev/null || true ;;
      *comments*) cat "$FIX/alerted-$3" 2>/dev/null || true ;;
      *commits*) cat "$FIX/pushed-$3" ;;
    esac ;;
  "pr comment") echo "COMMENT $3 :: $*" >> "$FIX/comments" ;;
esac
FAKE
chmod +x "$WORK/bin/gh"
cp "$ROOT/scripts/ci/security-stamp.sh" "$ROOT/scripts/ci/conflict-alert.sh" "$WORK/scripts/ci/"

# The sync pass's origin: main moves twice (a shared file, then a hook-layer file) after some branches were made.
g() { git -C "$WORK/clone" -c user.name=t -c user.email=t@example.com -c commit.gpgsign=false "$@"; }
git init -q --bare -b main "$WORK/origin.git"
git clone -q "$WORK/origin.git" "$WORK/clone" 2>/dev/null
g checkout -q -B main
mkdir -p "$WORK/clone/src/Metaphor.Hooks"
echo a > "$WORK/clone/shared.txt"
echo a > "$WORK/clone/src/Metaphor.Hooks/h.cs"
g add -A
g commit -q -m base
g push -q origin main
branch() { # branch NAME FILE CONTENT: a branch off main's current tip with one change
  g checkout -q -b "$1" main
  echo "$3" > "$WORK/clone/$2"
  g add -A
  g commit -q -m "$1"
  g push -q origin "$1"
  g checkout -q main
}
branch clean-b other.txt x
branch conflict-b shared.txt b
echo c > "$WORK/clone/shared.txt"
g add -A
g commit -q -m "main moves"
g push -q origin main
branch current-b other4.txt z # made after main moved, so it already has main
echo c > "$WORK/clone/src/Metaphor.Hooks/h.cs"
g add -A
g commit -q -m "hook moves"
g push -q origin main
branch current-b2 other5.txt z

passed=0
failed=0

setup() { # setup: a fresh fixture folder
  FIX="$WORK/fix"
  rm -rf "$FIX"
  mkdir -p "$FIX"
  export FIX
}

run() { # run [FLAG]: the script against the fixtures from the clone, no waiting between retries
  out="$(cd "$WORK/clone" && PATH="$WORK/bin:$PATH" RETRY_DELAY=0 RETRIES=3 bash "$SCRIPT" "$@" owner/repo 2>&1)"
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
line() { awk -v n="$1" '/^COMMENT /{p = ($2 == n)} p' <<<"$comments"; } # one comment's lines (bodies span lines)
untouched() { ! grep -qE "^pr (merge|edit)|--add-label" "$FIX/log"; }

# The alert pass.
setup
printf '1\talice\tCONFLICTING\n2\tbob\tMERGEABLE\n3\tcarol\tCONFLICTING\n4\tdave\tUNKNOWN\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-3"
run --alert-only
expect "alert: exits zero" [ "$code" -eq 0 ]
expect "alert: a conflicting PR gets one comment" commented 1
expect "alert: a second conflicting PR (a draft looks the same) gets one" commented 3
expect "alert: a mergeable PR gets none" silent 2
expect "alert: the comment mentions the author" grep -q '@alice' <<<"$comments"
expect "alert: the comment names main" grep -q 'main' <<<"$comments"
expect "alert: the comment carries the marker" grep -qF '<!-- conflict-alert -->' <<<"$comments"
expect "alert: a PR that stays UNKNOWN is skipped" silent 4
expect "alert: --alert-only lists no drafts" bash -c '! grep -q -- "--draft" "$1"' _ "$FIX/log"

setup
printf '1\talice\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-1"
echo 2026-11-02T00:00:00Z > "$FIX/alerted-1"
run --alert-only
expect "alert: no repeat comment while it stays conflicting" silent 1

setup
printf '1\talice\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-03T00:00:00Z > "$FIX/pushed-1"
echo 2026-11-02T00:00:00Z > "$FIX/alerted-1"
run --alert-only
expect "alert: a new push that conflicts again gets a new comment" commented 1

setup
printf '5\terin\tUNKNOWN\n' > "$FIX/list.1"
printf '5\terin\tCONFLICTING\n' > "$FIX/list.2"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-5"
run --alert-only
expect "alert: an UNKNOWN state is re-read until GitHub has computed it" commented 5

setup
printf '6\tfrank\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-6"
run --alert-only
expect "alert: it never touches labels or merges" untouched

setup
touch "$FIX/fail-list"
run --alert-only
expect "alert: a failed listing fails the run" [ "$code" -eq 1 ]

# The sync pass.
setup
printf '1\talice\tclean-b\n2\tbob\tconflict-b\n3\tcarol\tcurrent-b2\n' > "$FIX/drafts"
run --sync-only
expect "sync: exits zero" [ "$code" -eq 0 ]
expect "sync: a clean branch is told to merge main" commented 1
expect "sync: hook files changed on main are named, with the stamp warning" grep -q 'Hook-layer files changed.*h.cs' <<<"$(line 1)"
expect "sync: a conflicting branch is flagged" commented 2
expect "sync: the conflict comment names the file" grep -q 'conflicts with `main` in: shared.txt' <<<"$(line 2)"
expect "sync: a branch that already has main gets none" silent 3
expect "sync: the comment mentions the author" grep -q '@alice' <<<"$(line 1)"
expect "sync: it never merges or edits PRs" untouched
expect "sync: --sync-only skips the alert pass" bash -c '! grep -q "mergeable" "$1"' _ "$FIX/log"

key="$(grep -o 'integration-sync:[0-9a-f]*' <<<"$comments" | head -1 | cut -d: -f2)"
setup
printf '1\talice\tclean-b\n' > "$FIX/drafts"
echo "$key" > "$FIX/last-1"
run --sync-only
expect "sync: no repeat while the outcome is unchanged" silent 1

setup
printf '1\talice\tclean-b\n' > "$FIX/drafts"
echo deadbeef > "$FIX/last-1"
run --sync-only
expect "sync: a changed outcome comments again" commented 1

setup
run --sync-only
expect "sync: no draft PRs is fine" [ "$code" -eq 0 ]

# Both passes, as the workflow runs them.
setup
printf '7\tgina\tCONFLICTING\n' > "$FIX/list.1"
echo 2026-11-01T00:00:00Z > "$FIX/pushed-7"
printf '8\thank\tclean-b\n' > "$FIX/drafts"
run
expect "both: exits zero" [ "$code" -eq 0 ]
expect "both: the alert pass comments" grep -qF '<!-- conflict-alert -->' <<<"$(line 7)"
expect "both: the sync pass comments" grep -qF '<!-- integration-sync:' <<<"$(line 8)"
expect "both: it never touches labels or merges" untouched

setup
touch "$FIX/fail-list"
printf '8\thank\tclean-b\n' > "$FIX/drafts"
run
expect "both: a failed alert pass fails the run" [ "$code" -eq 1 ]
expect "both: and the sync pass doesn't run" bash -c '! grep -q -- "--draft" "$1"' _ "$FIX/log"

setup
out="$(PATH="$WORK/bin:$PATH" env -u GITHUB_REPOSITORY bash "$SCRIPT" 2>&1)"
code=$?
comments=""
expect "no repo is a usage error" [ "$code" -eq 2 ]
out="$(PATH="$WORK/bin:$PATH" bash "$SCRIPT" --bogus owner/repo 2>&1)"
code=$?
expect "an unknown flag is a usage error" [ "$code" -eq 2 ]

echo "conflict-alert tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
