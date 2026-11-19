#!/usr/bin/env bash
# Installs the tools `scripts/check.sh docs` runs, at pinned versions, once per clone (#353): lychee and Vale from their
# GitHub releases, each archive checked against its SHA-256 before it's unpacked, and markdownlint-cli2 from the
# lockfile in tools/docs/. Runs on Linux x86-64 (CI) and in Git Bash on Windows x86-64. Re-running it is safe.
#
#   bash scripts/install-docs-tools.sh           install what's missing for this checkout's pins into the clone
#   bash scripts/install-docs-tools.sh --path    print the folders holding them, lychee:vale:markdownlint, for PATH
#   bash scripts/install-docs-tools.sh --bin DIR only lychee and Vale, into DIR: scripts/setup-wsl.ps1 puts the Linux
#                                                builds in the metaphor-checks WSL distro that way (#341)
#
# The clone is the one holding the current folder, and its tools live under its common git dir, docs-tools/, which every
# worktree shares. Each tool gets one folder per pin, named by its version and archive SHA-256 (lychee, Vale) or by the
# SHA-256 of tools/docs/package.json and package-lock.json (markdownlint), so a branch on another pin installs, through
# the hash check, into its own folder, and never changes the binary another worktree runs. A folder only appears once
# complete (unpacked beside it, marked, then renamed); a complete one is never written again, an incomplete one is
# replaced.
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
mode=install bin=""
case "${1:-}" in
  "") ;;
  --path) mode=path ;;
  --bin)
    [ -n "${2:-}" ] || { echo "install-docs-tools: --bin needs a folder" >&2; exit 2; }
    mode=bin bin="$2" ;;
  *) echo "usage: install-docs-tools.sh [--path | --bin DIR]" >&2; exit 2 ;;
esac

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) platform=linux lychee_sha=$LYCHEE_SHA256_LINUX vale_sha=$VALE_SHA256_LINUX ;;
  MINGW*-x86_64 | MSYS*-x86_64 | CYGWIN*-x86_64)
    platform=windows lychee_sha=$LYCHEE_SHA256_WINDOWS vale_sha=$VALE_SHA256_WINDOWS ;;
  *) echo "install-docs-tools: unsupported platform $(uname -s)-$(uname -m)" >&2; exit 2 ;;
esac

exe=""
[ "$platform" = linux ] || exe=.exe

# complete DIR FILE: DIR holds a finished install. Its .installed marker, written last before the rename, names DIR
# (its pin), and FILE, the tool, is there. Anything else, such as an empty or half-copied folder, gets replaced.
complete() {
  local marker=""
  [ -f "$1/.installed" ] && read -r marker < "$1/.installed"
  [ "$marker" = "${1##*/}" ] && [ -f "$1/$2" ]
}

# The clone's folders for this checkout's pins. cd and pwd give a path PATH can hold in Git Bash (/c/..., not C:/...).
if [ "$mode" != bin ]; then
  common="$(git rev-parse --git-common-dir)" && shared="$(cd "$common" && pwd)/docs-tools" ||
    { echo "install-docs-tools: run it inside the repository" >&2; exit 2; }
  lock_sha="$(cat "$root/tools/docs/package.json" "$root/tools/docs/package-lock.json" | sha256sum)" ||
    { echo "install-docs-tools: could not read tools/docs/package.json and package-lock.json" >&2; exit 2; }
  lychee_dir="$shared/lychee-$LYCHEE_VERSION-$lychee_sha"
  vale_dir="$shared/vale-$VALE_VERSION-$vale_sha"
  lint_dir="$shared/markdownlint-${lock_sha%% *}"
  if [ "$mode" = path ]; then
    echo "$lychee_dir:$vale_dir:$lint_dir/node_modules/.bin"
    exit 0
  fi
  # A killed install leaves its .partial-* folder; one an hour old belongs to no running install.
  if [ -d "$shared" ]; then
    find "$shared" -mindepth 1 -maxdepth 1 -name '.partial-*' -mmin +60 -exec rm -rf {} + ||
      echo "install-docs-tools: could not remove an old .partial-* folder in $shared" >&2
  fi
  if complete "$lychee_dir" "lychee$exe" && complete "$vale_dir" "vale$exe" &&
    complete "$lint_dir" node_modules/.bin/markdownlint-cli2; then
    exit 0
  fi
  mkdir -p "$shared"
  # Unpacked next to the folders, so the final rename stays on one disk.
  work="$(mktemp -d "$shared/.partial-XXXXXX")"
else
  mkdir -p "$bin"
  work="$(mktemp -d)"
fi
trap 'rm -rf "$work"' EXIT

# fetch URL SHA256 FILE: download URL to FILE and fail unless its SHA-256 matches.
fetch() {
  curl -fsSL --retry 3 -o "$3" "$1"
  echo "$2  $3" | sha256sum -c --quiet - || { echo "install-docs-tools: SHA-256 mismatch for $1" >&2; exit 1; }
}

# unpack ARCHIVE NAME DEST: extract ARCHIVE into the work folder and move the binary called NAME into DEST.
unpack() {
  local dir="$work/unpacked-${2%.exe}"
  mkdir -p "$dir" "$3"
  case "$1" in
    *.zip) unzip -qo "$1" -d "$dir" ;;
    *) tar -xzf "$1" -C "$dir" ;;
  esac
  find "$dir" -type f -name "$2" -exec mv -f {} "$3/$2" \;
  [ -f "$3/$2" ] || { echo "install-docs-tools: $2 not found in $1" >&2; exit 1; }
  chmod +x "$3/$2"
}

# place TEMP FINAL FILE: mark the finished TEMP folder and rename it to FINAL, moving aside an incomplete FINAL first.
# A complete FINAL that another run put there first stays as it is.
place() {
  echo "${2##*/}" > "$1/.installed"
  if [ -e "$2" ] && ! complete "$2" "$3"; then mv -T "$2" "$work/replaced-${2##*/}"; fi
  mv -T "$1" "$2" 2> /dev/null || complete "$2" "$3" ||
    { echo "install-docs-tools: could not create $2" >&2; exit 1; }
}

# install_tool NAME URL SHA256 DEST: fetch NAME's release archive at URL, check it and put its binary in DEST.
install_tool() {
  local archive="$work/$1.tar.gz" dest="$4"
  [ "$platform" = linux ] || archive="$work/$1.zip"
  [ "$mode" = bin ] || { ! complete "$dest" "$1$exe" || return 0; dest="$work/$1"; }
  fetch "$2" "$3" "$archive"
  unpack "$archive" "$1$exe" "$dest"
  [ "$mode" = bin ] || place "$dest" "$4" "$1$exe"
}

lychee_url="https://github.com/lycheeverse/lychee/releases/download/lychee-v$LYCHEE_VERSION"
vale_url="https://github.com/errata-ai/vale/releases/download/v$VALE_VERSION"
[ "$mode" = bin ] || bin_lychee="$lychee_dir" bin_vale="$vale_dir"
[ "$mode" != bin ] || bin_lychee="$bin" bin_vale="$bin"
if [ "$platform" = linux ]; then
  install_tool lychee "$lychee_url/lychee-x86_64-unknown-linux-gnu.tar.gz" "$lychee_sha" "$bin_lychee"
  install_tool vale "$vale_url/vale_${VALE_VERSION}_Linux_64-bit.tar.gz" "$vale_sha" "$bin_vale"
else
  install_tool lychee "$lychee_url/lychee-x86_64-pc-windows-msvc.zip" "$lychee_sha" "$bin_lychee"
  install_tool vale "$vale_url/vale_${VALE_VERSION}_Windows_64-bit.zip" "$vale_sha" "$bin_vale"
fi

if [ "$mode" = bin ]; then
  echo "installed into $bin: lychee $LYCHEE_VERSION, vale $VALE_VERSION"
  exit 0
fi
if ! complete "$lint_dir" node_modules/.bin/markdownlint-cli2; then
  mkdir -p "$work/markdownlint"
  cp "$root/tools/docs/package.json" "$root/tools/docs/package-lock.json" "$work/markdownlint/"
  # --ignore-scripts: no package gets to run code at install time.
  npm ci --prefix "$work/markdownlint" --ignore-scripts --no-audit --no-fund --loglevel=error
  place "$work/markdownlint" "$lint_dir" node_modules/.bin/markdownlint-cli2
fi

echo "installed into $shared: lychee $LYCHEE_VERSION, vale $VALE_VERSION, markdownlint-cli2 (tools/docs lockfile)"
