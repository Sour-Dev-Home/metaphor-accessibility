#!/usr/bin/env bash
# Black-box tests for scripts/setup/install-docs-tools.sh's downloads (#458): curl gets connect and total timeouts, a
# failed download says a proxy may be why and how to point curl at it, and what's fetched and how it's checked don't
# change. curl is a stand-in on PATH, so nothing touches the network; --bin mode keeps the install out of the clone.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
INSTALL="$ROOT/scripts/setup/install-docs-tools.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
unset HTTPS_PROXY https_proxy

passed=0
failed=0
check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '%s\n' "${out:-}"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

# install CURL_BODY: run the installer in --bin mode with a stand-in curl whose body (sh) is CURL_BODY, its arguments
# logged to $WORK/args. Sets out and code; code 124 means it was still running after 20 s. Output goes through a file:
# a stand-in left sleeping would hold a command substitution's pipe open.
install() {
  local stubs="$WORK/stubs-$RANDOM"
  mkdir -p "$stubs"
  printf '#!/bin/sh\nprintf "%%s\\n" "$@" > "%s/args"\n%s\n' "$WORK" "$1" > "$stubs/curl"
  chmod +x "$stubs/curl"
  rm -f "$WORK/args"
  code=0
  PATH="$stubs:$PATH" timeout 20 bash "$INSTALL" --bin "$WORK/bin-$RANDOM" > "$WORK/out" 2>&1 || code=$?
  out="$(cat "$WORK/out")"
}
args_have() { grep -qx -- "$1" "$WORK/args"; }

# A connection that never answers: the stand-in, like curl, gives up with exit 28 once it's given --max-time, and hangs
# without one.
install 'case " $* " in *" --max-time "*) exit 28 ;; esac; sleep 30'
check "a hanging download is cut off by the timeout" test "$code" -eq 1
check "the timeout's failure is reported" has "could not download https://github.com/lycheeverse/lychee/"

# Any failed download names the URL and curl's exit code, and how to point curl at a proxy.
install 'exit 7'
check "a failed download exits 1" test "$code" -eq 1
check "a failed download names the URL and curl's exit code" has "lychee-v0.24.2/lychee-x86_64-"
check "a failed download gives curl's exit code" has "(curl exit 7)"
check "a failed download says a proxy may be the cause" has "proxy"
check "a failed download says how to point curl at the proxy" has "HTTPS_PROXY (or https_proxy)"

# A download that works: same URL, flags and retries as before plus the timeouts, and the SHA-256 check still runs (the
# stand-in's file doesn't match the pin, so the install stops there).
install 'while [ "$1" != -o ]; do shift; done; echo not-the-archive > "$2"'
check "a download is still checked against its pin" has "SHA-256 mismatch for https://github.com/lycheeverse/lychee/"
check "a checked download gives no proxy hint" lacks "HTTPS_PROXY"
check "curl keeps -fsSL" args_have -fsSL
check "curl keeps its 3 retries" grep -qx -- "--retry" "$WORK/args"
check "curl gets a connect timeout" args_have --connect-timeout
check "curl gets a total timeout" args_have --max-time
check "curl fetches the pinned release" grep -q "^https://github.com/lycheeverse/lychee/releases/download/lychee-v0.24.2/" \
  "$WORK/args"

echo "install-docs-tools tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
