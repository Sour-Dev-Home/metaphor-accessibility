#!/usr/bin/env bash
# Black-box tests for scripts/run-check-suites.sh (issue #336): it runs tests/check/test-check.sh and every
# tests/scripts/test-*.sh at once, prints each one's summary line and a failing one's whole output, and fails when any
# fails; only test-check.sh gets the live log. CI's repo-rules job and `check.sh full` both run it, so a suite can't run
# in one and not the other: no workflow runs a suite by itself, `full` fails with it, and SKIP_SCRIPT_TESTS skips it.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS GITHUB_ACTIONS SECURITY_REVIEW_SKIP SKIP_SCRIPT_TESTS

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
unset METAPHOR_LIVE_LOG METAPHOR_LIVE_PID
export METAPHOR_NO_WSL=1 # the runners started here run where they are
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

# A checkout of stand-in suites. Each records the live log's variables it sees in $SEEN/<name>, prints a summary line,
# and fails when it's $FAIL_SUITE. helper.sh isn't a suite.
FAKE="$WORK/checkout"
mkdir -p "$FAKE/scripts" "$FAKE/tests/check" "$FAKE/tests/scripts" "$WORK/seen"
cp "$ROOT/scripts/run-check-suites.sh" "$ROOT/scripts/wsl-run.sh" "$FAKE/scripts/"
for suite in tests/check/test-check.sh tests/scripts/test-b.sh tests/scripts/test-a.sh; do
  cat > "$FAKE/$suite" <<'SUITE'
#!/usr/bin/env bash
name="${BASH_SOURCE[0]##*/}"
echo "log ${METAPHOR_LIVE_LOG:-none}, pid ${METAPHOR_LIVE_PID:-none}" > "$SEEN/$name"
echo "$name: a progress line"
if [ "$name" = "${FAIL_SUITE:-}" ]; then echo "$name tests: 0 passed, 1 failed"; exit 1; fi
echo "$name tests: 1 passed, 0 failed"
SUITE
done
echo 'echo not a suite; exit 1' > "$FAKE/tests/scripts/helper.sh"
export SEEN="$WORK/seen"

run_fake() { # run_fake [ARGS...]: run the runner from outside the checkout; sets out and code.
  rm -f "$SEEN"/*
  out="$(cd "$WORK" && bash checkout/scripts/run-check-suites.sh "$@" 2>&1)"
  code=$?
}

run_fake --list
check "list: test-check.sh, then every tests/scripts suite in order" [ "$out" = "$(printf '%s\n' \
  tests/check/test-check.sh tests/scripts/test-a.sh tests/scripts/test-b.sh)" ]

run_fake
check "all pass: exit 0" [ "$code" -eq 0 ]
check "all pass: each suite's summary line" has "test-a.sh tests: 1 passed"
check "all pass: test-check.sh's summary line" has "test-check.sh tests: 1 passed"
check "all pass: no progress lines" lacks "a progress line"
check "all pass: the total" has "check-script suites: 3 passed, 0 failed"
check "all pass: every suite ran" [ "$(ls "$SEEN" | wc -l)" -eq 3 ]
check "not a suite: helper.sh never ran" lacks "not a suite"

FAIL_SUITE=test-b.sh run_fake
check "one fails: exit 1" [ "$code" -eq 1 ]
check "one fails: named" has "FAIL tests/scripts/test-b.sh"
check "one fails: its whole output" has "test-b.sh: a progress line"
check "one fails: the others' output stays short" lacks "test-a.sh: a progress line"
check "one fails: the total" has "check-script suites: 2 passed, 1 failed"

METAPHOR_LIVE_LOG="$WORK/live" METAPHOR_LIVE_PID=42 run_fake
out="$(cat "$SEEN/test-check.sh")"
check "live log: test-check.sh appends to it" [ "$out" = "log $WORK/live, pid 42" ]
out="$(cat "$SEEN/test-a.sh")"
check "live log: the other suites don't see it" [ "$out" = "log none, pid none" ]

# --select (`full`): in Git Bash without WSL, test-check.sh takes minutes, so it runs only when a check script or its
# tests changed since origin/main (scripts/test-select.sh, #243). Elsewhere, in CI and without --select, every suite runs.
cp "$ROOT/scripts/test-select.sh" "$FAKE/scripts/"
(cd "$FAKE" && git init -q -b main && git config user.name test && git config user.email test@example.invalid &&
  git config core.autocrlf false && git config commit.gpgsign false && echo one > notes.md && echo one > scripts/x.sh &&
  git add -A && git commit -q -m "chore: seed" && git update-ref refs/remotes/origin/main HEAD) || exit 2
echo two >> "$FAKE/notes.md"
OSTYPE=msys run_fake --select
check "no WSL, docs only: passes" [ "$code" -eq 0 ]
check "no WSL, docs only: skips test-check.sh" [ ! -e "$SEEN/test-check.sh" ]
check "no WSL, docs only: says so and why" has "skipped tests/check/test-check.sh: no check script or check test changed"
check "no WSL, docs only: runs the other suites" [ -e "$SEEN/test-a.sh" ]
check "no WSL, docs only: counts what ran" has "check-script suites: 2 passed, 0 failed"
OSTYPE=msys run_fake
check "no WSL, without --select: runs test-check.sh" [ -e "$SEEN/test-check.sh" ]
OSTYPE=linux-gnu run_fake --select
check "Linux or WSL: runs test-check.sh" [ -e "$SEEN/test-check.sh" ]
OSTYPE=msys GITHUB_ACTIONS=true run_fake --select
check "CI: runs test-check.sh" [ -e "$SEEN/test-check.sh" ]
echo two >> "$FAKE/scripts/x.sh"
OSTYPE=msys run_fake --select
check "no WSL, a check script changed: runs test-check.sh" [ -e "$SEEN/test-check.sh" ]
check "no WSL, a check script changed: no skip line" lacks "skipped"
(cd "$FAKE" && git checkout -q scripts/x.sh && git update-ref -d refs/remotes/origin/main)
OSTYPE=msys run_fake --select
check "no WSL, no origin/main: runs test-check.sh" [ -e "$SEEN/test-check.sh" ]

# The real list: every suite file, so a new one runs in CI and in `full` with no list to edit.
out="$(bash "$ROOT/scripts/run-check-suites.sh" --list)"
check "the real list is every suite file" [ "$out" = "$(cd "$ROOT" && printf '%s\n' tests/check/test-check.sh \
  tests/scripts/test-*.sh)" ]

# CI runs the suites only through the runner.
out="$(grep -A3 -- '- name: Test the check scripts' "$ROOT/.github/workflows/ci.yml")"
check "CI's step runs the runner" has "run: bash scripts/run-check-suites.sh"
out="$(grep -hvE '^[[:space:]]*#' "$ROOT"/.github/workflows/*.yml | grep -E 'tests/(check|scripts)/test-[A-Za-z0-9_-]+\.sh')"
check "no workflow runs a suite by itself" [ -z "$out" ]

# full: a copy of the scripts with a stand-in runner, Pester runner and dotnet.
GATE="$WORK/gate"
mkdir -p "$GATE" "$WORK/bin"
cp -r "$ROOT/scripts" "$GATE/"
printf '#!/usr/bin/env bash\necho "ran $*" >> "$SUITES_LOG"\nexit "${SUITES_CODE:-0}"\n' \
  > "$GATE/scripts/run-check-suites.sh"
printf '#!/usr/bin/env bash\nexit 0\n' > "$GATE/scripts/run-pester.sh"
printf '#!/usr/bin/env bash\necho "docs tools $*" >> "$DOCS_LOG"\n' > "$GATE/scripts/install-docs-tools.sh"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/bin/dotnet"
chmod +x "$WORK/bin/dotnet"
export PATH="$WORK/bin:$PATH" SUITES_LOG="$WORK/suites.log" DOCS_LOG="$WORK/docs-tools.log" METAPHOR_CHECK_LOCK="$WORK/lock"

run_full() { # run_full: `full` in a new repo; sets out and code.
  rm -rf "$WORK/repo" "$SUITES_LOG" "$DOCS_LOG"
  mkdir -p "$WORK/repo" && cd "$WORK/repo" || exit 2
  git init -q -b main
  git config user.name test && git config user.email test@example.invalid
  git config core.autocrlf false && git config commit.gpgsign false
  : > Metaphor.sln
  echo one > notes.md
  git add -A && git commit -q -m "chore: seed"
  git update-ref refs/remotes/origin/main HEAD
  cd "$WORK" || exit 2
  full_again
}
full_again() { # full_again: `full` again in the same repo; sets out and code.
  rm -f "$SUITES_LOG" "$DOCS_LOG"
  out="$(cd "$WORK/repo" && bash "$GATE/scripts/check.sh" full 2>&1)"
  code=$?
}

run_full
check "full: passes" [ "$code" -eq 0 ]
check "full: runs the suites, selecting where WSL is missing" [ "$(cat "$SUITES_LOG" 2> /dev/null)" = "ran --select" ]
# The clone's docs tools (#353): installing them isn't a test, so a skipped full installs them too; CI installs its own.
check "full: installs the docs tools" [ "$(cat "$DOCS_LOG" 2> /dev/null)" = "docs tools " ]
check "full: times them" has "check-scripts="
check "full: six steps in the live log" grep -q '^== start full 6 ' "$WORK/repo/.git/check-live-log"

SUITES_CODE=1 run_full
check "full: a failing suite fails it" [ "$code" -eq 1 ]
check "full: says which runner to rerun" has "run: bash scripts/run-check-suites.sh"

SKIP_SCRIPT_TESTS=1 run_full
check "SKIP_SCRIPT_TESTS: passes" [ "$code" -eq 0 ]
check "SKIP_SCRIPT_TESTS: skips the suites" [ ! -e "$SUITES_LOG" ]
check "SKIP_SCRIPT_TESTS: still installs the docs tools" [ -e "$DOCS_LOG" ]
SKIP_SCRIPT_TESTS=1 full_again
check "SKIP_SCRIPT_TESTS: its pass serves a skipped full" has "reused"
full_again
check "a skipped full's pass doesn't serve a plain full" [ -e "$SUITES_LOG" ]
check "a skipped full's pass: the plain full ran" lacks "reused"
full_again
check "a plain full's pass is reused" has "reused"
SKIP_SCRIPT_TESTS=1 full_again
check "a plain full's pass serves a skipped full" has "reused"
SKIP_SCRIPT_TESTS=1 GITHUB_ACTIONS=true run_full
check "CI: full leaves the docs tools to its own install step" [ ! -e "$DOCS_LOG" ]

echo "check-suites tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
