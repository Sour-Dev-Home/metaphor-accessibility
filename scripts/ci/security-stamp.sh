#!/usr/bin/env bash
# The must-run security review's stamp (issue #71). The security-review scope is the hook layer, the scripts that
# deploy, launch, package or fetch native code, and the guards that enforce the ground rules (#306); a commit touching
# it needs the security-reviewer agent's stamp.
#
# Commands (scripts/ci/security-stamp.sh COMMAND):
#   (none)                         stamp the staged scope (the security-reviewer agent's last step)
#   hash [COMMIT]                  print the scope hash of the index, or of COMMIT's tree
#   accepted COMMIT                print each hash a Security-Review trailer on COMMIT may carry
#   touches [COMMIT | BASE..HEAD]  exit 0 when the staged changes (or COMMIT, against each parent, or the range
#                                  from its merge base) touch the scope, listing the scope paths that changed; exit 1,
#                                  printing nothing, when they don't. The commit hook asks this same command.
#   stamped                        exit 0 when the stamp matches the staged scope
#   clean-merge [COMMIT]           exit 0 when the merge being committed (or COMMIT) is a clean merge from main, which
#                                  needs no stamp (docs/adr/0012-review-trailer-covers-clean-merges.md)
#   spend                          remove the stamp once HEAD carries its trailer or commits the stamped scope (the
#                                  post-commit and post-merge hooks)
# The yes-or-no commands exit 1 for no.
#
# The hash is the SHA-256 of "<mode> <blob> <path>" for each scope file, so the index and a commit with the same scope
# content hash the same, on any machine. The stamp is a file in this worktree's git folder, never committed.
# Exit 2 on a usage error or when git fails.
set -uo pipefail

# This list is the scope's one home; docs link here instead of restating it. It names directories, so a file added
# under one is in scope with no edit here (#410, docs/adr/0014-review-scope-by-directory.md); there are no exclusions.
# It covers the hook layer, the update check (the mod's one network request, ADR 0018), the installer exe (which runs
# downloaded code on players' PCs, ADR 0020), every script (those that
# deploy, launch, package or fetch native code, and the guards (#306): everything the checks, the git hooks and CI
# run, transitively, plus what they read to decide), and the reviewer's own prompt. tests/check/test-check.sh fails when a script the gate runs falls outside it. The last line is
# what decides dotnet restore, and so the lock files the security job's vulnerability audit reads, in any folder (#322,
# #324): MSBuild's auto-imported files, the SDK pin and the package sources, whose targets or feeds could hide a
# vulnerable package. Entries are git pathspecs, where * also matches /; noglob keeps bash from expanding them, and
# other scripts read this list as text.
set -f
scope=(scripts/ src/Metaphor.Hooks/ src/Metaphor.Features.Updates/ installer/ .github/workflows/ tests/check/ tests/bash/
  tools/docs/ tools/slow/ tools/Metaphor.CpkExtract/ .config/dotnet-tools.json .claude/agents/security-reviewer.md
  *Directory.Build.* *Directory.Packages.props *Directory.Solution.* *.sln.targets *global.json
  *nuget.config *NuGet.config *NuGet.Config)
set +f
# Every pathspec here matches in any case: Windows puts a file staged as Scripts/x.ps1 in scripts/ (#432).
export GIT_ICASE_PATHSPECS=1
# The scope before #306. A commit stamped then carries a hash of these paths; it stays valid while the commit changes
# nothing in the scope outside them. Open PR #294's head was stamped then; drop this list once that PR has merged.
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

# read_parents COMMIT: set the array parents to COMMIT's parents, in order.
read_parents() {
  local line
  line="$(git rev-list --parents -n 1 "$1")" || die "could not read the parents of $1"
  read -r -a parents <<<"$line"
  parents=("${parents[@]:1}")
}

# first_parent COMMIT: print COMMIT's first parent, or the empty tree for a root commit. A merge is compared with its
# first parent, so a change made inside it counts (#174): plain diff-tree shows nothing for a merge.
first_parent() {
  local parents
  read_parents "$1"
  echo "${parents[0]:-$empty_tree}"
}

# merge_heads: set the array merge_heads to the commits being merged into HEAD (none outside a merge).
merge_heads() {
  merge_heads=()
  [ -f "$git_dir/MERGE_HEAD" ] || return 0
  mapfile -t merge_heads < "$git_dir/MERGE_HEAD" || die "could not read $git_dir/MERGE_HEAD"
  merge_heads=("${merge_heads[@]%$'\r'}")
}

# touches [COMMIT]: exit 0 when the scope changed. A merge (COMMIT's, or the one being committed) changes it when it
# differs from any parent, so keeping one side with `-s ours` still counts (ADR 0012).
touches() {
  local status base head parents merge_heads
  touched_paths=""
  if [[ "${1:-}" == *..* ]]; then
    # A range BASE..HEAD (or BASE...HEAD) counts from their merge base, so merge commits can't hide a change.
    base="${1%%..*}" head="${1#*..}"
    touched_paths="$(git diff --exit-code --name-only "$base...${head#.}" -- "${scope[@]}")"
    status=$?
  elif [ -n "${1:-}" ]; then
    read_parents "$1"
    [ "${#parents[@]}" -gt 0 ] || parents=("$empty_tree")
    for base in "${parents[@]}"; do
      touched_paths="$(git diff-tree --exit-code --name-only -r "$base" "$1" -- "${scope[@]}")"
      status=$?
      [ "$status" -eq 0 ] || break
    done
  else
    base=HEAD
    git rev-parse -q --verify HEAD > /dev/null || base="$empty_tree"
    merge_heads
    for base in "$base" "${merge_heads[@]}"; do
      touched_paths="$(git diff --cached --exit-code --name-only "$base" -- "${scope[@]}")"
      status=$?
      [ "$status" -eq 0 ] || break
    done
  fi
  case $status in
    0) return 1 ;;
    1) return 0 ;;
    *) die "could not tell whether the scope changed" ;;
  esac
}

# scope_changes FROM [TO]: print the scope paths that differ between commit FROM and the index (or commit TO), one per
# line. Plumbing with -z and no rename detection, so config can't pair a deleted path with another or quote a name.
scope_changes() {
  local listing path
  if [ -n "${2:-}" ]; then
    listing="$(git diff-tree -r -z --no-renames --name-only "$1" "$2" -- "${scope[@]}" | tr '\0' '\n')"
  else
    listing="$(git diff-index --cached -z --no-renames --name-only "$1" -- "${scope[@]}" | tr '\0' '\n')"
  fi || die "could not compare the scope with $1"
  printf '%s\n' "$listing"
}

# in_list PATH LIST: exit 0 when PATH is one of LIST's lines.
in_list() { [ -n "$1" ] && grep -qxF -- "$1" <<<"$2"; }

# clean_merge [COMMIT]: exit 0 when COMMIT (or the merge being committed) is a clean merge from main, per scope path
# against the parents' merge base: a path kept from one parent is clean only when the other left it as the base had it,
# so an `-s ours` merge or a `checkout --ours` can't drop a change from main (or the branch's own) unreviewed.
clean_merge() {
  local parents merge_heads first second base main=refs/remotes/origin/main ours theirs ours_side theirs_side path
  if [ -n "${1:-}" ]; then
    read_parents "$1"
    [ "${#parents[@]}" -eq 2 ] || return 1
    first="${parents[0]}" second="${parents[1]}"
  else
    merge_heads
    [ "${#merge_heads[@]}" -eq 1 ] || return 1 # not merging, or an octopus merge, which isn't one from main
    first="$(git rev-parse -q --verify HEAD^{commit})" || return 1
    second="${merge_heads[0]}"
  fi
  git rev-parse -q --verify "$main^{commit}" > /dev/null || return 1
  git merge-base --is-ancestor "$second" "$main"
  case $? in
    0) ;;
    1) return 1 ;;
    *) die "could not tell whether $second is on main" ;;
  esac
  # One merge base, or the sides can't be told apart (none, or a criss-cross with several).
  base="$(git merge-base --all "$first" "$second")" || return 1
  [[ -n "$base" && "$base" != *$'\n'* ]] || return 1
  ours="$(scope_changes "$first" "${1:-}")" || exit 2      # paths where the result differs from the first parent
  theirs="$(scope_changes "$second" "${1:-}")" || exit 2   # ... and from the second
  ours_side="$(scope_changes "$base" "$first")" || exit 2   # paths the branch changed since the base
  theirs_side="$(scope_changes "$base" "$second")" || exit 2 # paths main changed since the base
  # Differing from the first parent means the second's version (or neither's): clean only if the branch left it alone.
  while IFS= read -r path; do
    [ -z "$path" ] && continue
    ! in_list "$path" "$theirs" && ! in_list "$path" "$ours_side" || return 1
  done <<<"$ours"
  # Differing from the second parent means the first's version: clean only if main left it alone.
  while IFS= read -r path; do
    [ -z "$path" ] && continue
    ! in_list "$path" "$theirs_side" || return 1
  done <<<"$theirs"
  return 0
}

# One rev-parse for the root and the git dir. The stamp lives in this worktree's own git dir, which is where
# `git rev-parse --git-path security-review-stamp` points for a name git doesn't share between worktrees.
dirs="$(git rev-parse --show-toplevel --absolute-git-dir)" || die "not inside a git repository"
root="${dirs%%$'\n'*}"
[ "$root" != "$dirs" ] || die "could not find the git folder"
git_dir="${dirs#*$'\n'}"
stamp_file="$git_dir/security-review-stamp"
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
    parent="$(first_parent "$2")" || exit 2
    git diff-tree --quiet -r "$parent" "$2" -- "${scope[@]}" "${old_scope[@]/#/:!}"
    case $? in
      0) scope_hash "$2" "${old_scope[@]}" || exit 2 ;;
      1) ;;
      *) die "could not tell whether $2 changes the guards" ;;
    esac ;;
  touches)
    # The answer is the exit code (0 yes, 1 no), the same call the commit hook makes (#875). Silence used to read as
    # "nothing in scope", so a yes now lists the scope paths that changed, and a no prints nothing.
    touches "${2:-}"
    status=$?
    [ "$status" -ne 0 ] || printf '%s\n' "$touched_paths"
    exit "$status" ;;
  stamped)
    hash="$(scope_hash)" || exit 2
    [ -f "$stamp_file" ] && [ "$(tr -d '\r\n' < "$stamp_file")" = "$hash" ] ;;
  clean-merge) clean_merge "${2:-}" ;;
  spend)
    # The post-commit hook: a stamp is single-use, so the commit carrying its trailer removes it (#246). A commit
    # refused at commit-msg never gets here, and keeps the stamp for the retry. `--no-verify` skips commit-msg but not
    # this hook: a commit that changes the scope to the stamped content spends it too, trailer or not (#321).
    [ -f "$stamp_file" ] || exit 0
    hash="$(tr -d '\r\n' < "$stamp_file")"
    trailers="$(git log -1 --no-show-signature --format='%(trailers:key=Security-Review,valueonly)' HEAD)" ||
      die "could not read HEAD" # --no-show-signature: #455
    if [ -n "$hash" ] && grep -qxF -- "$hash" <<<"$trailers"; then
      rm -f "$stamp_file" || die "could not remove $stamp_file"
    elif touches HEAD && [ "$(scope_hash HEAD)" = "$hash" ]; then
      rm -f "$stamp_file" || die "could not remove $stamp_file"
    fi ;;
  *) echo "usage: scripts/ci/security-stamp.sh [hash [COMMIT] | accepted COMMIT | touches [COMMIT | BASE..HEAD] | stamped | clean-merge [COMMIT] | spend]" >&2
     exit 2 ;;
esac
