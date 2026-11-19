#!/usr/bin/env bash
# The one check command. Local hooks and CI both run it, so their checks never drift apart.
# This file holds only main (the stage dispatch, with the pr-body case), the constants, the module loading and the
# stages that span modules (commits_stage, hooks_path_check, pre_commit_checks). Each concern lives in a module under
# scripts/check/ (report, run, pool, docs, build, message, review, rules); ADR 0013 says why.
#
#   scripts/check.sh fast    repo rules on staged files
#   scripts/check.sh pre-commit   fast, then formatting, docs and signatures when their files are staged (the hook)
#   scripts/check.sh pre-merge-commit   pre-commit for a merge git commits on its own (the hook, #321), without the
#                                 early security-review gate: git writes MERGE_HEAD after this hook, so a clean merge
#                                 can't be told here, and commit-msg, which runs next, judges it
#   scripts/check.sh pre-push     build, Unit and RoundTrip tests when pushed commits change code (the hook; refs on stdin)
#                                 (a passing full or pre-push is recorded per clone by tree hash; the same tree reuses it)
#                                 Tests are selected from the changed files and the project references (test-select.sh,
#                                 issue #243), and Pester runs only for script changes. It prints what ran and why.
#                                 CI and `full` always run everything; METAPHOR_FULL_TESTS=1 does so locally.
#   scripts/check.sh rules   repo rules on every tracked file in CI; locally on the files changed since origin/main
#                            (METAPHOR_SCAN_ALL=1 scans every file; so does `full`'s rules part in CI)
#   scripts/check.sh full    repo rules, build, Unit and RoundTrip tests, formatting, smoke and deploy script tests, check-script suites (CI: Windows in the merge queue, Linux on PRs)
#   scripts/check.sh docs    broken links fail; Markdown lint, Vale and ADR length warn (CI on Ubuntu)
#   scripts/check.sh slow    mutation and property tests and a duplication report, report only (weekly CI on Windows)
#   scripts/check.sh signatures [EXE]    each hook signature matches exactly once in the game exe (local, before a hook PR)
#   scripts/check.sh pr-body FILE        warns when a PR description is too long (CI on pull requests)
#   scripts/check.sh commit-msg FILE     one commit message, plus its security-review trailer (the commit-msg hook)
#   scripts/check.sh commits BASE..HEAD  every non-merge commit message in a range, and HEAD's security review (CI on PRs)
#   scripts/check.sh review-label FILE   the security-reviewed label on a PR not by the maintainer, from the files the
#                                        API lists (the review-gate workflow, from main's copy)
#
# A commit message has a typed subject of at most 72 characters ("feat: ...", "fix(check): ..."), then optionally a blank
# line and a body of at most 5 lines. A final paragraph of trailers (Co-Authored-By: ...) doesn't count. Subjects git
# writes itself (Revert "...", fixup!) pass.
#
# A commit touching the security-review scope (scripts/ci/security-stamp.sh) needs the security-reviewer agent's stamp
# matching the staged scope; the commit-msg hook then appends "Security-Review: <hash>". SECURITY_REVIEW_SKIP="<reason>"
# lets it through and records "Security-Review-Skipped: <reason>" instead. Clean merges from main and CI's HEAD-only
# check: docs/adr/0012-review-trailer-covers-clean-merges.md.
#
# In CI the `commits` stage also reads PR_AUTHOR_ASSOCIATION and PR_AUTHOR (#309). Every PR is judged by its trailers. A
# PR whose author is OWNER or has admin permission is the maintainer's; on any other PR a skip trailer doesn't count.
# Without PR_AUTHOR_ASSOCIATION (local runs) trailers decide. On any other PR the `review-label` stage also requires the
# `security-reviewed` label for a change outside docs/, added by an admin after the PR's latest push (#330).
#
# Rules judge the staged blobs in the index, not the working tree, so they see exactly what would be committed.
# Every step runs even after a failure, so one run shows every problem.
# Exit 0 when everything passes (warnings don't fail), 1 when something fails, 2 on a usage error or when a tool is
# missing or fails to run. The docs stage skips a missing docs tool with a warning unless REQUIRE_DOCS_TOOLS=1 (CI).
#
# Sourced rather than run, the script only defines its functions; `main STAGE [ARG]` then runs a stage, in a subshell
# of the caller since it exits and sets an EXIT trap. tests/check/test-check.sh does so, which saves a bash start per
# case (#328).
set -uo pipefail

# This script's folder, from builtins: under Git Bash each process start costs a lot, and a `cd`/`dirname` subshell is
# two (#328). Git hands the hooks a C:/... path.
check_dir="${BASH_SOURCE[0]%/*}"
[ "$check_dir" != "${BASH_SOURCE[0]}" ] || check_dir=.
[[ "$check_dir" == /* || "$check_dir" =~ ^[A-Za-z]: ]] || check_dir="$PWD/$check_dir"

# The modules, by concern (docs/adr/0013-check-modules-by-concern.md). Each header lists the run globals it reads.
# shellcheck source=check/report.sh
source "$check_dir/check/report.sh" || exit 2
# shellcheck source=check/run.sh
source "$check_dir/check/run.sh" || exit 2
# shellcheck source=check/docs.sh
source "$check_dir/check/docs.sh" || exit 2
# shellcheck source=check/build.sh
source "$check_dir/check/build.sh" || exit 2
# shellcheck source=check/message.sh
source "$check_dir/check/message.sh" || exit 2
# shellcheck source=check/review.sh
source "$check_dir/check/review.sh" || exit 2
# shellcheck source=check/rules.sh
source "$check_dir/check/rules.sh" || exit 2

# main STAGE [ARG]: run one stage, then exit through `finish`.
main() {
  local merge_hook=""
  stage="${1:-}"
  [ "$stage" != pre-merge-commit ] || stage=pre-commit merge_hook=merge
  # live_log is set only once full sources scripts/check/live-log.sh; empty, it tells timed_step and finish (which every stage
  # reaches, under set -u) that there is no live log to write.
  failures=0 warnings=0 reused=built step_times=() tmp="" locked="" waited="" live_log="" full_tree_id=""
  scan_scope="" maintainer_pr_result=""
  started=$EPOCHSECONDS
  case "$stage" in
    fast | pre-commit) timed=("stage 1 ($stage)" 10) ;;
    signatures) timed=("stage 4 (signatures)" 30 "commit $(git rev-parse --short HEAD 2> /dev/null || echo none)") ;;
    *) timed=() ;;
  esac
  case "$stage" in full | slow | signatures | pre-push) run_logged=1 ;; *) run_logged="" ;; esac

  tmp="$(mktemp -d)" || die "could not create a temporary folder"
  trap finish EXIT
  # pre-push takes the lock itself, only once it knows it will build (a docs-only push never waits).
  case "$stage" in
    full)
      full_tree_id="$(full_tree)" || full_tree_id=""
      # shellcheck disable=SC2046 # full_kinds prints separate words
      if [ -n "$full_tree_id" ] && cached_pass "$full_tree_id" $(full_kinds); then
        reused=reused
        echo "full: reused the recorded pass for tree $full_tree_id; no checks ran (change the tree to run them again)"
        exit 0
      fi
      take_lock
      # Only full reads the live log's helpers, so CI's copy of this script for the commits stage needs no more files.
      source "$check_dir/check/live-log.sh" || die "could not load $check_dir/check/live-log.sh"
      local steps=6
      [ -z "${SKIP_SCRIPT_TESTS:-}" ] || steps=4
      live_start full "$steps" ;;
    slow | signatures) take_lock ;;
  esac

  case "$stage" in
    fast | pre-commit | rules | full) ;;
    commit-msg)
      [ -f "${2:-}" ] || die "commit-msg stage needs the message file"
      review_trailer "$2"
      check_message "commit message" < "$2"
      [ "$failures" -eq 0 ]
      exit ;;
    commits)
      commits_stage "${2:-}"
      [ "$failures" -eq 0 ]
      exit ;;
    review-label)
      review_label_stage "${2:-}"
      [ "$failures" -eq 0 ]
      exit ;;
    pr-body)
      [ -f "${2:-}" ] || die "pr-body stage needs the file holding the PR description"
      # The Evidence section holds output and screenshots, so it doesn't count (the /pr template's sections).
      count="$(awk '/^## / { evidence = ($0 ~ /^## +Evidence[[:space:]]*$/) } !evidence' "$2" | prose_words)"
      [ "$count" -le "$max_pr_words" ] ||
        warn "PR description: $count words outside Evidence; descriptions aim for at most $max_pr_words"
      root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
      find_docs_tool_dirs
      if tool="$(find_tool vale)"; then
        # A short relative name, so the warnings read "pr-description.md:LINE" on every platform.
        cp "$2" "$tmp/pr-description.md" && cd "$tmp" || die "could not copy $2"
        check_style "$tool" pr-description.md
      else
        missing_tool vale
      fi
      exit 0 ;;
    docs)
      docs_stage
      [ "$warnings" -eq 0 ] || echo "$warnings warning(s); they don't fail the run"
      [ "$failures" -eq 0 ]
      exit ;;
    slow)
      slow_stage
      [ "$failures" -eq 0 ]
      exit ;;
    signatures)
      signatures_stage "${2:-}"
      [ "$failures" -eq 0 ]
      exit ;;
    pre-push)
      pre_push_stage
      [ "$failures" -eq 0 ]
      exit ;;
    *) echo "usage: scripts/check.sh fast|pre-commit|pre-merge-commit|pre-push|rules|full|docs|slow | signatures [EXE] | commit-msg FILE | commits BASE..HEAD | review-label FILE | pr-body FILE" >&2
       exit 2 ;;
  esac

  # From the root, so a run from a subfolder lists the same files: git diff prints paths from the root, which ls-files
  # would otherwise read from the current folder.
  root="$(git rev-parse --show-toplevel)" || die "could not list files (is this a git repository?)"
  cd "$root" || die "could not enter $root"
  : > "$tmp/changed"
  list_entries > "$tmp/entries" || die "could not list files (is this a git repository?)"
  # A change that lists no files means the listing went wrong; passing it would record a pass that scanned nothing.
  [ ! -s "$tmp/changed" ] || [ -s "$tmp/entries" ] || die "could not list the changed files"
  solution="$root/Metaphor.sln"
  if [ "$stage" = full ]; then
    [ -f "$solution" ] || die "full stage needs $(basename "$solution") at the repository root"
    command -v dotnet > /dev/null || die "full stage needs the .NET SDK (dotnet) on PATH"
    # The docs tools, which the check-script suites and a pre-commit's docs checks run. The clone shares one install
    # per pin (#353), so this fetches only on a clone's first full, or after a pin changes. CI installs its own.
    if [ "${GITHUB_ACTIONS:-}" != true ]; then
      bash "$check_dir/setup/install-docs-tools.sh" ||
        die "could not install the docs tools (run: bash scripts/setup/install-docs-tools.sh)"
    fi
  fi
  scan_files
  case "$stage" in rules | full) hooks_path_check ;; esac
  [ "$stage" != pre-commit ] || pre_commit_checks "$merge_hook"
  [ "$stage" != full ] || full_dotnet_checks
  [ "$failures" -eq 0 ]
}

# hooks_path_check: warn when core.hooksPath is absolute (#321). Git reads a relative one from each worktree's root, so
# each worktree runs its own branch's hooks; an absolute one makes every worktree run that one checkout's hooks, and a
# hook changed on a branch isn't exercised until that checkout has it.
hooks_path_check() {
  local hooks
  hooks="$(git config --get core.hooksPath)" || return 0
  [[ "$hooks" == /* || "$hooks" =~ ^[A-Za-z]:[\\/] ]] || return 0
  warn "core.hooksPath is an absolute path ($hooks), so every worktree runs that checkout's hooks, not its own: run \
git config core.hooksPath scripts/githooks"
}

# commits_stage BASE..HEAD: every non-merge commit's message, then HEAD's security review (check_head_review). Merge
# messages are skipped: git writes them, and PRs are squash-merged.
commits_stage() {
  local shas sha
  shas="$(git rev-list --no-merges "$1" --)" || die "could not list the commits in $1"
  local short
  for sha in $shas; do
    # The short hash on the first line, then the message: one git start where two were. --no-show-signature: a
    # contributor's log.showSignature would print the signature check first (#455).
    git log -1 --no-show-signature --format=%h%n%B "$sha" > "$tmp/message" || die "could not read commit $sha"
    { read -r short && check_message "commit $short"; } < "$tmp/message"
  done
  check_head_review "$1"
}

# pre_commit_checks [merge]: the pre-commit hook's extra checks, each only when its files are staged, so a commit that doesn't
# touch them stays instant. Formatting reads the working tree, not the index: close enough for a hook, and CI checks the
# commit itself.
pre_commit_checks() {
  local staged staged_cs=() line
  staged="$(git diff --cached --name-only --diff-filter=ACMR)" || die "could not list the staged files"
  while IFS= read -r line; do [[ "$line" != *.cs ]] || staged_cs+=("$line"); done <<< "$staged"
  if [ "${#staged_cs[@]}" -gt 0 ]; then
    if [ ! -f "$solution" ]; then
      warn "no ${solution##*/} at the repository root, so formatting wasn't checked"
    elif ! command -v dotnet > /dev/null; then
      warn "dotnet isn't on PATH, so formatting wasn't checked (CI checks it)"
    else
      (cd "$root" && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
        dotnet format whitespace "$solution" --verify-no-changes --include "${staged_cs[@]}") ||
        report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
    fi
  fi
  # Builtin matches on the list, where a grep per check was a start each (#328).
  if [[ $'\n'"$staged"$'\n' == *.md$'\n'* ]]; then
    # In a subshell, which starts its own count: an earlier failure isn't a docs one.
    (failures=0 && docs_stage && [ "$failures" -eq 0 ]) || report "docs: see the problems above"
  fi
  if [[ $'\n'"$staged"$'\n' == *$'\n'src/Metaphor.Core/hook-signatures.json$'\n'* ]]; then
    if [ -n "${METAPHOR_GAME_EXE:-}" ]; then
      (failures=0 && signatures_stage "$METAPHOR_GAME_EXE" && [ "$failures" -eq 0 ]) ||
        report "signatures: see the problems above"
    else
      warn "hook signatures changed: set METAPHOR_GAME_EXE to check them against the game, or run: scripts/check.sh signatures EXE"
    fi
  fi
  # A merge git commits on its own (the pre-merge-commit hook) skips the early review gate: git writes MERGE_HEAD only
  # after that hook, so a clean merge can't be told here, and commit-msg judges it.
  if [ "${1:-}" != merge ] && in_scope; then
    review_gate && skipping &&
      echo "security review: skipped (${SECURITY_REVIEW_SKIP}); the commit records the reason"
  fi
}

# Run when executed; when sourced, stop at the definitions.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then main "$@"; fi
