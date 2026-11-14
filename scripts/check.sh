#!/usr/bin/env bash
# The one check command. Local hooks and CI both run it, so their checks never drift apart.
#
#   scripts/check.sh fast    repo rules on staged files (the pre-commit hook)
#   scripts/check.sh rules   repo rules on every tracked file (CI on Ubuntu)
#   scripts/check.sh full    repo rules, then build, Unit tests and formatting (CI on Windows)
#   scripts/check.sh commit-msg FILE     one commit message (the commit-msg hook)
#   scripts/check.sh commits BASE..HEAD  every non-merge commit message in a range (CI on pull requests)
#
# A commit message has a typed subject of at most 72 characters ("feat: ...", "fix(check): ..."), then optionally a blank
# line and a body of at most 5 lines. A final paragraph of trailers (Co-Authored-By: ...) doesn't count. Subjects git
# writes itself (Revert "...", fixup!) pass.
#
# Rules judge the staged blobs in the index, not the working tree, so they see exactly what would be committed.
# Every step runs even after a failure, so one run shows every problem.
# Exit 0 when everything passes, 1 when something fails, 2 on a usage error or when a tool is missing or fails to run.
set -uo pipefail

die() {
  echo "check: $*" >&2
  exit 2
}

failures=0

report() {
  echo "$1"
  failures=$((failures + 1))
}

# check_message WHO: check the commit message on stdin against the agreed shape, reporting as WHO. Comment lines and
# everything below git's scissors line are dropped first, because the commit-msg hook sees the message before git does.
max_subject_chars=72
max_body_lines=5

check_message() {
  local label="$1" message subject body last length lines
  # Like git: drop CR, trailing spaces, comment lines and everything below the scissors line. $(...) also drops
  # trailing blank lines.
  message="$(sed -e 's/\r$//' -e 's/[[:space:]]*$//' -e '/^# -* >8 -*$/,$d' -e '/^#/d')"
  message="${message#"${message%%[!$'\n']*}"}" # leading blank lines
  if [ -z "$message" ]; then
    report "$label: message is empty"
    return
  fi
  subject="${message%%$'\n'*}"
  body=""
  [ "$message" = "$subject" ] || body="${message#*$'\n'}"

  # Git writes these subjects itself (git revert, git commit --fixup), so they pass whole; PRs are squash-merged, so
  # they never reach main.
  if ! [[ "$subject" =~ ^(Revert\ \"|fixup!\ |squash!\ |amend!\ ) ]]; then
    [[ "$subject" =~ ^(feat|fix|docs|test|refactor|perf|build|ci|chore|style|revert)(\([^\)]+\))?!?:\ [^[:space:]] ]] ||
      report "$label: subject needs a type prefix, such as \"feat: \" or \"fix(check): \""
    length="$(printf '%s' "$subject" | LC_ALL=C.UTF-8 wc -m)"
    length="${length//[[:space:]]/}"
    [ "$length" -le "$max_subject_chars" ] ||
      report "$label: subject is $length characters; the limit is $max_subject_chars"
  fi

  [ -z "$body" ] && return
  [ -z "${body%%$'\n'*}" ] || report "$label: leave a blank line after the subject"
  # The last paragraph holds trailers (Co-Authored-By:, Signed-off-by:) when every line is "Key: value", as git reads
  # them. They don't count, even when they are the whole body.
  body="${body#$'\n'}"
  last="${body##*$'\n\n'}"
  if ! grep -qvE '^[A-Za-z][A-Za-z0-9-]*: ' <<<"$last"; then
    [ "$last" = "$body" ] && body="" || body="${body%$'\n\n'*}"
  fi
  lines="$(grep -c '[^[:space:]]' <<<"$body")"
  [ "$lines" -le "$max_body_lines" ] || report "$label: body has $lines lines; the limit is $max_body_lines"
}

tmp="$(mktemp -d)" || die "could not create a temporary folder"
trap 'rm -rf "$tmp"' EXIT

stage="${1:-}"
# list_entries prints "<mode> <blob> <stage>\t<path>\0" for each file to check, in `git ls-files -s -z` form.
case "$stage" in
  fast)
    list_entries() {
      git diff --cached --name-only --diff-filter=ACMR -z |
        xargs -0 -r git --literal-pathspecs ls-files -s -z --
    } ;;
  rules | full) list_entries() { git ls-files -s -z; } ;;
  commit-msg)
    [ -f "${2:-}" ] || die "commit-msg stage needs the message file"
    check_message "commit message" < "$2"
    [ "$failures" -eq 0 ]
    exit ;;
  commits)
    # Merge commits are skipped: git writes their messages, and PRs are squash-merged.
    shas="$(git rev-list --no-merges "${2:-}" --)" || die "could not list the commits in ${2:-}"
    for sha in $shas; do
      git log -1 --format=%B "$sha" > "$tmp/message" || die "could not read commit $sha"
      check_message "commit $(git rev-parse --short "$sha")" < "$tmp/message"
    done
    [ "$failures" -eq 0 ]
    exit ;;
  *) echo "usage: scripts/check.sh fast|rules|full | commit-msg FILE | commits BASE..HEAD" >&2; exit 2 ;;
esac

list_entries > "$tmp/entries" || die "could not list files (is this a git repository?)"
root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
solution="$root/Metaphor.sln"
if [ "$stage" = full ]; then
  [ -f "$solution" ] || die "full stage needs $(basename "$solution") at the repository root"
  command -v dotnet > /dev/null || die "full stage needs the .NET SDK (dotnet) on PATH"
fi

# Policy, not a measurement: our code and notes have no reason to exceed this, so anything bigger needs a look.
max_bytes=$((1024 * 1024))

# Known game-file hashes, read from the index: one SHA-256 per line, then a label naming its game build.
known_hashes=""
if git cat-file -e :scripts/game-content.sha256 2>/dev/null; then
  known_hashes="$(git show :scripts/game-content.sha256 | tr -d '\r' | sed 's/#.*//' | awk 'NF { print tolower($1) }')" ||
    die "could not read scripts/game-content.sha256"
fi

# Private personal-information patterns, matched as fixed strings so special characters can't break the scan.
# The git-ignored .claude/pii-patterns.txt holds them locally; CI passes them in PII_PATTERNS. One per line, '#' comments.
if [ -n "${PII_PATTERNS:-}" ]; then
  printf '%s\n' "$PII_PATTERNS"
elif [ -f "$root/.claude/pii-patterns.txt" ]; then
  cat "$root/.claude/pii-patterns.txt"
fi | tr -d '\r' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | grep -v -e '^#' -e '^$' > "$tmp/private"
if [ ! -s "$tmp/private" ]; then
  # CI sets REQUIRE_PRIVATE_PATTERNS on pushes to main, where a missing secret must not pass quietly.
  [ "${REQUIRE_PRIVATE_PATTERNS:-}" = 1 ] && die "no private patterns, but REQUIRE_PRIVATE_PATTERNS=1 (is the secret set?)"
  echo "warning: no private patterns, so personal information is checked against general patterns only." \
    "Copy .claude/pii-patterns.example.txt to .claude/pii-patterns.txt and fill it in." >&2
fi

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

# scan_lines PATH REASON SKIP-RE GREP-ARGS...: report each line of the current blob where grep finds a match, ignoring
# matches that SKIP-RE (lower case; empty skips nothing) accepts. Only the line number is printed, never the matched
# text, because CI logs are public.
scan_lines() {
  local path="$1" reason="$2" skip="$3" matches status line text last=""
  shift 3
  matches="$(grep -nao "$@" "$tmp/blob")"
  status=$?
  [ "$status" -le 1 ] || die "could not scan $path"
  [ "$status" -eq 0 ] || return 0
  while IFS= read -r line; do
    text="${line#*:}"
    [ -n "$skip" ] && [[ "${text,,}" =~ $skip ]] && continue
    [ "${line%%:*}" = "$last" ] && continue
    last="${line%%:*}"
    report "personal information: $path:$last: $reason"
  done <<<"$matches"
}

# check_header PATH KIND LABEL VALUE-RE [HINT]: report when the current blob's header (the lines before its first "## "
# heading) has no "- **LABEL:** value" line whose value matches VALUE-RE. The label matches as a word in any case, plural
# or inside longer wording ("Date saved:", "Game build(s) the sources mention:"), with the colon inside or after the
# bold. HINT describes the value in the message.
check_header() {
  local path="$1" kind="$2" label="$3" value_re="$4" hint="${5:-}"
  sed '/^## /,$d' "$tmp/blob" |
    grep -qiE "^- \*\*([^*]* )?${label}s?([^*a-z][^*]*)?(:\*\*|\*\*:)[[:space:]]*${value_re}" ||
    report "$kind: $path: header has no $label line${hint:+ $hint}"
}

while IFS= read -r -d '' entry; do
  meta="${entry%%$'\t'*}"
  path="${entry#*$'\t'}"
  read -r mode blob _ <<<"$meta"
  [ "$mode" = 160000 ] && continue # a submodule: its content lives in another repository

  # Game folders are the git-ignored ones at the repo root. Lower-cased because Windows paths are case-insensitive.
  lower="${path,,}"
  case "$lower" in
    game/* | extracted/* | dumps/* | captures/*) report_game_content "$path" "inside a game folder" ;;
  esac
  case "/$lower" in
    *.rep/*) report_game_content "$path" "inside a disassembler project folder" ;;
    *.cpk | *.pak) report_game_content "$path" "game archive extension" ;;
    # Our own builds are blocked too: they live as CI artifacts tied to a commit, never in the repository.
    *.exe | *.dll | *.asi) report "game content or build output: $path: executable extension" ;;
    *.dmp | *.mdmp) report_game_content "$path" "memory dump extension" ;;
    *.gpr | *.idb | *.i64 | *.til | *.nam | *.id0 | *.id1 | *.id2 | *.bndb | *.dd64)
      report_game_content "$path" "disassembler database extension" ;;
  esac

  # One git call per file: process start-up dominates the run time in Git Bash.
  git cat-file blob "$blob" > "$tmp/blob" || die "could not read $path"
  size="$(wc -c < "$tmp/blob")"
  size="${size//[[:space:]]/}"
  [ "$size" -le "$max_bytes" ] || report_game_content "$path" "larger than $max_bytes bytes ($size)"

  if [ -n "$known_hashes" ]; then
    hash="$(sha256sum < "$tmp/blob" | cut -d' ' -f1)" || die "could not hash $path"
    grep -Fxq "$hash" <<<"$known_hashes" && report_game_content "$path" "hash matches a known game file"
  fi

  # Notes cite their sources and pin their game build (CLAUDE.md ground rules 1 and 2).
  case "$path" in
    docs/raw-sources/README.md | docs/findings/README.md) ;;
    docs/raw-sources/*.md)
      check_header "$path" "raw source" Source '[[:alnum:]<]'
      check_header "$path" "raw source" Saved '[0-9]{4}-[0-9]{2}-[0-9]{2}([^0-9a-z]|$)' "with a YYYY-MM-DD date"
      check_header "$path" "raw source" "Game build" '[[:alnum:]]' ;;
    # A finding names its game build by the executable's SHA-256 (GLOSSARY.md).
    docs/findings/*.md)
      check_header "$path" finding "Game build" '[0-9a-f]{64}([^0-9a-f]|$)' "naming the executable's SHA-256" ;;
    docs/raw-sources/*) report "raw source: $path: not a .md file" ;;
    docs/findings/*) report "finding: $path: not a .md file" ;;
  esac

  scan_lines "$path" "local user path" "" -E "$user_path_re"
  scan_lines "$path" "email address" "$not_email_re" -iE "$email_re"
  if [ -s "$tmp/private" ]; then
    printf '%s\n' "$path" | grep -qiF -f "$tmp/private"
    case $? in
      0) report "personal information: $path: file name matches a private pattern" ;;
      1) ;;
      *) die "could not scan the name of $path" ;;
    esac
    scan_lines "$path" "matches a private pattern" "" -iF -f "$tmp/private"
  fi
done < "$tmp/entries"

# The .NET part of the full stage. Tests and formatting still run after a failed build, so one run shows everything.
if [ "$stage" = full ]; then
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  dotnet build "$solution" -c Release -v q || report "build: failed, see the errors above"
  dotnet test "$solution" -c Release --no-build -v q --filter Category=Unit || report "tests: Unit tests failed"
  dotnet format whitespace "$solution" --verify-no-changes ||
    report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
fi

[ "$failures" -eq 0 ]
