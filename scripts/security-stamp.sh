#!/usr/bin/env bash
# The must-run security review's stamp (issue #71). The security-review scope is the hook layer, the scripts that
# deploy, launch, package or fetch native code, and the guards that enforce the ground rules (#306); a commit touching
# it needs the security-reviewer agent's stamp.
#
#   scripts/security-stamp.sh                stamp the staged scope (the security-reviewer agent's last step)
#   scripts/security-stamp.sh hash [COMMIT]  print the scope hash of the index, or of COMMIT's tree
#   scripts/security-stamp.sh accepted COMMIT  print each hash a Security-Review trailer on COMMIT may carry
#   scripts/security-stamp.sh touches [COMMIT | BASE..HEAD]  exit 0 when the staged changes (or COMMIT, against its
#                                            first parent, or the range from its merge base) touch the scope, 1 when
#                                            they don't
#   scripts/security-stamp.sh stamped        exit 0 when the stamp matches the staged scope, 1 when it doesn't
#
# The hash is the SHA-256 of "<mode> <blob> <path>" for each scope file, so the index and a commit with the same scope
# content hash the same, on any machine. The stamp is a file in this worktree's git folder, never committed.
# Exit 2 on a usage error or when git fails.
set -uo pipefail

# The guards (#306) are everything the checks, the git hooks and CI run, transitively, plus what they read to decide:
# tests/check/test-check.sh fails when one of those scripts falls outside this list. The last line is what dotnet
# restore reads before the security job's vulnerability audit, in any folder (#322): MSBuild's auto-imported files, the
# SDK pin and the package sources, whose targets or feeds could hide a vulnerable package. Entries are git pathspecs,
# where * also matches /; noglob keeps bash from expanding them, and other scripts read this list as text.
set -f
scope=(src/Metaphor.Hooks/ scripts/deploy-dev.ps1 scripts/smoke-test.ps1 scripts/package-mod.sh scripts/fetch-prism.ps1
  scripts/ReloadedDev.psm1 scripts/capture.ps1 scripts/listen.ps1
  scripts/check.sh scripts/game-content.sha256 scripts/tree-hash.sh scripts/ci-changes.sh scripts/test-select.sh
  scripts/stage-timing.sh scripts/install-docs-tools.sh scripts/conflict-alert.sh scripts/integration-sync.sh
  scripts/check-status.sh scripts/github-outage.sh scripts/run-pester.sh scripts/run-check-suites.sh scripts/live-log.sh
  scripts/wsl-run.sh scripts/wsl-tools.txt scripts/setup-wsl.ps1 scripts/githooks/ scripts/security-stamp.sh
  .github/workflows/ tests/check/ tests/scripts/ tools/docs/ tools/slow/ .config/dotnet-tools.json
  .claude/agents/security-reviewer.md
  *Directory.Build.* *Directory.Packages.props *Directory.Solution.* *.sln.targets global.json
  *nuget.config *NuGet.config *NuGet.Config)
set +f
# The scope before #306. A commit stamped then carries a hash of these paths; it stays valid while the commit changes
# nothing in the scope outside them.
old_scope=(src/Metaphor.Hooks/ scripts/deploy-dev.ps1 scripts/smoke-test.ps1 scripts/package-mod.sh
  scripts/fetch-prism.ps1 scripts/ReloadedDev.psm1 scripts/capture.ps1)
empty_tree=4b825dc642cb6eb9a060e54bf8d69288fbee4904

die() {
  echo "security-stamp: $*" >&2
  exit 2
}

# scope_hash [COMMIT [PATHS...]]: print the hash of the scope (or PATHS) in the index, or in COMMIT.
scope_hash() {
  local listing paths=("${scope[@]}")
  [ $# -le 1 ] || paths=("${@:2}")
  if [ -n "${1:-}" ]; then
    # Against the empty tree, which lists the same mode, blob and path as ls-tree, but takes wildcard pathspecs.
    listing="$(git diff-tree -r "$empty_tree" "$1" -- "${paths[@]}")" || die "could not list the scope in $1"
    listing="$(fields 1 3 <<<"$listing")"
  else
    listing="$(git ls-files -s -- "${scope[@]}")" || die "could not list the staged scope"
    listing="$(fields 0 1 <<<"$listing")"
  fi
  listing="$(sha256sum <<<"$listing")" || die "could not hash the scope"
  echo "${listing%% *}"
}

# fields A B: for each "<words>\t<path>" line on stdin print "<word A> <word B> <path>" (words counted from 0), as
# awk -F'\t' '{ split($1, m, " "); print m[A+1], m[B+1], $2 }' did, with builtins: under Git Bash each process start
# costs a lot (#328). The hash depends on this text, so it stays byte for byte the same.
fields() {
  local line words path
  while IFS= read -r line; do
    path=""
    [[ "$line" != *$'\t'* ]] || { path="${line#*$'\t'}" && path="${path%%$'\t'*}"; }
    read -r -a words <<<"${line%%$'\t'*}"
    printf '%s %s %s\n' "${words[$1]:-}" "${words[$2]:-}" "$path"
  done
}

# touches [COMMIT]: exit 0 when the scope changed.
touches() {
  local status base head
  if [[ "${1:-}" == *..* ]]; then
    # A range BASE..HEAD (or BASE...HEAD) counts from their merge base, so merge commits can't hide a change.
    base="${1%%..*}" head="${1#*..}"
    git diff --quiet "$base...${head#.}" -- "${scope[@]}"
  elif [ -n "${1:-}" ]; then
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

# One rev-parse for the root and the git dir. The stamp lives in this worktree's own git dir, which is where
# `git rev-parse --git-path security-review-stamp` points for a name git doesn't share between worktrees.
dirs="$(git rev-parse --show-toplevel --absolute-git-dir)" || die "not inside a git repository"
root="${dirs%%$'\n'*}"
[ "$root" != "$dirs" ] || die "could not find the git folder"
stamp_file="${dirs#*$'\n'}/security-review-stamp"
cd "$root" || die "could not enter $root" # git lists paths relative to the current folder

case "${1:-}" in
  "")
    hash="$(scope_hash)" || exit 2
    printf '%s\n' "$hash" > "$stamp_file" || die "could not write $stamp_file"
    echo "security review stamped: $hash" ;;
  hash) scope_hash "${2:-}" ;;
  accepted)
    # Every hash a Security-Review trailer on COMMIT may carry: its scope hash, and its old-scope hash when it changes
    # nothing in the scope outside the old one (#306).
    [ -n "${2:-}" ] || die "accepted needs a commit"
    scope_hash "$2" || exit 2
    git diff-tree --quiet -r --root --no-commit-id "$2" -- "${scope[@]}" "${old_scope[@]/#/:!}"
    case $? in
      0) scope_hash "$2" "${old_scope[@]}" || exit 2 ;;
      1) ;;
      *) die "could not tell whether $2 changes the guards" ;;
    esac ;;
  touches) touches "${2:-}" ;;
  stamped)
    hash="$(scope_hash)" || exit 2
    [ -f "$stamp_file" ] && [ "$(tr -d '\r\n' < "$stamp_file")" = "$hash" ] ;;
  *) echo "usage: scripts/security-stamp.sh [hash [COMMIT] | accepted COMMIT | touches [COMMIT | BASE..HEAD] | stamped]" >&2
     exit 2 ;;
esac
