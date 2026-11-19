#!/usr/bin/env bash
# Black-box tests for scripts/check/run-pester.sh (#348): it builds the stand-in game once, splits each Pester file into one
# process per test (a file whose Describe has its own BeforeAll runs whole), runs them at once, each with its own
# temporary folder, and reports per file. Stand-ins for pwsh and dotnet record what they were asked to run.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS GITHUB_ACTIONS SECURITY_REVIEW_SKIP SKIP_SCRIPT_TESTS

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
unset METAPHOR_LIVE_LOG METAPHOR_LIVE_PID METAPHOR_STANDIN_BUILT
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

# A checkout with three Pester files: split.Tests.ps1 (three tests, file-level BeforeAll only), shared.Tests.ps1 (a
# Describe-level BeforeAll its two tests share) and one.Tests.ps1 (a single test).
FAKE="$WORK/checkout"
mkdir -p "$FAKE/scripts/check" "$FAKE/tests/pester" "$WORK/bin" "$WORK/seen"
cp "$ROOT/scripts/check/run-pester.sh" "$ROOT/scripts/check/live-log.sh" "$ROOT/scripts/check/pool.sh" "$FAKE/scripts/check/"
cat > "$FAKE/tests/pester/split.Tests.ps1" <<'PS1'
BeforeAll {
    function Get-Thing { 1 }
}
Describe 'split' {
    BeforeEach { $x = 1 }
    It 'one' { 1 | Should -Be 1 }
    It 'two' {
        2 | Should -Be 2
    }
    It 'three' -ForEach @(1, 2) { $_ | Should -BeGreaterThan 0 }
}
PS1
cat > "$FAKE/tests/pester/shared.Tests.ps1" <<'PS1'
Describe 'shared' {
    BeforeAll { $script:result = 'run once' }
    It 'a' { $result | Should -Be 'run once' }
    It 'b' { $result | Should -Be 'run once' }
}
PS1
cat > "$FAKE/tests/pester/one.Tests.ps1" <<'PS1'
Describe 'one' {
    It 'only' { 1 | Should -Be 1 }
}
PS1
(cd "$FAKE" && git init -q && git -c core.autocrlf=false add -A) || exit 2

# pwsh: records FILE, LINES, METAPHOR_STANDIN_BUILT and TEMP (and whether TEMP exists), counts how many run at once, and
# fails when FILE ends in $FAIL_FILE. dotnet: records each call.
cat > "$WORK/bin/pwsh" <<'STUB'
#!/usr/bin/env bash
temp_ok=no
[ -d "$TEMP" ] && [ "$TEMP" = "$TMP" ] && temp_ok=yes
echo "${FILE}|${LINES:-}|${METAPHOR_STANDIN_BUILT:-}|$TEMP|$temp_ok" >> "$SEEN/pwsh.$BASHPID"
echo "$FILE" >> "$SEEN/order"
: > "$SEEN/running.$BASHPID"
ls "$SEEN" | grep -c '^running\.' >> "$SEEN/concurrent"
sleep 1
rm -f "$SEEN/running.$BASHPID"
echo "output of ${FILE##*/} ${LINES:-whole}"
total="$(grep -ciE '^[[:space:]]*It[[:space:]]' "$FILE")"
ran="$total"
[ -z "${LINES:-}" ] || ran="$(wc -w <<< "$LINES")"
# The last line ends in CRLF, as pwsh writes it on Windows.
if [ -n "${KILL_FILE:-}" ] && [[ "$FILE" == *"$KILL_FILE" ]]; then kill -9 $$; fi
if [ -n "${EMPTY_FILE:-}" ] && [[ "$FILE" == *"$EMPTY_FILE" ]]; then exit 0; fi
if [ -n "${FAIL_FILE:-}" ] && [[ "$FILE" == *"$FAIL_FILE" ]]; then printf 'pester-counts 0 %s %s %s\r\n' "$ran" "$ran" "$total"; exit 1; fi
if [ -n "${MISS_FILE:-}" ] && [[ "$FILE" == *"$MISS_FILE" ]]; then ran=$((ran - 1)); fi
printf 'pester-counts %s 0 %s %s\r\n' "$ran" "$ran" "$total"
STUB
printf '#!/usr/bin/env bash\necho "dotnet $*" >> "$SEEN/dotnet"\nexit "${DOTNET_CODE:-0}"\n' > "$WORK/bin/dotnet"
chmod +x "$WORK/bin/pwsh" "$WORK/bin/dotnet"
export SEEN="$WORK/seen" PATH="$WORK/bin:$PATH" METAPHOR_TEST_JOBS=8 METAPHOR_PESTER_BATCH=1

run_fake() { # run_fake [ARGS...]: run the runner in the checkout; sets out, code and calls (one line per pwsh run).
  rm -f "$SEEN"/*
  out="$(cd "$FAKE" && bash scripts/check/run-pester.sh "$@" 2>&1)"
  code=$?
  calls="$(cat "$SEEN"/pwsh.* 2> /dev/null | sort)"
}
lines_of() { # lines_of FILE: the LINES each run of FILE got, one run per line, sorted
  grep "^tests/pester/$1|" <<< "$calls" | cut -d '|' -f 2 | sort
}

run_fake
check "all pass: exit 0" [ "$code" -eq 0 ]
check "split: one process per test, each test once" [ "$(lines_of split.Tests.ps1)" = "$(printf '%s\n' 10 6 7)" ]
check "shared: a Describe-level BeforeAll keeps the file whole" [ "$(lines_of shared.Tests.ps1)" = "" ]
check "shared: in one process" [ "$(grep -c '^tests/pester/shared' <<< "$calls")" -eq 1 ]
check "one test: runs whole" [ "$(lines_of one.Tests.ps1)" = "" ]
check "five processes in all" [ "$(wc -l <<< "$calls")" -eq 5 ]
check "they run at once" [ "$(sort -n "$SEEN/concurrent" | tail -n 1)" -ge 2 ]
check "the stand-in game is built once" [ "$(cat "$SEEN/dotnet")" = \
  "dotnet build tests/Metaphor.StandInGame -c Release -v q --nologo" ]
check "every process is told it's built" [ -z "$(cut -d '|' -f 3 <<< "$calls" | grep -v '^1$')" ]
check "every process has its own temporary folder" [ "$(cut -d '|' -f 4 <<< "$calls" | sort -u | wc -l)" -eq 5 ]
check "each folder exists while it runs" [ -z "$(cut -d '|' -f 5 <<< "$calls" | grep -v '^yes$')" ]
first_temp="$(head -n 1 <<< "$calls" | cut -d '|' -f 4)"
check "the folders are gone afterwards" [ ! -e "$first_temp" ]
check "a live line per file" [ "$(grep -cE '^\[[0-9]/3\] tests/pester/[a-z]+\.Tests\.ps1 ok \([0-9]+ s\)$' <<< "$out")" -eq 3 ]
check "a summary per file, with its test count and processes" has \
  "== tests/pester/split.Tests.ps1 ok: 3 passed, 0 failed, 3 processes"
check "a passing file's output stays short" lacks "output of split.Tests.ps1"
check "the total" has "run-pester: 3 files passed, 0 failed"

FAIL_FILE=split.Tests.ps1 run_fake
check "one fails: exit 1" [ "$code" -eq 1 ]
check "one fails: its live line" has "tests/pester/split.Tests.ps1 FAIL"
check "one fails: its summary" has "== tests/pester/split.Tests.ps1 FAIL"
check "one fails: its whole output" has "output of split.Tests.ps1 7"
check "one fails: the others' output stays short" lacks "output of one.Tests.ps1"
check "one fails: the total" has "run-pester: 2 files passed, 1 failed"

MISS_FILE=one.Tests.ps1 run_fake
check "a test that didn't run: exit 1" [ "$code" -eq 1 ]
check "a test that didn't run: says so" has "== tests/pester/one.Tests.ps1 FAIL: 0 passed, 0 failed, 1 processes"
check "a test that didn't run: how many ran" has "; ran 0 of its 1 tests"

# A process that is killed or prints nothing fails its file (#367: the shared pool keeps #348's failure modes).
KILL_FILE=one.Tests.ps1 run_fake
check "a killed process: exit 1" [ "$code" -eq 1 ]
check "a killed process: its file fails" has "== tests/pester/one.Tests.ps1 FAIL"
check "a killed process: the others still run" has "== tests/pester/split.Tests.ps1 ok"
check "a killed process: the total" has "run-pester: 2 files passed, 1 failed"
EMPTY_FILE=one.Tests.ps1 run_fake
check "an empty process: exit 1" [ "$code" -eq 1 ]
check "an empty process: its file fails, having run nothing" has "== tests/pester/one.Tests.ps1 FAIL: 0 passed, 0 failed, 1 processes"

METAPHOR_PESTER_BATCH=2 run_fake
check "two per process: dealt in turn, each test once" [ "$(lines_of split.Tests.ps1)" = "$(printf '%s\n' '6 10' 7)" ]
METAPHOR_PESTER_BATCH='' run_fake
check "default four per process: three tests run whole" [ "$(lines_of split.Tests.ps1)" = "" ]
check "default four per process: one process per file" [ "$(wc -l <<< "$calls")" -eq 3 ]

run_fake tests/pester/one.Tests.ps1
check "a file argument: only that file runs" [ "$(wc -l <<< "$calls")" -eq 1 ]

DOTNET_CODE=1 run_fake
check "the stand-in game fails to build: exit 1" [ "$code" -eq 1 ]
check "the stand-in game fails to build: says so" has "the stand-in game didn't build"
check "the stand-in game fails to build: runs nothing" [ -z "$calls" ]

METAPHOR_TEST_JOBS=1 run_fake
check "one job: still passes" [ "$code" -eq 0 ]
check "one job: one at a time" [ "$(sort -n "$SEEN/concurrent" | tail -n 1)" -eq 1 ]

# Longest first: the clone's git dir keeps each file's slowest process from the last run, and the next run starts the
# slowest file first; a file with no record goes before the rest.
times="$FAKE/.git/pester-times"
check "a record of the last run" grep -qE '^tests/pester/split\.Tests\.ps1 [0-9]+$' "$times"
printf '%s\n' 'tests/pester/split.Tests.ps1 1' 'tests/pester/one.Tests.ps1 50' 'tests/pester/shared.Tests.ps1 9' > "$times"
METAPHOR_TEST_JOBS=1 run_fake
check "the slowest file starts first" [ "$(cut -d / -f 3 "$SEEN/order" | uniq | tr '\n' ' ')" = \
  "one.Tests.ps1 shared.Tests.ps1 split.Tests.ps1 " ]
printf '%s\n' 'tests/pester/one.Tests.ps1 50' > "$times"
METAPHOR_TEST_JOBS=1 run_fake
check "a file with no record starts before the rest" [ "$(head -n 1 "$SEEN/order")" != tests/pester/one.Tests.ps1 ]
check "the record covers every file again" [ "$(wc -l < "$times")" -eq 3 ]

# The real files: each one that builds the stand-in game skips the build when run-pester.sh has built it, so its
# processes don't each start MSBuild. CI runs the files directly, where they still build it.
out="$(cd "$ROOT" && grep -l 'dotnet build .*Metaphor.StandInGame' tests/pester/*.Tests.ps1 | while read -r file; do
  grep -qF 'if (-not $env:METAPHOR_STANDIN_BUILT) {' "$file" || echo "$file"
done)"
check "every real file's stand-in build is skipped once built" [ -z "$out" ]
# A stopped game must have exited before the test goes on: Pester's TestDrive cleanup fails on a DLL it still holds,
# which a loaded machine (many processes at once) makes likely.
out="$(cd "$ROOT" && grep -n 'Stop-Process' tests/pester/*.Tests.ps1 | grep -v 'Stop-Process -Force -PassThru | Wait-Process')"
check "every real file waits for the game it stops" [ -z "$out" ]
# And each file that copies the stand-in into TestDrive clears it in an AfterAll first (tests/pester/StandIn.psm1): an
# exited game's files stay locked for a moment, and a game still starting has no Path to stop it by.
out="$(cd "$ROOT" && grep -l 'Metaphor.StandInGame/bin' tests/pester/*.Tests.ps1 | while read -r file; do
  grep -qF 'Stop-StandIn $TestDrive' "$file" || echo "$file"
done)"
check "every real file that copies the stand-in clears TestDrive of it" [ -z "$out" ]

echo "run-pester tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
