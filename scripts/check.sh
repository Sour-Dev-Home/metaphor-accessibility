#!/usr/bin/env bash
# The one check command. Local hooks and CI both run it, so their checks never drift apart.
#
#   scripts/check.sh fast    repo rules on staged files (the pre-commit hook)
#   scripts/check.sh rules   repo rules on every tracked file (CI on Ubuntu)
#   scripts/check.sh full    repo rules, build, Unit and RoundTrip tests, formatting, smoke and deploy script tests (CI on Windows)
#   scripts/check.sh docs    broken links fail; Markdown lint, Vale and ADR length warn (CI on Ubuntu)
#   scripts/check.sh slow    mutation testing and a duplication report, report only (weekly CI on Windows)
#   scripts/check.sh signatures [EXE]    each hook signature matches exactly once in the game exe (local, before a hook PR)
#   scripts/check.sh pr-body FILE        warns when a PR description is too long (CI on pull requests)
#   scripts/check.sh commit-msg FILE     one commit message (the commit-msg hook)
#   scripts/check.sh commits BASE..HEAD  every non-merge commit message in a range (CI on pull requests)
#
# A commit message has a typed subject of at most 72 characters ("feat: ...", "fix(check): ..."), then optionally a blank
# line and a body of at most 5 lines. A final paragraph of trailers (Co-Authored-By: ...) doesn't count. Subjects git
# writes itself (Revert "...", fixup!) pass.
#
# Rules judge the staged blobs in the index, not the working tree, so they see exactly what would be committed.
# Every step runs even after a failure, so one run shows every problem.
# Exit 0 when everything passes (warnings don't fail), 1 when something fails, 2 on a usage error or when a tool is
# missing or fails to run. The docs stage skips a missing docs tool with a warning unless REQUIRE_DOCS_TOOLS=1 (CI).
set -uo pipefail

die() {
  echo "check: $*" >&2
  exit 2
}

failures=0
warnings=0

report() {
  echo "$1"
  failures=$((failures + 1))
}

# warn MESSAGE [FILE [LINE]]: print a warning that doesn't fail the run. In GitHub Actions it also becomes an annotation
# on the file, so it shows on the pull request.
warn() {
  local message="${1//\\//}" file="${2:-}" # Windows tools print backslashes
  file="${file//\\//}"
  echo "warning: $message"
  warnings=$((warnings + 1))
  if [ "${GITHUB_ACTIONS:-}" = true ]; then
    # Workflow commands need % escaped everywhere, and , and : in properties too.
    message="${message//%/%25}"
    file="${file//%/%25}" && file="${file//,/%2C}" && file="${file//:/%3A}"
    echo "::warning${file:+ file=$file}${3:+,line=$3}::$message"
  fi
  return 0
}

# find_tool NAME: print the path of a docs tool, looking first where scripts/install-docs-tools.sh puts it.
find_tool() {
  local candidate
  for candidate in "$root/.tools/bin/$1" "$root/.tools/bin/$1.exe" "$root/tools/docs/node_modules/.bin/$1"; do
    [ -x "$candidate" ] && { echo "$candidate"; return 0; }
  done
  command -v "$1"
}

# docs_stage: the docs checks, on the working tree's tracked Markdown files. A broken relative link or anchor fails the
# run; lint, style and length findings are warnings. Missing tools are skipped with a warning, unless
# REQUIRE_DOCS_TOOLS=1 (CI), where a missing tool must not pass quietly.
docs_stage() {
  root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
  cd "$root" || die "could not enter $root"
  local files=() file tool
  # Left out: raw sources, which are saved copies of other people's pages linking to the original site; the code of
  # conduct, which is upstream text kept verbatim; and CLAUDE.md, which is only an import line.
  while IFS= read -r -d '' file; do files+=("$file"); done < <(
    git ls-files -z -- '*.md' ':!:docs/raw-sources/**' ':!:CODE_OF_CONDUCT.md' ':!:CLAUDE.md'
  )
  [ "${#files[@]}" -gt 0 ] || return 0

  if tool="$(find_tool lychee)"; then
    check_links "$tool" "${files[@]}"
  else
    missing_tool lychee
  fi
  if tool="$(find_tool vale)"; then
    check_style "$tool" "${files[@]}"
  else
    missing_tool vale
  fi
  if tool="$(find_tool markdownlint-cli2)"; then
    check_lint "$tool" "${files[@]}"
  else
    missing_tool markdownlint-cli2
  fi

  # An ADR is one paragraph plus optional sections.
  local count
  for file in "${files[@]}"; do
    case "$file" in
      docs/adr/*.md)
        count="$(prose_words < "$file")"
        [ "$count" -le "$max_adr_words" ] ||
          warn "$file: $count words; ADRs aim for at most $max_adr_words" "$file" ;;
    esac
  done
}

# The slow suite (weekly in CI): Stryker.NET mutation testing of Metaphor.Core, and a jscpd duplication report, into
# artifacts/slow/ (git-ignored). summary.md there has the mutation score per file, each surviving mutant, and the
# duplication totals. Scores never fail it: a tool that can't run exits 2, after the summary says what's missing.
slow_stage() {
  local root out broken=""
  root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
  out="$root/artifacts/slow"
  command -v dotnet > /dev/null || die "slow stage needs the .NET SDK (dotnet) on PATH"
  command -v npm > /dev/null || die "slow stage needs npm on PATH"
  rm -rf "$out" && mkdir -p "$out" || die "could not empty $out"

  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  (cd "$root" && dotnet tool restore -v q) || die "could not restore dotnet-stryker (.config/dotnet-tools.json)"
  (cd "$root/tests/Metaphor.Core.Tests" && dotnet stryker --output "$out/stryker") || broken="$broken Stryker.NET"

  npm ci --prefix "$root/tools/slow" --ignore-scripts --no-audit --no-fund --loglevel=error ||
    die "could not install jscpd (tools/slow)"
  (cd "$root" && tools/slow/node_modules/.bin/jscpd --config tools/slow/jscpd.json --output "$out/jscpd") ||
    broken="$broken jscpd"

  local reports="$out/stryker/reports"
  {
    [ -f "$reports/mutation-report.md" ] && cat "$reports/mutation-report.md" || echo "No mutation report."
    echo
    node "$root/tools/slow/survivors.mjs" "$reports/mutation-report.json" "$root" || echo "No list of surviving mutants."
    echo
    [ -f "$out/jscpd/jscpd-report.md" ] && cat "$out/jscpd/jscpd-report.md" || echo "No duplication report."
  } > "$out/summary.md"
  echo "slow suite reports: $out (summary.md)"
  [ -z "$broken" ] || die "failed to run:$broken (see above)"
}

# The offline signature check (stage 4 in issue #1, local only): each signature in src/Metaphor.Core/hook-signatures.json must
# match exactly once in the game exe on disk. The exe is the argument, or METAPHOR_GAME_EXE. Nothing is launched.
signatures_stage() {
  local exe="${1:-${METAPHOR_GAME_EXE:-}}" root
  [ -n "$exe" ] || die "signatures stage needs the game exe: pass it, or set METAPHOR_GAME_EXE"
  [ -f "$exe" ] || die "no file at $exe"
  root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
  command -v dotnet > /dev/null || die "signatures stage needs the .NET SDK (dotnet) on PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  # Built first, so a build failure can't be mistaken for the tool's exit 1.
  dotnet build "$root/tools/Metaphor.SignatureCheck" -c Release -v q --nologo > /dev/null ||
    die "could not build tools/Metaphor.SignatureCheck (run: dotnet build tools/Metaphor.SignatureCheck)"
  dotnet run --project "$root/tools/Metaphor.SignatureCheck" -c Release --no-build -- "$exe"
  case $? in
    0) ;;
    1) report "signatures: a signature doesn't match exactly once (see above)" ;;
    *) die "the signature check could not run (see above)" ;;
  esac
}

max_adr_words=250
max_pr_words=300

# prose_words: count the words on stdin, leaving out headings and fenced code blocks, which hold diagrams and sketches
# rather than prose.
prose_words() {
  tr -d '\r' | awk '/^[[:space:]]*(```|~~~)/ { code = !code; next } !code && !/^#+ /' | wc -w |
    tr -d '[:space:]'
}

# check_lint MARKDOWNLINT FILE...: warn on each markdownlint finding (settings in .markdownlint-cli2.jsonc).
check_lint() {
  local markdownlint="$1" output status line
  shift
  output="$("$markdownlint" -- "$@" 2>&1)"
  status=$?
  [ "$status" -le 1 ] || die "markdownlint failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    # path:line[:column] severity rule/name description
    [[ "$line" =~ ^([^:]+):([0-9]+)(:[0-9]+)?\ [a-z]+\ (MD[0-9]+.*)$ ]] || continue
    warn "${BASH_REMATCH[1]}:${BASH_REMATCH[2]}: ${BASH_REMATCH[4]}" "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}"
  done <<<"$output"
}

# check_style VALE FILE...: warn on each Vale finding, such as a word from .vale/styles/Project/Filler.yml.
check_style() {
  local vale="$1" output status line
  shift
  [ -f "$root/.vale.ini" ] || die "Vale needs .vale.ini at the repository root"
  output="$("$vale" --config="$root/.vale.ini" --output=line --no-exit -- "$@" 2>&1)"
  status=$?
  [ "$status" -eq 0 ] || die "Vale failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    # path:line:column:rule:message
    [[ "$line" =~ ^([^:]+):([0-9]+):[0-9]+:[^:]+:(.*)$ ]] || continue
    warn "${BASH_REMATCH[1]}:${BASH_REMATCH[2]}: ${BASH_REMATCH[3]}" "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}"
  done <<<"$output"
}

# missing_tool NAME: skip a docs tool that isn't installed, or fail when CI requires it.
missing_tool() {
  [ "${REQUIRE_DOCS_TOOLS:-}" = 1 ] && die "$1 is not installed, but REQUIRE_DOCS_TOOLS=1 (run scripts/install-docs-tools.sh)"
  warn "$1 is not installed, so its checks were skipped (run scripts/install-docs-tools.sh)"
}

# check_links LYCHEE FILE...: report each broken relative link or anchor. Offline, so outside sites can't fail the run.
check_links() {
  local lychee="$1" output status line current="" target where found=0
  shift
  output="$("$lychee" --offline --include-fragments --no-progress -- "$@" 2>&1)"
  status=$?
  [ "$status" -eq 0 ] && return 0
  [ "$status" -eq 2 ] || die "lychee failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    if [[ "$line" =~ ^\[(.+)\]:$ ]]; then
      current="${BASH_REMATCH[1]}"
    elif [[ "$line" =~ ^\[ERROR\]\ (.+)\ \(at\ ([0-9]+):[0-9]+\)\ \|\ (.*)$ ]]; then
      target="${BASH_REMATCH[1]#file://}"
      target="${target#/}"
      target="${target#"${root#/}"/}"
      where="${BASH_REMATCH[2]}"
      report "broken link: $current:$where: $target (${BASH_REMATCH[3]})"
      found=1
    fi
  done <<<"$output"
  # lychee found something this parser can't read (its output format changed): fail rather than pass quietly.
  [ "$found" -eq 1 ] || die "lychee reported broken links in a format this script can't read: $output"
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
  pr-body)
    [ -f "${2:-}" ] || die "pr-body stage needs the file holding the PR description"
    # The Evidence section holds output and screenshots, so it doesn't count (the /pr template's sections).
    count="$(awk '/^## / { evidence = ($0 ~ /^## +Evidence[[:space:]]*$/) } !evidence' "$2" | prose_words)"
    [ "$count" -le "$max_pr_words" ] ||
      warn "PR description: $count words outside Evidence; descriptions aim for at most $max_pr_words"
    root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
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
  *) echo "usage: scripts/check.sh fast|rules|full|docs|slow | signatures [EXE] | commit-msg FILE | commits BASE..HEAD | pr-body FILE" >&2
     exit 2 ;;
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
# bold. One opening quote before the value (`, ", ' or <) is skipped. HINT describes the value in the message.
check_header() {
  local path="$1" kind="$2" label="$3" value_re="$4" hint="${5:-}"
  # Process substitution, not a pipe: grep -q stops at the first match, and under pipefail the write error that gives
  # sed (on a long file with no "## " heading) would read as a missing line, at random.
  grep -qiE "^- \*\*([^*]* )?${label}s?([^*a-z][^*]*)?(:\*\*|\*\*:)[[:space:]]*[\`\"'<]?${value_re}" \
    < <(sed '/^## /,$d' "$tmp/blob") ||
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

  # Notes cite their sources and pin their game build (AGENTS.md ground rules 1 and 2).
  case "$path" in
    docs/raw-sources/README.md | docs/findings/README.md) ;;
    docs/raw-sources/*.md)
      check_header "$path" "raw source" Source '[[:alnum:]]'
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
  dotnet test "$solution" -c Release --no-build -v q --filter "Category=Unit|Category=RoundTrip" ||
    report "tests: Unit or RoundTrip tests failed"
  dotnet format whitespace "$solution" --verify-no-changes ||
    report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
  # The smoke-test and deploy scripts' own tests, against the stand-in game (no game needed). Pester 5 or later.
  pwsh -NoProfile -Command \
    'Import-Module Pester -MinimumVersion 5.0; $r = Invoke-Pester tests/smoke -PassThru -Output Normal; exit $r.FailedCount' ||
    report "tests: smoke or deploy script tests failed (run: Invoke-Pester tests/smoke)"
fi

[ "$failures" -eq 0 ]
