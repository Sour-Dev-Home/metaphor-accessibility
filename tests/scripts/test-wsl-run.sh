#!/usr/bin/env bash
# Black-box tests for scripts/wsl-run.sh (issue #341): a suite sourcing it from Git Bash (OSTYPE=msys here) runs in the
# metaphor-checks distro through a stand-in wsl.exe, or falls back to Git Bash with a notice when METAPHOR_NO_WSL is
# set, WSL is missing, the distro is missing or a tool is; on Linux it runs where it is. Then the distro side: a
# missing tool exits 125, and with every tool present it prints their versions and runs the suite with its arguments.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
unset METAPHOR_NO_WSL METAPHOR_TEST_JOBS # set per case
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
starts() { [[ "$out" == "$1"* ]]; }

# A checkout of its own: the wrapper, a tools list and a suite that sources the wrapper like the real ones do.
FAKE="$WORK/checkout"
mkdir -p "$FAKE/scripts" "$FAKE/tests" "$WORK/bin" "$WORK/nowsl"
cp "$ROOT/scripts/wsl-run.sh" "$FAKE/scripts/"
cat > "$FAKE/tests/suite.sh" <<'SUITE'
#!/usr/bin/env bash
OSTYPE="${FAKE_OSTYPE:-$OSTYPE}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT/scripts/wsl-run.sh"
echo "ran in place with: $* (jobs ${METAPHOR_TEST_JOBS:-unset}) (pid ${METAPHOR_LIVE_PID:-unset})"
exit 7
SUITE
# The stand-in wsl.exe: --list prints $WSL_LIST in UTF-16 with CRLFs, as wsl.exe does; anything else records its
# arguments, one per line, and the live log's variables in $WSL_ENV, and exits with $WSL_CODE.
cat > "$WORK/bin/wsl.exe" <<'STUB'
#!/usr/bin/env bash
if [ "$1" = --list ]; then printf '%s\r\n' $WSL_LIST | sed 's/./&\x0/g'; exit 0; fi
printf '%s\n' "$@" > "$WSL_ARGS"
printf 'WSLENV=%s\nLIVE=%s\n' "$WSLENV" "${METAPHOR_LIVE_LOG:-}" > "$WSL_ENV"
exit "$WSL_CODE"
STUB
chmod +x "$WORK/bin/wsl.exe" "$FAKE/tests/suite.sh"
unset METAPHOR_LIVE_LOG METAPHOR_LIVE_PID WSLENV # a run inside `full` inherits them
export WSL_ARGS="$WORK/args" WSL_ENV="$WORK/env" WSL_LIST="Debian metaphor-checks" WSL_CODE=0
# PATH: the stand-in, then the shell's basics only, so no real wsl.exe is found.
base_path="/usr/bin:/bin"

# run_suite [ARGS...]: run the fake suite from Git Bash's point of view; sets out and code.
run_suite() {
  rm -f "$WSL_ARGS"
  out="$(cd "$WORK" && FAKE_OSTYPE=msys PATH="$WORK/bin:$base_path" bash "$FAKE/tests/suite.sh" "$@" 2>&1)"
  code=$?
}

run_suite one "two words"
check "set up: the suite's code comes back" [ "$code" -eq 0 ]
check "set up: the suite doesn't also run in place" lacks "ran in place"
check "set up: no notice" lacks "notice:"
args="$(cat "$WSL_ARGS" 2> /dev/null)"
check "set up: wsl.exe runs the suite in the distro, from the checkout's root, with its arguments" \
  [ "$args" = "$(printf '%s\n' --distribution metaphor-checks --cd "$(cd "$FAKE" && { pwd -W 2> /dev/null || pwd; })" \
    --exec /bin/bash scripts/wsl-run.sh inside tests/suite.sh one "two words")" ]

WSL_CODE=3 run_suite
check "set up: a failing suite's code comes back" [ "$code" -eq 3 ]
out="$(cat "$WSL_ENV" 2> /dev/null)"
check "no live log: WSLENV doesn't carry one" lacks "METAPHOR_LIVE_LOG"

# A run inside `full` (#336): the live log reaches the distro as a path WSL translates, so test-check.sh appends to it.
METAPHOR_LIVE_LOG="$WORK/live" run_suite
out="$(cat "$WSL_ENV" 2> /dev/null)"
check "live log: WSLENV translates it as a path" has "METAPHOR_LIVE_LOG/p"
check "live log: handed over in Windows form" has "LIVE=$(cd "$WORK" && { pwd -W 2> /dev/null || pwd; })/live"

WSL_CODE=125 run_suite one
check "a missing tool (exit 125 in the distro): falls back to Git Bash" has "ran in place with: one"
check "a missing tool: the distro side's notice is the only one" lacks "notice:"

WSL_LIST="Debian docker-desktop" run_suite
check "no distro: falls back with the notice" has "notice: running in Git Bash, not WSL: WSL has no metaphor-checks distro."
check "no distro: wsl.exe never runs a suite" [ ! -e "$WSL_ARGS" ]
check "no distro: the suite's own code" [ "$code" -eq 7 ]

out="$(cd "$WORK" && FAKE_OSTYPE=msys PATH="$WORK/nowsl:$base_path" bash "$FAKE/tests/suite.sh" 2>&1)"
check "no WSL: falls back with the notice" has "notice: running in Git Bash, not WSL: WSL is not installed."

METAPHOR_NO_WSL=1 run_suite
check "METAPHOR_NO_WSL: falls back with the notice" has "notice: running in Git Bash, not WSL: METAPHOR_NO_WSL is set."
check "METAPHOR_NO_WSL: wsl.exe never runs a suite" [ ! -e "$WSL_ARGS" ]

out="$(cd "$WORK" && FAKE_OSTYPE=linux-gnu PATH="$WORK/bin:$base_path" bash "$FAKE/tests/suite.sh" x 2>&1)"
check "Linux: runs in place" has "ran in place with: x"
check "Linux: no notice" lacks "notice:"

# The distro side, run in the fake checkout's root.
printf '# a comment\nbash bash\nno-such-tool-341 some-package\n' > "$FAKE/scripts/wsl-tools.txt"
out="$(cd "$FAKE" && bash scripts/wsl-run.sh inside tests/suite.sh a 2>&1)"
code=$?
check "inside, a tool missing: exit 125" [ "$code" -eq 125 ]
check "inside, a tool missing: one notice naming it and the setup command" [ "$out" = "notice: running in Git Bash, \
not WSL: metaphor-checks lacks no-such-tool-341. Set WSL up with scripts/setup-wsl.ps1 (CONTRIBUTING.md)." ]
check "inside, a tool missing: doesn't run the suite" lacks "ran in place"

printf 'bash bash\n' > "$FAKE/scripts/wsl-tools.txt"
out="$(cd "$FAKE" && bash scripts/wsl-run.sh inside tests/suite.sh a "b c" 2>&1)"
code=$?
check "inside: returns the suite's code" [ "$code" -eq 7 ]
check "inside: runs the suite with its arguments" has "with: a b c"
check "inside: prints the versions first" starts "wsl: metaphor-checks"
check "inside: every core by default" has "(jobs $(nproc))"
out="$(cd "$FAKE" && METAPHOR_TEST_JOBS=2 bash scripts/wsl-run.sh inside tests/suite.sh 2>&1)"
check "inside: METAPHOR_TEST_JOBS overrides the core count" has "(jobs 2)"
out="$(cd "$FAKE" && METAPHOR_LIVE_PID=42 bash scripts/wsl-run.sh inside tests/suite.sh 2>&1)"
check "inside: hands the live log's pid on (scripts/run-check-suites.sh decides who gets it)" has "(pid 42)"

echo "wsl-run tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
