#!/usr/bin/env bash
# Black-box tests for scripts/ci/integration-sync.sh: a real throwaway git repo as `origin` and a fake `gh`.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

mkdir -p "$WORK/bin" "$WORK/scripts/ci"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fixtures in $FIX: list (the `pr list` output), last-NUM (key of the latest marker comment); comments go to $FIX/comments.
echo "$*" >> "$FIX/log"
case "$1 $2" in
  "pr list") cat "$FIX/list" ;;
  "pr view") cat "$FIX/last-$3" 2>/dev/null || true ;;
  "pr comment") echo "COMMENT $3 :: $*" >> "$FIX/comments" ;;
esac
FAKE
chmod +x "$WORK/bin/gh"
cp "$ROOT/scripts/ci/security-stamp.sh" "$ROOT/scripts/ci/integration-sync.sh" "$WORK/scripts/ci/"

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
setup() { FIX="$WORK/fix"; rm -rf "$FIX"; mkdir -p "$FIX"; export FIX; }
run() {
  out="$(cd "$WORK/clone" && PATH="$WORK/bin:$PATH" bash "$WORK/scripts/ci/integration-sync.sh" owner/repo 2>&1)"
  code=$?
  comments="$(cat "$FIX/comments" 2>/dev/null || true)"
}
expect() { local name="$1"; shift; if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '%s\n' "$out" "$comments"; fi; }
commented() { [ "$(grep -c "^COMMENT $1 " <<<"$comments")" -eq 1 ]; }
silent() { ! grep -q "^COMMENT $1 " <<<"$comments"; }
line() { awk -v n="$1" '/^COMMENT /{p = ($2 == n)} p' <<<"$comments"; } # one comment's lines (bodies span lines)

setup
printf '1\talice\tclean-b\n2\tbob\tconflict-b\n3\tcarol\tcurrent-b2\n' > "$FIX/list"
run
expect "exits zero" [ "$code" -eq 0 ]
expect "a clean branch is told to merge main" commented 1
expect "hook files changed on main are named, with the stamp warning" grep -q 'Hook-layer files changed.*h.cs' <<<"$(line 1)"
expect "a conflicting branch is flagged" commented 2
expect "the conflict comment names the file" grep -q 'conflicts with `main` in: shared.txt' <<<"$(line 2)"
expect "a branch that already has main gets none" silent 3
expect "the comment mentions the author" grep -q '@alice' <<<"$(line 1)"
expect "it never merges or edits PRs" bash -c '! grep -qE "^pr (merge|edit)" "$1"' _ "$FIX/log"

key="$(grep -o 'integration-sync:[0-9a-f]*' <<<"$comments" | head -1 | cut -d: -f2)"
setup
printf '1\talice\tclean-b\n' > "$FIX/list"
echo "$key" > "$FIX/last-1"
run
expect "no repeat while the outcome is unchanged" silent 1

setup
printf '1\talice\tclean-b\n' > "$FIX/list"
echo deadbeef > "$FIX/last-1"
run
expect "a changed outcome comments again" commented 1

setup
: > "$FIX/list"
run
expect "no draft PRs is fine" [ "$code" -eq 0 ]

setup
out="$(cd "$WORK/clone" && PATH="$WORK/bin:$PATH" env -u GITHUB_REPOSITORY bash "$WORK/scripts/ci/integration-sync.sh" 2>&1)"
code=$?
comments=""
expect "no repo is a usage error" [ "$code" -eq 2 ]

echo "integration-sync tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
