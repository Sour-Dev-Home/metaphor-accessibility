#!/usr/bin/env bash
# Black-box tests for scripts/github-outage.sh (issue #312): classifying recorded outage and real-failure output, a
# push that fails during an outage and is parked, a real rejection that is not, the resume after recovery, and a second
# resume that does nothing. `gh` is a stub on PATH; the remote is a local bare repository.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTAGE="$ROOT/scripts/github-outage.sh"
SHAPES="$ROOT/tests/fixtures/outage/shapes.txt"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0
check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '%s\n' "${out:-}"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

# Every recorded shape is classified as its header says.
name="" want="" block=""
flush() {
  [ -n "$name" ] || return 0
  out="$(classify 1 <<< "$block")" # in-process: spawning bash is slow under Git Bash
  check "shape $name is $want" test "$out" = "$want"
}
# shellcheck source=../../scripts/github-outage.sh
source "$OUTAGE"
while IFS= read -r line; do
  if [[ "$line" == "=== "* ]]; then
    flush
    read -r _ want name <<< "$line"
    block=""
  else
    block+="$line"$'\n'
  fi
done < "$SHAPES"
flush
out="$(classify 124 <<< "")"
check "a timeout kill with no output is an outage" test "$out" = "outage"
out="$(classify 0 <<< "")"
check "exit 0 is ok" test "$out" = "ok"

# A fake gh and a local remote.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" << 'EOF'
#!/usr/bin/env bash
echo "$*" >> "$GH_LOG"
if [ -f "$GH_DOWN" ]; then echo "HTTP 503: Service Unavailable (https://api.github.com/graphql)" >&2; exit 1; fi
case "$1 $2" in
  "pr list") [ ! -f "$GH_LOG.pr" ] || echo 77 ;;
  "pr create") touch "$GH_LOG.pr"; echo "https://github.com/example/repo/pull/77" ;;
  "pr merge") ;;
esac
EOF
chmod +x "$WORK/bin/gh"
export PATH="$WORK/bin:$PATH" GH_LOG="$WORK/gh.log" GH_DOWN="$WORK/gh-down"
: > "$GH_LOG"

git init -q --bare "$WORK/remote.git"
git init -q -b main "$WORK/repo"
cd "$WORK/repo" || exit 1
export GIT_AUTHOR_NAME=t GIT_AUTHOR_EMAIL=t@example.invalid GIT_COMMITTER_NAME=t GIT_COMMITTER_EMAIL=t@example.invalid
git commit -q --allow-empty -m "chore: start"
git remote add origin "$WORK/remote.git"
git push -q origin main
git checkout -q -b 312-test
git commit -q --allow-empty -m "feat: work"

# Outage: the push fails, the work is parked with its PR text.
git remote set-url origin "$WORK/nowhere.git"
git push -u origin 312-test > "$WORK/push.out" 2>&1
code=$?
out="$(bash "$OUTAGE" classify "$code" < "$WORK/push.out")"
check "a push to an unreachable remote is not a rejection" test "$code" -ne 0
echo "body text" > "$WORK/body.md"
out="$(bash "$OUTAGE" park 312-test 312 --title "feat: x" --body-file "$WORK/body.md")"
check "park reports it" has "parked: GitHub outage: 312-test"
out="$(bash "$OUTAGE" list)"
check "list shows the parked branch" has "312-test (issue 312)"
check "the record is in the git dir" test -f "$(git rev-parse --git-common-dir)/parked-pushes/312-test/title"
out="$(git status --porcelain)"
check "nothing parked is committed or untracked" test -z "$out"

# Still down: resume keeps the record.
out="$(bash "$OUTAGE" resume)"
code=$?
check "resume during the outage fails and keeps it" test "$code" -eq 1
check "resume says the push failed" has "312-test: push failed"
out="$(bash "$OUTAGE" list)"
check "still parked" has "312-test"

# Recovery for git, gh still down: the push lands, the PR waits.
git remote set-url origin "$WORK/remote.git"
touch "$GH_DOWN"
out="$(bash "$OUTAGE" resume)"
check "push done, PR still parked" has "312-test: pushed"
check "PR failure reported as an outage" has "PR not opened"
out="$(bash "$OUTAGE" list)"
check "only the PR steps remain" test "$out" = "312-test (issue 312): pr automerge "

# Full recovery.
rm -f "$GH_DOWN"
out="$(bash "$OUTAGE" resume)"
check "PR opened" has "312-test: PR #77"
check "auto-merge enabled" has "auto-merge enabled on #77"
check "the pr create used the recorded title" grep -q -- "--title feat: x" "$GH_LOG"
out=""
check "the branch is on the remote" git -C "$WORK/remote.git" rev-parse --verify -q refs/heads/312-test > /dev/null
out="$(bash "$OUTAGE" list)"
check "nothing left parked" test "$out" = "none parked"

# A second run does nothing.
: > "$GH_LOG"
out="$(bash "$OUTAGE" resume)"
check "second resume is a no-op" test "$out" = "nothing parked"
check "second resume calls nothing" test ! -s "$GH_LOG"

# A real rejection: a diverged remote is classified real, so the agent does not park it.
git clone -q "$WORK/remote.git" "$WORK/other" 2> /dev/null
git -C "$WORK/other" checkout -q 312-test
git -C "$WORK/other" commit -q --allow-empty -m "feat: elsewhere"
git -C "$WORK/other" push -q origin 312-test
git commit -q --allow-empty -m "feat: local"
git push origin 312-test > "$WORK/push.out" 2>&1
code=$?
out="$(bash "$OUTAGE" classify "$code" < "$WORK/push.out")"
check "a real rejected push is real, not an outage" test "$out" = "real"
out="$(bash "$OUTAGE" list)"
check "a rejection is not parked" test "$out" = "none parked"

# The status API, through a curl stub that serves a local file.
printf '#!/usr/bin/env bash\ncat "$STATUS_FILE" 2> /dev/null\n' > "$WORK/bin/curl"
chmod +x "$WORK/bin/curl"
export STATUS_FILE="$WORK/status.json"
echo '{"status":{"indicator":"none","description":"All Systems Operational"}}' > "$STATUS_FILE"
out="$(status)"
check "status none" test "$out" = "none"
echo '{"status":{"indicator":"major","description":"Major Service Outage"}}' > "$STATUS_FILE"
out="$(status)"
check "status major is not ok" test "$out" = "major"
rm -f "$STATUS_FILE"
out="$(status)"
check "an unreachable status API says so" test "$out" = "unreachable"

# check-status.sh lists parked work.
bash "$OUTAGE" park 312-test 312 > /dev/null
out="$(METAPHOR_CHECK_LOCK="$WORK/lock" METAPHOR_CHECK_PS_CMD="true" bash "$ROOT/scripts/check-status.sh" 2>&1)"
check "check-status lists parked work" has "parked (GitHub outage): 312-test (issue 312): push"

echo "github-outage tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
