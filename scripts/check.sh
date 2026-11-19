#!/usr/bin/env bash
# The one check command. Local hooks and CI both run it, so their checks never drift apart.
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
#   scripts/check.sh slow    mutation testing and a duplication report, report only (weekly CI on Windows)
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
# A commit touching the security-review scope (scripts/security-stamp.sh) needs the security-reviewer agent's stamp
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

# list_entries prints "<mode> <blob> <stage>\t<path>\0" for each file to check, in `git ls-files -s -z` form. Changed
# files are every change but a deletion (--diff-filter=d), so a symlink turned into a file is scanned too. They are
# listed in $tmp/changed, with paths from the repository root: the script runs from there.
# fast and pre-commit scan the staged changes. Elsewhere CI, or METAPHOR_SCAN_ALL=1, scans every tracked file: CI does
# so before anything merges. Otherwise the scan covers the files changed since the merge-base with origin/main, staged
# changes included (issue #297). Without origin/main, or when the rules themselves change, it scans every file.
list_entries() {
  local base
  case "$stage" in
    fast | pre-commit)
      git diff --cached --name-only --diff-filter=d -z > "$tmp/changed" && changed_entries ;;
    *)
      if [ "${GITHUB_ACTIONS:-}" != true ] && [ -z "${METAPHOR_SCAN_ALL:-}" ] &&
        base="$(git merge-base HEAD origin/main 2> /dev/null)" &&
        git diff --cached --name-only --diff-filter=d -z "$base" -- > "$tmp/changed" &&
        ! grep -qzxE 'scripts/(check\.sh|game-content\.sha256)' "$tmp/changed"; then
        scan_scope="changed since origin/main"
        changed_entries
      else
        scan_scope=all
        git ls-files -s -z
      fi ;;
  esac
}

# changed_entries: print the index entries of the paths in $tmp/changed. One `git ls-files` of the whole index, kept
# when the path is listed, instead of an xargs and a `git ls-files` per batch (#328): the changed paths are files, so
# matching them exactly lists what a literal pathspec per path would, in the same index order.
changed_entries() {
  local path entry
  local -A wanted=()
  [ -s "$tmp/changed" ] || return 0
  while IFS= read -r -d '' path; do wanted[$path]=1; done < "$tmp/changed"
  git ls-files -s -z > "$tmp/index" || return
  while IFS= read -r -d '' entry; do
    [ -z "${wanted[${entry#*$'\t'}]:-}" ] || printf '%s\0' "$entry"
  done < "$tmp/index"
}

# main STAGE [ARG]: run one stage, then exit through `finish`.
main() {
  local merge_hook=""
  stage="${1:-}"
  [ "$stage" != pre-merge-commit ] || stage=pre-commit merge_hook=merge
  # live_log is set only once full sources scripts/live-log.sh; empty, it tells timed_step and finish (which every stage
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
      source "$check_dir/live-log.sh" || die "could not load $check_dir/live-log.sh"
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
      bash "$check_dir/install-docs-tools.sh" ||
        die "could not install the docs tools (run: bash scripts/install-docs-tools.sh)"
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
    # The short hash on the first line, then the message: one git start where two were.
    git log -1 --format=%h%n%B "$sha" > "$tmp/message" || die "could not read commit $sha"
    { read -r short && check_message "commit $short"; } < "$tmp/message"
  done
  check_head_review "$1"
}

# Policy, not a measurement: our code and notes have no reason to exceed this, so anything bigger needs a look.
max_bytes=$((1024 * 1024))

# read_known_hashes: set known_hashes to the known game-file hashes in the index's scripts/game-content.sha256, lower
# case, one per line. The file holds one SHA-256 per line, then a label naming its game build; '#' starts a comment.
# One `git cat-file --batch` and builtins, where a `git cat-file -e`, `git show`, tr, sed and awk were five starts (#328).
read_known_hashes() {
  local listing head line
  known_hashes=""
  listing="$(git cat-file --batch <<< :scripts/game-content.sha256)" || die "could not read scripts/game-content.sha256"
  head="${listing%%$'\n'*}"
  [[ "$head" == *" missing" ]] && return 0
  [[ "$head" =~ ^[0-9a-f]+\ blob\ [0-9]+$ ]] || die "could not read scripts/game-content.sha256"
  [ "$listing" != "$head" ] || return 0 # an empty file
  while IFS= read -r line; do
    line="${line//$'\r'/}" && line="${line%%#*}"
    [[ "$line" =~ ^[[:blank:]]*([^[:blank:]]+) ]] && known_hashes+="${BASH_REMATCH[1],,}"$'\n'
  done <<< "${listing#*$'\n'}"
}

# pattern_lines: copy stdin's lines to stdout without CRs, leading or trailing spaces, comments or blank lines.
pattern_lines() {
  local line
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line//$'\r'/}"
    line="${line#"${line%%[![:space:]]*}"}" && line="${line%"${line##*[![:space:]]}"}"
    [ -z "$line" ] || [ "${line:0:1}" = "#" ] || printf '%s\n' "$line"
  done
}

# read_private_patterns: write the private personal-information patterns to $tmp/private, one per line. They are
# matched as fixed strings so special characters can't break the scan. The git-ignored .claude/pii-patterns.txt holds
# them locally; CI passes them in PII_PATTERNS. One per line, '#' comments.
read_private_patterns() {
  if [ -n "${PII_PATTERNS:-}" ]; then
    pattern_lines <<< "$PII_PATTERNS"
  elif [ -f "$root/.claude/pii-patterns.txt" ]; then
    pattern_lines < "$root/.claude/pii-patterns.txt"
  fi > "$tmp/private"
  if [ ! -s "$tmp/private" ]; then
    # CI sets REQUIRE_PRIVATE_PATTERNS on pushes to main, where a missing secret must not pass quietly.
    [ "${REQUIRE_PRIVATE_PATTERNS:-}" = 1 ] && die "no private patterns, but REQUIRE_PRIVATE_PATTERNS=1 (is the secret set?)"
    echo "warning: no private patterns, so personal information is checked against general patterns only." \
      "Copy .claude/pii-patterns.example.txt to .claude/pii-patterns.txt and fill it in." >&2
  fi
}

report_game_content() {
  report "game content: $1: $2"
}

# General personal-information patterns run everywhere, CI included. Unlike the private list they are regular
# expressions, which is safe because we write and test them here.
# User folders: C:\Users\<name>, /c/Users/<name> (Git Bash), /mnt/c/Users/<name> (WSL), /home/<name>, /Users/<name>.
user_path_re='[A-Za-z]:[\\/]+[Uu][Ss][Ee][Rr][Ss][\\/]+[A-Za-z0-9_]'
user_path_re+='|(^|[^A-Za-z0-9_.:-])((/mnt)?/[A-Za-z]/[Uu]sers|/home|/Users)/[A-Za-z0-9_]'
email_re='[a-z0-9._%+-]+@[a-z0-9-]+(\.[a-z0-9-]+)*\.[a-z]{2,}'
# Email-shaped text that can't belong to a person: reserved example domains, GitHub's no-reply addresses, SSH remotes
# (git@host) and high-resolution image names (icon@2x.png).
not_email_re='@([a-z0-9-]+\.)*(example\.(com|org|net)|[a-z0-9-]+\.(invalid|example|test)|users\.noreply\.github\.com)$'
not_email_re+='|^git@|@[0-9]+x\.[a-z]+$'

# The scan (issue #297). Process starts dominate the run time in Git Bash, so nothing starts once per file: one
# `git cat-file --batch` writes every blob to $blobs/<n> (n is its index in `paths`), one sha256sum pass hashes them,
# and each rule runs one grep over all of them. Findings are gathered per file, then printed file by file in index
# order, exactly as a per-file loop would print them. scan_files sets blobs to $tmp/blobs.

# scan_lines FOUND REASON SKIP-RE GREP-ARGS...: add a finding to the array named FOUND, per file, for each line where
# grep finds a match, ignoring matches that SKIP-RE (lower case; empty skips nothing) accepts. Only the line number is
# printed, never the matched text, because CI logs are public.
scan_lines() {
  local -n found_in="$1"
  local reason="$2" skip="$3" status line n rest number text last=""
  shift 3
  grep -rnao "$@" -- "$blobs" > "$tmp/matches"
  status=$?
  [ "$status" -le 1 ] || die "could not scan the files to check"
  # grep reads one file at a time, so a file's matches are together and in line order.
  while IFS= read -r line; do
    line="${line#"$blobs"/}" && n="${line%%:*}" && rest="${line#*:}" && number="${rest%%:*}" && text="${rest#*:}"
    [ -n "$skip" ] && [[ "${text,,}" =~ $skip ]] && continue
    [ "$n:$number" = "$last" ] && continue
    last="$n:$number"
    found_in[n]+="personal information: ${paths[n]}:$number: $reason"$'\n'
  done < "$tmp/matches"
}

# header_has HAS LABEL VALUE-RE: set HAS[n] for each note whose header (the lines before its first "## " heading) has a
# "- **LABEL:** value" line whose value matches VALUE-RE. The label matches as a word in any case, plural or inside
# longer wording ("Date saved:", "Game build(s) the sources mention:"), with the colon inside or after the bold. One
# opening quote before the value (`, ", ' or <) is skipped. $tmp/headers holds every note's header lines as "n:line".
header_has() {
  local -n has_line="$1"
  local line
  # -a: GNU grep 3.5+ withholds the lines of a file with invalid UTF-8 ("binary file matches") and still exits 0.
  grep -aiE "^[0-9]+:- \*\*([^*]* )?${2}s?([^*a-z][^*]*)?(:\*\*|\*\*:)[[:space:]]*[\`\"'<]?${3}" "$tmp/headers" \
    > "$tmp/header-lines"
  [ $? -le 1 ] || die "could not read the note headers"
  while IFS= read -r line; do has_line[${line%%:*}]=1; done < "$tmp/header-lines"
}

# emit FINDINGS: report each line of FINDINGS (each ends in a newline).
emit() {
  local rest="$1"
  while [ -n "$rest" ]; do
    report "${rest%%$'\n'*}"
    rest="${rest#*$'\n'}"
  done
}

# scan_files: the file rules on the entries list_entries wrote to $tmp/entries.
scan_files() {
  local scan_started=$SECONDS entry meta hash size i n path lower line idlist hash_them=0
  local -A known=()
  blobs="$tmp/blobs"
  read_known_hashes
  read_private_patterns
  for hash in $known_hashes; do known[$hash]=1; done
  paths=() ids=() sizes=() notes=()
  found_hash=() found_user=() found_email=() found_name=() found_private=() has_source=() has_saved=() has_build=()
  has_sha=()
  while IFS= read -r -d '' entry; do
    meta="${entry%%$'\t'*}" # "<mode> <blob> <stage>"
    [ "${meta%% *}" = 160000 ] && continue # a submodule: its content lives in another repository
    meta="${meta#* }"
    paths+=("${entry#*$'\t'}") ids+=("${meta%% *}")
  done < "$tmp/entries"

  if [ "${#paths[@]}" -gt 0 ]; then
    command -v perl > /dev/null || die "the file scan needs perl (Git for Windows and Linux include it)"
    # Notes cite their sources and pin their game build (AGENTS.md ground rules 1 and 2).
    for i in "${!paths[@]}"; do
      case "${paths[i]}" in
        docs/raw-sources/README.md | docs/findings/README.md) ;;
        docs/raw-sources/*.md | docs/findings/*.md) notes+=("$i") ;;
      esac
    done
    [ -z "$known_hashes" ] || hash_them=1
    # Each blob as "<id> blob <size>\n<content>\n". perl writes the content to $blobs/<n>, prints its size (and its
    # SHA-256 when there are known hashes to match), and writes each note's header lines (those before its first "## "
    # heading) to $tmp/headers as "<n>:<line>". One perl start where a sha256sum and an awk pass, each behind an xargs
    # and two subshells, were eight (#328).
    printf -v idlist '%s\n' "${ids[@]}"
    git cat-file --batch <<< "${idlist%$'\n'}" | perl -e '
      binmode STDIN;
      my ($dir, $hash_them, $headers, @notes) = @ARGV;
      my %note = map { $_ => 1 } @notes;
      require Digest::SHA if $hash_them;
      mkdir($dir) or die "check: could not create $dir: $!\n";
      open(my $hdr, ">:raw", $headers) or die "check: $headers: $!\n";
      my $n = 0;
      while (defined(my $head = <STDIN>)) {
        my ($id, $type, $size) = split / /, $head;
        die "check: object $id is missing\n" unless defined $size && $type eq "blob";
        chomp $size;
        my $data = "";
        while (length($data) < $size) {
          read(STDIN, $data, $size - length($data), length($data)) or die "check: object $id is cut short\n";
        }
        getc(STDIN);
        open(my $out, ">:raw", "$dir/$n") or die "check: $dir/$n: $!\n";
        print $out $data;
        close($out) or die "check: $dir/$n: $!\n";
        if ($note{$n}) {
          open(my $in, "<", \$data) or die "check: note $n: $!\n";
          while (defined(my $line = <$in>)) {
            chomp $line;
            last if $line =~ /^## /;
            print $hdr "$n:$line\n";
          }
        }
        print $hash_them ? "$size " . Digest::SHA::sha256_hex($data) . "\n" : "$size\n";
        $n++;
      }
      close($hdr) or die "check: $headers: $!\n";' "$blobs" "$hash_them" "$tmp/headers" "${notes[@]}" > "$tmp/sizes" ||
      die "could not read the files to check"
    n=0
    while read -r size hash; do
      sizes+=("$size")
      [ "$hash_them" -eq 0 ] || [ -z "${known[$hash]:-}" ] || found_hash[n]=1
      n=$((n + 1))
    done < "$tmp/sizes"
    [ "${#sizes[@]}" -eq "${#paths[@]}" ] || die "could not read the files to check"

    if [ "${#notes[@]}" -gt 0 ]; then
      header_has has_source Source '[[:alnum:]]'
      header_has has_saved Saved '[0-9]{4}-[0-9]{2}-[0-9]{2}([^0-9a-z]|$)'
      header_has has_build "Game build" '[[:alnum:]]'
      # A finding names its game build by the executable's SHA-256 (GLOSSARY.md).
      header_has has_sha "Game build" '[0-9a-f]{64}([^0-9a-f]|$)'
    fi

    scan_lines found_user "local user path" "" -E "$user_path_re"
    scan_lines found_email "email address" "$not_email_re" -iE "$email_re"
    if [ -s "$tmp/private" ]; then
      # One name per line, so grep's line number is the file's index plus one.
      printf '%s\n' "${paths[@]//$'\n'/ }" > "$tmp/names"
      grep -naiF -f "$tmp/private" "$tmp/names" > "$tmp/name-matches" # -a as in header_has
      [ $? -le 1 ] || die "could not scan the file names"
      while IFS= read -r line; do found_name[${line%%:*} - 1]=1; done < "$tmp/name-matches"
      scan_lines found_private "matches a private pattern" "" -iF -f "$tmp/private"
    fi
  fi

  for i in "${!paths[@]}"; do
    path="${paths[i]}"
    # Game folders are the git-ignored ones at the repo root. Lower-cased because Windows paths are case-insensitive.
    lower="${path,,}"
    case "$lower" in
      game/* | extracted/* | dumps/* | captures/* | ghidra/*) report_game_content "$path" "inside a game folder" ;;
    esac
    case "/$lower" in
      *.rep/*) report_game_content "$path" "inside a disassembler project folder" ;;
      *.cpk | *.pak) report_game_content "$path" "game archive extension" ;;
      # Our own builds are blocked too: they live as CI artifacts tied to a commit, never in the repository.
      *.exe | *.dll | *.asi) report "game content or build output: $path: executable extension" ;;
      *.dmp | *.mdmp) report_game_content "$path" "memory dump extension" ;;
      *.gpr | *.gzf | *.gbf | *.idb | *.i64 | *.til | *.nam | *.id0 | *.id1 | *.id2 | *.bndb | *.dd64)
        report_game_content "$path" "disassembler database extension" ;;
    esac

    size="${sizes[i]}"
    [ "$size" -le "$max_bytes" ] || report_game_content "$path" "larger than $max_bytes bytes ($size)"
    [ -z "${found_hash[i]:-}" ] || report_game_content "$path" "hash matches a known game file"

    case "$path" in
      docs/raw-sources/README.md | docs/findings/README.md) ;;
      docs/raw-sources/*.md)
        [ -n "${has_source[i]:-}" ] || report "raw source: $path: header has no Source line"
        [ -n "${has_saved[i]:-}" ] || report "raw source: $path: header has no Saved line with a YYYY-MM-DD date"
        [ -n "${has_build[i]:-}" ] || report "raw source: $path: header has no Game build line" ;;
      docs/findings/*.md)
        [ -n "${has_sha[i]:-}" ] ||
          report "finding: $path: header has no Game build line naming the executable's SHA-256" ;;
      docs/raw-sources/*) report "raw source: $path: not a .md file" ;;
      docs/findings/*) report "finding: $path: not a .md file" ;;
    esac

    emit "${found_user[i]:-}"
    emit "${found_email[i]:-}"
    [ -z "${found_name[i]:-}" ] || report "personal information: $path: file name matches a private pattern"
    emit "${found_private[i]:-}"
  done
  case "$stage" in
    rules | full)
      if [ "$scan_scope" = all ]; then
        echo "scan: every tracked file (${#paths[@]}) in $((SECONDS - scan_started)) s"
      else
        echo "scan: ${#paths[@]} files $scan_scope in $((SECONDS - scan_started)) s" \
          "(CI scans every tracked file; METAPHOR_SCAN_ALL=1 does so here)"
      fi ;;
  esac
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
