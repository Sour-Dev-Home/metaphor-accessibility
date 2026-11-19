#!/usr/bin/env bash
# Installs the tools `scripts/check.sh docs` runs, at pinned versions, into .tools/ (git-ignored): lychee and Vale from
# their GitHub releases, each archive checked against its SHA-256 before it's unpacked, and markdownlint-cli2 from the
# lockfile in tools/docs/. Runs on Linux x86-64 (CI) and in Git Bash on Windows x86-64. Re-running it is safe.
# `--bin DIR` installs only lychee and Vale, into DIR: scripts/setup-wsl.ps1 puts the Linux builds in the
# metaphor-checks WSL distro that way (#341), leaving the checkout's .tools/ and node_modules to Windows.
#
# To update a tool: change its version and both hashes below. GitHub shows each asset's SHA-256 on the release page.
set -euo pipefail

LYCHEE_VERSION=0.24.2
LYCHEE_SHA256_LINUX=1f4e0ef7f6554a6ed33dd7ac144fb2e1bbed98598e7af973042fc5cd43951c9a
LYCHEE_SHA256_WINDOWS=32975d1493ee1a975d6bb41e4fb56fe419cb442ded628bb772ba2e614acfacad
VALE_VERSION=3.25.1
VALE_SHA256_LINUX=4bf5c5176b682b1da0830f471584d3494f1c2e27ee3029e1ad6f6661d4962182
VALE_SHA256_WINDOWS=5f4b5cefee5d0f7ee0621d7cc035886515888fd732c4e8add36d3c922f2adc68

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin="$root/.tools/bin" bin_only=""
if [ "${1:-}" = --bin ]; then
  [ -n "${2:-}" ] || { echo "install-docs-tools: --bin needs a folder" >&2; exit 2; }
  bin="$2" bin_only=1
fi
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p "$bin"

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) platform=linux ;;
  MINGW*-x86_64 | MSYS*-x86_64 | CYGWIN*-x86_64) platform=windows ;;
  *) echo "install-docs-tools: unsupported platform $(uname -s)-$(uname -m)" >&2; exit 2 ;;
esac

# fetch URL SHA256 FILE: download URL to FILE and fail unless its SHA-256 matches.
fetch() {
  curl -fsSL --retry 3 -o "$3" "$1"
  echo "$2  $3" | sha256sum -c --quiet - || { echo "install-docs-tools: SHA-256 mismatch for $1" >&2; exit 1; }
}

# unpack ARCHIVE NAME: extract ARCHIVE into the work folder and move the binary called NAME into .tools/bin.
unpack() {
  local dir="$work/${2%.exe}"
  mkdir -p "$dir"
  case "$1" in
    *.zip) unzip -qo "$1" -d "$dir" ;;
    *) tar -xzf "$1" -C "$dir" ;;
  esac
  find "$dir" -type f -name "$2" -exec mv -f {} "$bin/$2" \;
  [ -f "$bin/$2" ] || { echo "install-docs-tools: $2 not found in $1" >&2; exit 1; }
  chmod +x "$bin/$2"
}

lychee_url="https://github.com/lycheeverse/lychee/releases/download/lychee-v$LYCHEE_VERSION"
vale_url="https://github.com/errata-ai/vale/releases/download/v$VALE_VERSION"
if [ "$platform" = linux ]; then
  fetch "$lychee_url/lychee-x86_64-unknown-linux-gnu.tar.gz" "$LYCHEE_SHA256_LINUX" "$work/lychee.tar.gz"
  unpack "$work/lychee.tar.gz" lychee
  fetch "$vale_url/vale_${VALE_VERSION}_Linux_64-bit.tar.gz" "$VALE_SHA256_LINUX" "$work/vale.tar.gz"
  unpack "$work/vale.tar.gz" vale
else
  fetch "$lychee_url/lychee-x86_64-pc-windows-msvc.zip" "$LYCHEE_SHA256_WINDOWS" "$work/lychee.zip"
  unpack "$work/lychee.zip" lychee.exe
  fetch "$vale_url/vale_${VALE_VERSION}_Windows_64-bit.zip" "$VALE_SHA256_WINDOWS" "$work/vale.zip"
  unpack "$work/vale.zip" vale.exe
fi

if [ -n "$bin_only" ]; then
  echo "installed into $bin: lychee $LYCHEE_VERSION, vale $VALE_VERSION"
  exit 0
fi
# --ignore-scripts: no package gets to run code at install time.
npm ci --prefix "$root/tools/docs" --ignore-scripts --no-audit --no-fund --loglevel=error

echo "installed into .tools/bin: lychee $LYCHEE_VERSION, vale $VALE_VERSION; markdownlint-cli2 into tools/docs/node_modules"
