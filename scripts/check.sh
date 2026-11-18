#!/usr/bin/env bash
# The one check command. Local hooks and CI both run it, so their checks never drift apart.
#
#   scripts/check.sh fast    repo rules on staged files
#   scripts/check.sh pre-commit   fast, then formatting, docs and signatures when their files are staged (the hook)
#   scripts/check.sh pre-push     build, Unit and RoundTrip tests when pushed commits change code (the hook; refs on stdin)
#                                 (a passing full or pre-push is recorded per clone by tree hash; the same tree reuses it)
#   scripts/check.sh rules   repo rules on every tracked file (CI on Ubuntu)
#   scripts/check.sh full    repo rules, build, Unit and RoundTrip tests, formatting, smoke and deploy script tests (CI: Windows in the merge queue, Linux on PRs)
#   scripts/check.sh docs    broken links fail; Markdown lint, Vale and ADR length warn (CI on Ubuntu)
#   scripts/check.sh slow    mutation testing and a duplication report, report only (weekly CI on Windows)
#   scripts/check.sh signatures [EXE]    each hook signature matches exactly once in the game exe (local, before a hook PR)
#   scripts/check.sh pr-body FILE        warns when a PR description is too long (CI on pull requests)
#   scripts/check.sh commit-msg FILE     one commit message, plus its security-review trailer (the commit-msg hook)
#   scripts/check.sh commits BASE..HEAD  every non-merge commit message and security-review trailer in a range (CI on PRs)
#
# A commit message has a typed subject of at most 72 characters ("feat: ...", "fix(check): ..."), then optionally a blank
# line and a body of at most 5 lines. A final paragraph of trailers (Co-Authored-By: ...) doesn't count. Subjects git
# writes itself (Revert "...", fixup!) pass.
#
# A commit touching the security-review scope (scripts/security-stamp.sh) needs the security-reviewer agent's stamp
# matching the staged scope; the commit-msg hook then appends "Security-Review: <hash>". SECURITY_REVIEW_SKIP="<reason>"
# lets it through and records "Security-Review-Skipped: <reason>" instead.
#
# In CI the `commits` stage also reads PR_AUTHOR_ASSOCIATION. A PR whose author is OWNER, MEMBER or COLLABORATOR is the
# maintainer's and is judged by trailers. Any other PR is outside: trailers don't count, and a PR touching the scope needs
# the `security-reviewed` label, added after the PR's latest push (PR_NUMBER, GITHUB_REPOSITORY, GH_TOKEN, and
# PR_UPDATED_AT from the event). Without PR_AUTHOR_ASSOCIATION (local runs) trailers decide.
#
# Rules judge the staged blobs in the index, not the working tree, so they see exactly what would be committed.
# Every step runs even after a failure, so one run shows every problem.
# Exit 0 when everything passes (warnings don't fail), 1 when something fails, 2 on a usage error or when a tool is
# missing or fails to run. The docs stage skips a missing docs tool with a warning unless REQUIRE_DOCS_TOOLS=1 (CI).
set -uo pipefail

# The test categories to run. WindowsOnly tests pin down Windows behaviour (the mod only runs there), so they run on
# Windows alone; on Linux (the pull-request build in CI) they are left out.
test_filter() {
  case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*) echo "Category=Unit|Category=RoundTrip|Category=WindowsOnly" ;;
    *) echo "Category=Unit|Category=RoundTrip" ;;
  esac
}

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
    git ls-files -z -- '*.md' ':!:docs/raw-sources/**' ':!:.github/CODE_OF_CONDUCT.md' ':!:CLAUDE.md'
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

# Files whose change can change the build or the tests' result.
code_path_re='(\.(cs|csproj|props|targets|sln|json|editorconfig)|(^|/)global\.json)$'

# The pass cache (issue #241): "<stage> <tree>" lines in this clone's git dir, never a shared path. A pass is reused only
# for the identical tree, so any change, staged or committed, runs the checks again. A `full` pass also covers pre-push.
pass_cache() { echo "$(git rev-parse --absolute-git-dir 2> /dev/null)/check-pass-cache"; }
cached_pass() { # cached_pass TREE STAGE...: exit 0 when one of the stages passed on TREE
  local tree="$1" kind file
  shift
  file="$(pass_cache)"
  [ -f "$file" ] || return 1
  for kind in "$@"; do grep -qxF "$kind $tree" "$file" && return 0; done
  return 1
}
record_pass() { # record_pass STAGE TREE: keep the last 50 passes
  local file
  file="$(pass_cache)"
  { echo "$1 $2"; cat "$file" 2> /dev/null || true; } | head -n 50 > "$file.new" && mv "$file.new" "$file"
}
# full_tree: print the index's tree, only when the working tree matches it (the build reads the working tree).
full_tree() {
  git diff --quiet 2> /dev/null && git write-tree 2> /dev/null
}

# The pre-push hook's stage. Git passes "<local ref> <local sha> <remote ref> <remote sha>" per pushed ref on stdin.
# Builds and runs the Unit and RoundTrip tests unless every pushed commit leaves code alone. When it can't tell what
# changed (a new branch with no origin/main to compare, or a remote commit not fetched), it builds.
pre_push_stage() {
  local zero=0000000000000000000000000000000000000000 local_ref local_sha remote_ref remote_sha base
  local changed="" unknown=0 pushed=0 root solution trees=() tree cached=1
  while read -r local_ref local_sha remote_ref remote_sha; do
    [ -n "$local_sha" ] && [ "$local_sha" != "$zero" ] || continue # a deleted branch pushes nothing
    pushed=1
    trees+=("$(git rev-parse "$local_sha^{tree}" 2> /dev/null || echo none)")
    base="$remote_sha"
    if [ "$base" = "$zero" ]; then
      base="$(git merge-base "$local_sha" origin/main 2> /dev/null)" || base=""
    fi
    if [ -z "$base" ] || ! git cat-file -e "$base^{commit}" 2> /dev/null; then
      unknown=1
      break
    fi
    changed+="$(git diff --name-only "$base" "$local_sha")"$'\n' || die "could not list the files changed in $local_ref"
  done
  [ "$pushed" -eq 1 ] || return 0
  if [ "$unknown" -eq 0 ] && ! grep -qE "$code_path_re" <<< "$changed"; then
    echo "pre-push: no code changes, so the build and tests were skipped"
    return 0
  fi
  for tree in "${trees[@]}"; do
    cached_pass "$tree" full pre-push || cached=0
  done
  if [ "$cached" -eq 1 ]; then
    reused=reused
    echo "pre-push: reused the recorded pass for tree ${trees[*]}; no build or tests ran"
    return 0
  fi
  take_lock
  root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
  solution="$root/Metaphor.sln"
  [ -f "$solution" ] || die "pre-push stage needs $(basename "$solution") at the repository root"
  command -v dotnet > /dev/null || die "pre-push stage needs the .NET SDK (dotnet) on PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  # Tests run only after a good build: --no-build would otherwise test the last good build and pass.
  if dotnet build "$solution" -c Release -v q --nologo; then
    dotnet test "$solution" -c Release --no-build -v q --filter "$(test_filter)" ||
      report "tests: Unit or RoundTrip tests failed"
  else
    report "build: failed, see the errors above"
  fi
  if [ "$failures" -eq 0 ]; then
    for tree in "${trees[@]}"; do record_pass pre-push "$tree"; done
  fi
}

max_adr_words=250
max_pr_words=300

# prose_words: count the words on stdin, leaving out headings and fenced code blocks, which hold diagrams and sketches
# rather than prose.
prose_words() {
  tr -d '\r' | awk '/^[[:space:]]*(```|~~~)/ { code = !code; next } !code && !/^#+ /' | wc -w |
    tr -d '[:space:]'
}

# check_lint MARKDOWNLINT FILE...: warn on each markdownlint finding (settings in tools/docs/.markdownlint-cli2.jsonc).
check_lint() {
  local markdownlint="$1" config="$root/tools/docs/.markdownlint-cli2.jsonc" output status line
  shift
  [ -f "$config" ] || die "markdownlint needs tools/docs/.markdownlint-cli2.jsonc"
  output="$("$markdownlint" --config "$config" -- "$@" 2>&1)"
  status=$?
  [ "$status" -le 1 ] || die "markdownlint failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    # path:line[:column] severity rule/name description
    [[ "$line" =~ ^([^:]+):([0-9]+)(:[0-9]+)?\ [a-z]+\ (MD[0-9]+.*)$ ]] || continue
    warn "${BASH_REMATCH[1]}:${BASH_REMATCH[2]}: ${BASH_REMATCH[4]}" "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}"
  done <<<"$output"
}

# check_style VALE FILE...: warn on each Vale finding, such as a word from tools/docs/.vale/styles/Project/Filler.yml.
check_style() {
  local vale="$1" config="$root/tools/docs/.vale.ini" output status line
  shift
  [ -f "$config" ] || die "Vale needs tools/docs/.vale.ini"
  output="$("$vale" --config="$config" --output=line --no-exit -- "$@" 2>&1)"
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

# The security review's stamp script answers for the repository in the current folder.
stamp_script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/security-stamp.sh"

# in_scope [COMMIT]: exit 0 when the staged changes (or COMMIT) touch the security-review scope, 1 when they don't.
in_scope() {
  bash "$stamp_script" touches "$@"
  case $? in
    0) return 0 ;;
    1) return 1 ;;
    *) die "could not tell whether ${1:-the commit} touches the security-review scope" ;;
  esac
}

# skipping: exit 0 when SECURITY_REVIEW_SKIP gives a reason (not just spaces).
skipping() {
  local reason="${SECURITY_REVIEW_SKIP:-}"
  [ -n "${reason//[[:space:]]/}" ]
}

# review_gate: a staged change to the scope needs a stamp matching it, unless SECURITY_REVIEW_SKIP gives a reason.
# Exit 0 when the commit may go ahead.
review_gate() {
  skipping && return 0
  bash "$stamp_script" stamped
  case $? in
    0) return 0 ;;
    1) report "security review: the staged hook or deploy code has no matching stamp. Run the security-reviewer agent \
(it stamps when it finds nothing critical or important), or set SECURITY_REVIEW_SKIP=\"<reason>\" (maintainer only)"
       return 1 ;;
    *) die "could not check the security-review stamp" ;;
  esac
}

# review_trailer FILE: on a commit touching the scope, replace any Security-Review trailers in the message FILE with the
# one this commit earns.
review_trailer() {
  local file="$1" trailer
  in_scope || return 0
  review_gate || return 0
  if skipping; then
    trailer="Security-Review-Skipped: ${SECURITY_REVIEW_SKIP//[$'\r\n']/ }"
  else
    trailer="Security-Review: $(bash "$stamp_script" hash)" || die "could not hash the security-review scope"
  fi
  grep -viE '^Security-Review(-Skipped)?:' "$file" > "$tmp/message"
  [ $? -le 1 ] || die "could not read $file"
  git interpret-trailers --trailer "$trailer" < "$tmp/message" > "$file" || die "could not add the trailer to $file"
}

# check_review_trailer SHA: a commit touching the scope carries "Security-Review: <its scope hash>" or a
# Security-Review-Skipped trailer with a reason.
check_review_trailer() {
  local sha="$1" want reviewed skipped
  in_scope "$sha" || return 0
  want="$(bash "$stamp_script" hash "$sha")" || die "could not hash the security-review scope of $sha"
  reviewed="$(git log -1 --format='%(trailers:key=Security-Review,valueonly)' "$sha")" || die "could not read $sha"
  skipped="$(git log -1 --format='%(trailers:key=Security-Review-Skipped,valueonly)' "$sha")" || die "could not read $sha"
  grep -qxF "$want" <<<"$reviewed" && return 0
  [ -n "${skipped//[[:space:]]/}" ] && return 0
  report "commit $(git rev-parse --short "$sha"): touches the security-review scope without a Security-Review trailer \
matching its content, or a Security-Review-Skipped one"
}

# require_review_label: an outside PR touching the scope needs the security-reviewed label, added after its latest push.
# A fork's token is read-only, so CI can't remove a stale label; it reads the timeline instead. Push time is the latest
# of the timeline's commits and force pushes and the event's updated_at (set by GitHub, so a backdated commit can't
# make a label look fresh). ISO 8601 UTC timestamps compare as text.
require_review_label() {
  local repo="${GITHUB_REPOSITORY:-}" pr="${PR_NUMBER:-}" events kind date pushed="${PR_UPDATED_AT:-}" label_state="" labeled_at=""
  [ -n "$repo" ] && [ -n "$pr" ] && [ -n "$pushed" ] ||
    die "an outside PR needs GITHUB_REPOSITORY, PR_NUMBER and PR_UPDATED_AT to read its timeline"
  events="$(gh api --paginate "repos/$repo/issues/$pr/timeline" --jq \
    '.[] | (select((.event=="labeled" or .event=="unlabeled") and .label.name=="security-reviewed") | "\(.event)\t\(.created_at)"),
     (select(.event=="head_ref_force_pushed") | "push\t\(.created_at)"),
     (select(.event=="committed") | "push\t\(.committer.date)")')" || die "could not read the timeline of PR #$pr"
  while IFS=$'\t' read -r kind date; do
    case "$kind" in
      labeled | unlabeled) label_state="$kind" labeled_at="$date" ;;
      push) [[ "$date" > "$pushed" ]] && pushed="$date" ;;
    esac
  done <<<"$events"
  [ "$label_state" = labeled ] ||
    { report "outside PR touches the security-review scope: a maintainer must review it and add the security-reviewed label"; return; }
  [[ "$labeled_at" > "$pushed" ]] ||
    report "security-reviewed was added before the PR's latest push ($pushed): review the new commits and add the label again"
}

stage="${1:-}"
# Stages with a budget from issue #1 print their duration on exit, and a warning when over budget (stage-timing.sh).
started="$(date +%s)" timing_script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/stage-timing.sh"
case "$stage" in
  fast | pre-commit) timed=("stage 1 ($stage)" 10) ;;
  signatures) timed=("stage 4 (signatures)" 30 "commit $(git rev-parse --short HEAD 2> /dev/null || echo none)") ;;
  *) timed=() ;;
esac
current_branch() { git branch --show-current 2> /dev/null | grep . || echo "detached"; }
# The run log (issue #269): one line per building run in this clone's git dir, newest first, the last 50.
# "<stage> <branch> <start> <end> <pass|fail> <built|reused>"; read by scripts/check-status.sh.
reused=built
case "$stage" in full | slow | signatures | pre-push) run_logged=1 ;; *) run_logged="" ;; esac
log_run() { # log_run EXIT_CODE
  local file result=pass
  [ "$1" -eq 0 ] || result=fail
  file="$(git rev-parse --absolute-git-dir 2> /dev/null)/check-run-log" || return 0
  [ "$file" != "/check-run-log" ] || return 0
  {
    echo "$stage $(current_branch) $(date -u -d "@$started" +%Y-%m-%dT%H:%M:%SZ) $(date -u +%Y-%m-%dT%H:%M:%SZ) $result $reused"
    cat "$file" 2> /dev/null || true
  } | head -n 50 > "$file.new" && mv "$file.new" "$file"
}
finish() {
  local code=$?
  rm -rf "${tmp:-}"
  [ -z "$run_logged" ] || log_run "$code" || true
  [ -z "${waited:-}" ] || rm -f "${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}.waiting/$$"
  [ -z "${locked:-}" ] || rm -rf "$locked"
  [ "${#timed[@]}" -eq 0 ] ||
    bash "$timing_script" report "${timed[0]}" "${timed[1]}" "$started" "$(date +%s)" \
      "${timed[@]:2}"
  exit "$code"
}

tmp="$(mktemp -d)" || die "could not create a temporary folder"
trap finish EXIT

# Stages that build take turns through one lock per machine, with no shared MSBuild nodes: several worktrees building
# at once stalled each other for over an hour (2026-11-17). METAPHOR_CHECK_SLOTS (default 2) runs may build at once, one
# lock directory each; a slot whose holder has exited is taken over (#281).
take_lock() {
  [ -z "${locked:-}" ] || return 0
  export MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0
  local base lock holder slots n
  base="${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}"
  slots="${METAPHOR_CHECK_SLOTS:-2}"
  [[ "$slots" =~ ^[1-9][0-9]*$ ]] || slots=2
  lock=""
  while [ -z "$lock" ]; do
    # Slot 1 is the original lock path (check-status.sh and older tools read it); slot N is "<path>.N".
    for ((n = 1; n <= slots; n++)); do
      lock="$base"
      [ "$n" -eq 1 ] || lock="$base.$n"
      if mkdir "$lock" 2> /dev/null; then break; fi
      holder="$(cat "$lock/pid" 2> /dev/null || true)"
      if [ -n "$holder" ] && ! kill -0 "$holder" 2> /dev/null; then
        rm -rf "$lock"
        if mkdir "$lock" 2> /dev/null; then break; fi
      fi
      lock=""
    done
    [ -z "$lock" ] || break
    if [ -z "${waited:-}" ]; then
      echo "check: waiting for another build check to finish ($base, $slots slots)" >&2
      mkdir -p "$base.waiting" 2> /dev/null && echo "$(current_branch) $(date +%s)" > "$base.waiting/$$"
    fi
    waited=1
    sleep "${METAPHOR_CHECK_POLL:-5}"
  done
  rm -f "$base.waiting/$$"
  # scripts/check-status.sh reads these; pid goes last, since a lock with no pid yet is never taken over.
  current_branch > "$lock/branch"
  date +%s > "$lock/since"
  echo "$$" > "$lock/pid"
  locked="$lock"
}
# pre-push takes the lock itself, only once it knows it will build (a docs-only push never waits).
case "$stage" in
  full)
    full_tree_id="$(full_tree)" || full_tree_id=""
    if [ -n "$full_tree_id" ] && cached_pass "$full_tree_id" full; then
      reused=reused
      echo "full: reused the recorded pass for tree $full_tree_id; no checks ran (change the tree to run them again)"
      exit 0
    fi
    take_lock ;;
  slow | signatures) take_lock ;;
esac

# list_entries prints "<mode> <blob> <stage>\t<path>\0" for each file to check, in `git ls-files -s -z` form.
case "$stage" in
  fast | pre-commit)
    list_entries() {
      git diff --cached --name-only --diff-filter=ACMR -z |
        xargs -0 -r git --literal-pathspecs ls-files -s -z --
    } ;;
  rules | full) list_entries() { git ls-files -s -z; } ;;
  commit-msg)
    [ -f "${2:-}" ] || die "commit-msg stage needs the message file"
    review_trailer "$2"
    check_message "commit message" < "$2"
    [ "$failures" -eq 0 ]
    exit ;;
  commits)
    # Merge commits are skipped: git writes their messages, and PRs are squash-merged.
    shas="$(git rev-list --no-merges "${2:-}" --)" || die "could not list the commits in ${2:-}"
    outside=0
    case "${PR_AUTHOR_ASSOCIATION:-}" in
      "" | OWNER | MEMBER | COLLABORATOR) ;;
      *) outside=1 ;;
    esac
    for sha in $shas; do
      git log -1 --format=%B "$sha" > "$tmp/message" || die "could not read commit $sha"
      check_message "commit $(git rev-parse --short "$sha")" < "$tmp/message"
      [ "$outside" -eq 1 ] || check_review_trailer "$sha"
    done
    # An outside PR is judged by its whole diff, so a scope change inside a merge commit still needs the label.
    if [ "$outside" -eq 1 ] && in_scope "${2:-}"; then
      require_review_label
    fi
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
  pre-push)
    pre_push_stage
    [ "$failures" -eq 0 ]
    exit ;;
  *) echo "usage: scripts/check.sh fast|pre-commit|pre-push|rules|full|docs|slow | signatures [EXE] | commit-msg FILE | commits BASE..HEAD | pr-body FILE" >&2
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

# The pre-commit hook's extra checks, each only when its files are staged, so a commit that doesn't touch them stays
# instant. Formatting reads the working tree, not the index: close enough for a hook, and CI checks the commit itself.
if [ "$stage" = pre-commit ]; then
  git diff --cached --name-only --diff-filter=ACMR > "$tmp/staged" || die "could not list the staged files"
  mapfile -t staged_cs < <(grep -E '\.cs$' "$tmp/staged")
  if [ "${#staged_cs[@]}" -gt 0 ]; then
    if [ ! -f "$solution" ]; then
      warn "no $(basename "$solution") at the repository root, so formatting wasn't checked"
    elif ! command -v dotnet > /dev/null; then
      warn "dotnet isn't on PATH, so formatting wasn't checked (CI checks it)"
    else
      (cd "$root" && DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 \
        dotnet format whitespace "$solution" --verify-no-changes --include "${staged_cs[@]}") ||
        report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
    fi
  fi
  if grep -qE '\.md$' "$tmp/staged"; then
    # In a subshell, which starts its own count: docs_stage changes directory, and an earlier failure isn't a docs one.
    (failures=0 && docs_stage && [ "$failures" -eq 0 ]) || report "docs: see the problems above"
  fi
  if grep -qxF src/Metaphor.Core/hook-signatures.json "$tmp/staged"; then
    if [ -n "${METAPHOR_GAME_EXE:-}" ]; then
      (failures=0 && signatures_stage "$METAPHOR_GAME_EXE" && [ "$failures" -eq 0 ]) ||
        report "signatures: see the problems above"
    else
      warn "hook signatures changed: set METAPHOR_GAME_EXE to check them against the game, or run: scripts/check.sh signatures EXE"
    fi
  fi
  if in_scope; then
    review_gate && skipping &&
      echo "security review: skipped (${SECURITY_REVIEW_SKIP}); the commit records the reason"
  fi
fi

# The .NET part of the full stage. Tests and formatting still run after a failed build, so one run shows everything.
if [ "$stage" = full ]; then
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  dotnet build "$solution" -c Release -v q || report "build: failed, see the errors above"
  dotnet test "$solution" -c Release --no-build -v q --filter "$(test_filter)" ||
    report "tests: Unit or RoundTrip tests failed"
  dotnet format whitespace "$solution" --verify-no-changes ||
    report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
  # The smoke-test and deploy scripts' own tests, against the stand-in game (no game needed). Pester 5 or later.
  # CI sets SKIP_SCRIPT_TESTS=1 because its own `scripts` job runs them in parallel.
  if [ -z "${SKIP_SCRIPT_TESTS:-}" ]; then
    pwsh -NoProfile -Command \
      'Import-Module Pester -MinimumVersion 5.0; $r = Invoke-Pester tests/smoke -PassThru -Output Normal; exit $r.FailedCount' ||
      report "tests: smoke or deploy script tests failed (run: Invoke-Pester tests/smoke)"
  fi
  # Record only a clean pass of an unchanged tree (nothing edited while the checks ran).
  if [ "$failures" -eq 0 ] && [ -n "${full_tree_id:-}" ] && [ "$(full_tree)" = "$full_tree_id" ]; then
    record_pass full "$full_tree_id"
  fi
fi

[ "$failures" -eq 0 ]
