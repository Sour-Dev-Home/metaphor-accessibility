#!/usr/bin/env bash
# The must-run security review's stamp (issue #71). The security-review scope is the hook layer plus the scripts that
# deploy, launch, package or fetch native code; a commit touching it needs the security-reviewer agent's stamp.
#
#   scripts/security-stamp.sh                stamp the staged scope (the security-reviewer agent's last step)
#   scripts/security-stamp.sh hash [COMMIT]  print the scope hash of the index, or of COMMIT's tree
#   scripts/security-stamp.sh touches [COMMIT]  exit 0 when the staged changes (or COMMIT, against its first parent)
#                                            touch the scope, 1 when they don't
#   scripts/security-stamp.sh stamped        exit 0 when the stamp matches the staged scope, 1 when it doesn't
#
# The hash is the SHA-256 of "<mode> <blob> <path>" for each scope file, so the index and a commit with the same scope
# content hash the same, on any machine. The stamp is a file in this worktree's git folder, never committed.
# Exit 2 on a usage error or when git fails.
set -uo pipefail

scope=(src/Metaphor.Hooks/ scripts/deploy-dev.ps1 scripts/smoke-test.ps1 scripts/package-mod.sh scripts/fetch-prism.ps1
  scripts/ReloadedDev.psm1)
empty_tree=4b825dc642cb6eb9a060e54bf8d69288fbee4904

die() {
  echo "security-stamp: $*" >&2
  exit 2
}

# scope_hash [COMMIT]: print the hash of the scope in the index, or in COMMIT.
scope_hash() {
  local listing
  if [ -n "${1:-}" ]; then
    listing="$(git ls-tree -r "$1" -- "${scope[@]}")" || die "could not list the scope in $1"
    listing="$(awk -F'\t' '{ split($1, m, " "); print m[1], m[3], $2 }' <<<"$listing")"
  else
    listing="$(git ls-files -s -- "${scope[@]}")" || die "could not list the staged scope"
    listing="$(awk -F'\t' '{ split($1, m, " "); print m[1], m[2], $2 }' <<<"$listing")"
  fi
  printf '%s\n' "$listing" | sha256sum | cut -d' ' -f1
}

# touches [COMMIT]: exit 0 when the scope changed.
touches() {
  local status base
  if [ -n "${1:-}" ]; then
    git diff-tree --quiet -r --root --no-commit-id "$1" -- "${scope[@]}"
  else
    base=HEAD
    git rev-parse -q --verify HEAD > /dev/null || base="$empty_tree"
    git diff --cached --quiet "$base" -- "${scope[@]}"
  fi
  status=$?
  case $status in
    0) return 1 ;;
    1) return 0 ;;
    *) die "could not tell whether the scope changed" ;;
  esac
}

root="$(git rev-parse --show-toplevel)" || die "not inside a git repository"
cd "$root" || die "could not enter $root" # git lists paths relative to the current folder
stamp_file="$(git rev-parse --git-path security-review-stamp)" || die "could not find the git folder"

case "${1:-}" in
  "")
    hash="$(scope_hash)" || exit 2
    printf '%s\n' "$hash" > "$stamp_file" || die "could not write $stamp_file"
    echo "security review stamped: $hash" ;;
  hash) scope_hash "${2:-}" ;;
  touches) touches "${2:-}" ;;
  stamped)
    hash="$(scope_hash)" || exit 2
    [ -f "$stamp_file" ] && [ "$(tr -d '\r\n' < "$stamp_file")" = "$hash" ] ;;
  *) echo "usage: scripts/security-stamp.sh [hash [COMMIT] | touches [COMMIT] | stamped]" >&2
     exit 2 ;;
esac
