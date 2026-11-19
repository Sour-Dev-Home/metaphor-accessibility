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

# The test categories to run. WindowsOnly tests pin down Windows behaviour (the mod only runs there), so they run on
# Windows alone; on Linux (the pull-request build in CI) they are left out.
category_filter() {
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
  # markdownlint-cli2 also loads a config from each linted file's folder, even with --config, and a .cjs or .mjs one
  # (or a custom rule a .jsonc one names) runs code in the docs step (#322). Only tools/docs/, in the security-review
  # scope, may hold one; with any other, markdownlint doesn't run. The name matches in any case, as Windows reads it
  # (#324); the folder doesn't, since the review scope's tools/docs/ doesn't cover a Tools/Docs/ that Windows merges in.
  local configs=()
  while IFS= read -r -d '' file; do configs+=("$file"); done < <(
    git ls-files -z -- ':(glob,icase)**/.markdownlint*' ':(exclude,glob)tools/docs/**'
  )
  if [ "${#configs[@]}" -gt 0 ]; then
    for file in "${configs[@]}"; do
      report "$file: markdownlint config outside tools/docs/; markdownlint would load it and run its code"
    done
  elif tool="$(find_tool markdownlint-cli2)"; then
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
# full_kinds: print the kind this full records, then the kinds it may reuse. A full with SKIP_SCRIPT_TESTS=1 (CI's,
# whose own jobs run the script tests) records full-skipped, which serves only another skipped run, never a plain full.
full_kinds() {
  if [ -n "${SKIP_SCRIPT_TESTS:-}" ]; then echo "full-skipped full"; else echo full; fi
}
# full_tree: print the index's tree, only when the working tree matches it (the build reads the working tree).
full_tree() {
  git diff --quiet 2> /dev/null && git write-tree 2> /dev/null
}

# select_tests UNKNOWN CHANGED: print test-select.sh's lines for the pushed change; nothing when the whole solution runs
# (an unknown change set, CI, or METAPHOR_FULL_TESTS=1).
whole_solution() { [ "$1" -eq 1 ] || [ "${GITHUB_ACTIONS:-}" = true ] || [ -n "${METAPHOR_FULL_TESTS:-}" ]; }
select_tests() {
  whole_solution "$1" && return 0
  sed '/^$/d' <<< "$2" | bash "$check_dir/test-select.sh"
}

# build_selected SOLUTION ROOT UNKNOWN SELECTION: the pre-push build (#314). Builds the whole solution when the change
# set is unknown, in CI or with METAPHOR_FULL_TESTS=1, when a build-wide file changed, and when the selection names
# nothing to build (a code change the selector couldn't place). Otherwise builds just the selected test projects and
# the changed projects and their dependents that no test reaches (#332); a project's build brings the projects it
# references. Returns 1 on a failure.
build_selected() {
  local solution="$1" root="$2" unknown="$3" selection="$4" kind proj reason ok=0 any=0
  if whole_solution "$unknown" || grep -q '^solution ' <<< "$selection" || ! grep -qE '^(dotnet|build) ' <<< "$selection"; then
    timed_step build dotnet build "$solution" -c Release -v q --nologo
    return
  fi
  while read -r kind proj reason; do
    [ "$kind" = dotnet ] || [ "$kind" = build ] || continue
    any=1
    timed_step "build:$(basename "$proj" .csproj)" dotnet build "$root/$proj" -c Release -v q --nologo || ok=1
  done <<< "$selection"
  [ "$any" -eq 1 ] || ok=1
  return "$ok"
}

# run_selected_tests SOLUTION UNKNOWN SELECTION: local test selection (issue #243, scripts/test-select.sh). Runs only the
# test projects the changed files can affect and says which ran and why. With an unknown change set, in CI, or with
# METAPHOR_FULL_TESTS=1 it runs the whole solution. `full` (CI, merge queue) never calls this.
run_selected_tests() {
  local solution="$1" unknown="$2" selection="$3" kind proj reason ran=0
  if whole_solution "$unknown"; then
    echo "tests: all suites (change set unknown, CI, or METAPHOR_FULL_TESTS)"
    timed_step tests dotnet test "$solution" -c Release --no-build -v q --filter "$(category_filter)" ||
      report "tests: Unit or RoundTrip tests failed"
    return 0
  fi
  while read -r kind proj reason; do
    if [ "$kind" = pester ] && [ -z "${SKIP_SCRIPT_TESTS:-}" ] && command -v pwsh > /dev/null; then
      echo "tests: Pester smoke tests because $proj $reason"
      timed_step pester bash "$check_dir/run-pester.sh" ||
        report "tests: smoke or deploy script tests failed (run: bash scripts/run-pester.sh)"
      ran=1
    fi
    [ "$kind" = dotnet ] || continue
    ran=1
    echo "tests: $proj because $reason"
    timed_step "tests:$(basename "$proj" .csproj)" dotnet test "$proj" -c Release --no-build -v q --filter "$(category_filter)" ||
      report "tests: $proj failed"
  done <<< "$selection"
  [ "$ran" -eq 1 ] || echo "tests: no test project is affected by this change"
}

# The pre-push hook's stage. Git passes "<local ref> <local sha> <remote ref> <remote sha>" per pushed ref on stdin.
# Builds and runs the Unit and RoundTrip tests unless every pushed commit leaves code alone. When it can't tell what
# changed (a new branch with no origin/main to compare, or a remote commit not fetched), it builds.
pre_push_stage() {
  local zero=0000000000000000000000000000000000000000 local_ref local_sha remote_ref remote_sha base
  local changed="" unknown=0 pushed=0 root solution trees=() tree cached=1 selection
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
    # shellcheck disable=SC2046 # full_kinds prints separate words
    cached_pass "$tree" $(full_kinds) pre-push || cached=0
  done
  if [ "$cached" -eq 1 ]; then
    reused=reused
    echo "pre-push: reused the recorded pass for tree ${trees[*]}; no build or tests ran"
    return 0
  fi
  take_lock nowait
  root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
  solution="$root/Metaphor.sln"
  [ -f "$solution" ] || die "pre-push stage needs $(basename "$solution") at the repository root"
  command -v dotnet > /dev/null || die "pre-push stage needs the .NET SDK (dotnet) on PATH"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  # Tests run only after a good build: --no-build would otherwise test the last good build and pass.
  selection="$(select_tests "$unknown" "$changed")"
  if build_selected "$solution" "$root" "$unknown" "$selection"; then
    run_selected_tests "$solution" "$unknown" "$selection"
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
# Builtins only: the docs stage runs it once per ADR, where tr, awk, wc and tr were four starts each (#328).
fence_re='^[[:space:]]*(```|~~~)'
heading_re='^#+ '
prose_words() {
  local line code=0 count=0 words
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line//$'\r'/}"
    if [[ "$line" =~ $fence_re ]]; then
      code=$((1 - code))
      continue
    fi
    [ "$code" -eq 0 ] && ! [[ "$line" =~ $heading_re ]] || continue
    read -r -a words <<< "$line"
    count=$((count + ${#words[@]}))
  done
  echo "$count"
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

# Builtins throughout, where a sed, a wc and two greps were four starts per message (#328).
scissors_re='^# -* >8 -*$'
trailer_re='^[A-Za-z][A-Za-z0-9-]*: '

# char_count VAR TEXT: set VAR to TEXT's length in UTF-8 characters, not bytes.
char_count() {
  local LC_ALL=C.UTF-8
  printf -v "$1" '%s' "${#2}"
}

# every_line_a_trailer TEXT: exit 0 when every line of TEXT reads "Key: value".
every_line_a_trailer() {
  local line
  while IFS= read -r line; do [[ "$line" =~ $trailer_re ]] || return 1; done <<< "$1"
}
check_message() {
  local label="$1" message="" line subject body last length lines
  # Like git: drop CR, trailing spaces, comment lines and everything below the scissors line, then trailing and leading
  # blank lines.
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}" && line="${line%"${line##*[![:space:]]}"}"
    [[ "$line" =~ $scissors_re ]] && break
    [ "${line:0:1}" = "#" ] || message+="$line"$'\n'
  done
  message="${message%"${message##*[!$'\n']}"}" # trailing blank lines
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
    char_count length "$subject"
    [ "$length" -le "$max_subject_chars" ] ||
      report "$label: subject is $length characters; the limit is $max_subject_chars"
  fi

  [ -z "$body" ] && return
  [ -z "${body%%$'\n'*}" ] || report "$label: leave a blank line after the subject"
  # The last paragraph holds trailers (Co-Authored-By:, Signed-off-by:) when every line is "Key: value", as git reads
  # them. They don't count, even when they are the whole body.
  body="${body#$'\n'}"
  last="${body##*$'\n\n'}"
  if every_line_a_trailer "$last"; then
    [ "$last" = "$body" ] && body="" || body="${body%$'\n\n'*}"
  fi
  lines=0
  while IFS= read -r line; do [[ "$line" != *[^[:space:]]* ]] || lines=$((lines + 1)); done <<< "$body"
  [ "$lines" -le "$max_body_lines" ] || report "$label: body has $lines lines; the limit is $max_body_lines"
}

# The security review's stamp script answers for the repository in the current folder.
stamp_script="$check_dir/security-stamp.sh"

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

# stamp_check CHECK [COMMIT]: run the stamp script's yes-or-no CHECK; exit 0 for yes, 1 for no, die when it fails.
stamp_check() {
  bash "$stamp_script" "$@"
  case $? in
    0) return 0 ;;
    1) return 1 ;;
    *) die "security review: could not run the stamp script's $1 check${2:+ on $2}" ;;
  esac
}

# review_gate: a staged change to the scope needs a stamp matching it, unless SECURITY_REVIEW_SKIP gives a reason or
# the commit is a clean merge from main (docs/adr/0012-review-trailer-covers-clean-merges.md). Exit 0 when the commit
# may go ahead, setting review_by (skip, stamp or clean-merge), which review_trailer reads to pick the trailer.
review_gate() {
  review_by=skip
  skipping && return 0
  review_by=stamp
  stamp_check stamped && return 0
  review_by=clean-merge
  stamp_check clean-merge && return 0
  report "security review: the staged hook or deploy code has no matching stamp. Run the security-reviewer agent \
(it stamps when it finds nothing critical or important), or set SECURITY_REVIEW_SKIP=\"<reason>\" (maintainer only). A \
clean merge from origin/main needs none (ADR 0012)"
  return 1
}

# review_trailer FILE: on a commit touching the scope, replace any Security-Review trailers in the message FILE with the
# one this commit earns. A clean merge from main earns none: CI judges it by its first parent.
review_trailer() {
  local file="$1" trailer=""
  in_scope || return 0
  review_gate || return 0
  case "$review_by" in
    skip) trailer="Security-Review-Skipped: ${SECURITY_REVIEW_SKIP//[$'\r\n']/ }" ;;
    stamp) trailer="Security-Review: $(bash "$stamp_script" hash)" || die "could not hash the security-review scope" ;;
    clean-merge) echo "security review: not needed, a clean merge from main: each hook-scope file is the version one \
side changed, reviewed there (ADR 0012)" ;;
  esac
  grep -viE '^Security-Review(-Skipped)?:' "$file" > "$tmp/message"
  [ $? -le 1 ] || die "could not read $file"
  if [ -n "$trailer" ]; then
    git interpret-trailers --trailer "$trailer" < "$tmp/message" > "$file" || die "could not add the trailer to $file"
  else
    cp "$tmp/message" "$file" || die "could not write $file"
  fi
}

# check_head_review BASE..HEAD: when the range touches the scope, judge HEAD alone, walking first parents through clean
# merges and commits leaving the scope alone (docs/adr/0012-review-trailer-covers-clean-merges.md).
check_head_review() {
  local base="${1%%..*}" sha="${1#*..}" head parent
  in_scope "$1" || return 0
  head="$(git rev-parse -q --verify "${sha#.}^{commit}")" || die "could not read the head of $1"
  sha="$head"
  while :; do
    git merge-base --is-ancestor "$sha" "$base"
    case $? in
      0) return 0 ;;
      1) ;;
      *) die "could not tell whether $sha is on $base" ;;
    esac
    has_review_trailer "$sha" && return 0
    parent="$(git rev-parse -q --verify "$sha^1")" || break # a root commit has nothing to pass through
    { ! in_scope "$sha" || stamp_check clean-merge "$sha"; } || break
    sha="$parent"
  done
  report "commit $(git rev-parse --short "$sha"): touches the security-review scope without a Security-Review trailer \
matching its content, or a Security-Review-Skipped one on the maintainer's PR$([ "$sha" = "$head" ] || echo "; HEAD \
reaches it through clean merges from main or commits leaving the scope alone (ADR 0012)")"
}

# has_review_trailer SHA: exit 0 when SHA carries "Security-Review: <a hash the stamp script accepts for it>" (its scope
# hash, or its old-scope hash from before #306) or, on the maintainer's PR, a Security-Review-Skipped trailer with a
# reason.
has_review_trailer() {
  local sha="$1" want reviewed skipped hash
  want="$(bash "$stamp_script" accepted "$sha")" || die "could not hash the security-review scope of $sha"
  # Both trailers from one git start, split at a \x01 byte. A trailer holding one of its own would move the split, so
  # more than one fails closed.
  local format='%(trailers:key=Security-Review,valueonly)%x01%(trailers:key=Security-Review-Skipped,valueonly)'
  reviewed="$(git log -1 --format="$format" "$sha")" || die "could not read $sha"
  skipped="${reviewed#*$'\x01'}"
  [[ "$skipped" != *$'\x01'* && "$reviewed" == *$'\x01'* ]] || die "could not read the trailers of $sha"
  reviewed="${reviewed%%$'\x01'*}"
  for hash in $want; do
    grep -qxF "$hash" <<<"$reviewed" && return 0
  done
  # SECURITY_REVIEW_SKIP is the maintainer's, so a skip trailer counts only on the maintainer's PR (#309).
  [ -n "${skipped//[[:space:]]/}" ] && maintainer_pr
}

# is_admin LOGIN: exit 0 when LOGIN has admin permission on GITHUB_REPOSITORY, 1 when not (or the login isn't one GitHub
# could issue, so it never reaches the API path); dies when the permission can't be read.
is_admin() {
  local permission
  [[ "$1" =~ ^[A-Za-z0-9][A-Za-z0-9-]*(\[bot\])?$ ]] || return 1
  permission="$(gh api "repos/${GITHUB_REPOSITORY:-}/collaborators/$1/permission" --jq .permission)" ||
    die "could not read the permission of $1 on ${GITHUB_REPOSITORY:-}"
  [ "$permission" = admin ]
}

# maintainer_pr: exit 0 when PR_AUTHOR_ASSOCIATION is empty (a local run) or OWNER, or the PR's author (PR_AUTHOR) is
# an admin: in an organization's repository the maintainer's own PRs show MEMBER. Cached after the first call.
maintainer_pr() {
  if [ -z "${maintainer_pr_result:-}" ]; then
    maintainer_pr_result=no
    case "${PR_AUTHOR_ASSOCIATION:-}" in
      "" | OWNER) maintainer_pr_result=yes ;;
      *) [ -n "${PR_AUTHOR:-}" ] && is_admin "$PR_AUTHOR" && maintainer_pr_result=yes ;;
    esac
  fi
  [ "$maintainer_pr_result" = yes ]
}

# require_review_label PUSHED: a PR not by the maintainer changing files outside docs/ needs the security-reviewed
# label, added by an admin after its latest push (#127, #309). CI's token is read-only, so it can't remove a stale
# label; it reads the timeline instead. Push time is the latest of the timeline's commits and force pushes and PUSHED (a
# time GitHub set, so a backdated commit can't make a label look fresh; empty when the caller checked the push another
# way). ISO 8601 UTC timestamps compare as text. Only the latest label event counts, so a non-admin re-adding the label
# replaces the maintainer's.
require_review_label() {
  local repo="${GITHUB_REPOSITORY:-}" pr="${PR_NUMBER:-}" events kind date actor pushed="$1"
  local label_state="" labeled_at="" labeled_by=""
  events="$(gh api --paginate "repos/$repo/issues/$pr/timeline" --jq \
    '.[] | (select((.event=="labeled" or .event=="unlabeled") and .label.name=="security-reviewed")
       | "\(.event)\t\(.created_at)\t\(.actor.login // "")"),
     (select(.event=="head_ref_force_pushed") | "push\t\(.created_at)"),
     (select(.event=="committed") | "push\t\(.committer.date)")')" || die "could not read the timeline of PR #$pr"
  while IFS=$'\t' read -r kind date actor; do
    case "$kind" in
      labeled | unlabeled) label_state="$kind" labeled_at="$date" labeled_by="$actor" ;;
      push) [[ "$date" > "$pushed" ]] && pushed="$date" ;;
    esac
  done <<<"$events"
  [ "$label_state" = labeled ] ||
    { report "PR not by the maintainer changes files outside docs/: a maintainer must review it and add the \
security-reviewed label"; return; }
  is_admin "$labeled_by" ||
    { report "security-reviewed was added by $labeled_by, who isn't an admin: a maintainer must review the PR and add \
the label"; return; }
  [[ "$labeled_at" > "$pushed" ]] ||
    report "security-reviewed was added before the PR's latest push ($pushed): review the new commits and add the label again"
}

# head_arrived SHA REF REPO: print when SHA became the head of the PR from branch REF of repository REPO, from the
# review-gate workflow's runs, whose creation times GitHub sets, where a commit date can be backdated (#321). Every push
# starts a run, so it is the oldest run in the newest unbroken stretch of runs at SHA on that branch: a run at another
# head ends the stretch. Reading only the latest 100 can only make the time later, which fails closed. Fails when no run
# at SHA is listed, or when the workflow's file is renamed, so the gate fails closed then too.
head_arrived() {
  local runs sha created repo arrived=""
  runs="$(gh api -X GET "repos/$GITHUB_REPOSITORY/actions/workflows/review-gate.yml/runs" -f event=pull_request_target \
    -f branch="$2" -F per_page=100 \
    --jq '.workflow_runs[] | "\(.head_sha)\t\(.created_at)\t\(.head_repository.full_name // "")"')" || return 1
  while IFS=$'\t' read -r sha created repo; do
    [ "$repo" = "$3" ] || continue
    [ "$sha" = "$1" ] || break
    arrived="$created"
  done < <(sort -t $'\t' -k2,2r <<<"$runs")
  [ -n "$arrived" ] && echo "$arrived"
}

# review_label_stage FILE: the review gate the review-gate workflow runs from main's copy (#330), so a PR can't edit the
# check that judges it. FILE lists the PR's changed files as the API gives them, never the PR's code: one per line, the
# path, then a tab and the old path for a rename. The event gives PR_CHANGED_FILES, PR_UPDATED_AT, PR_HEAD_SHA,
# PR_EVENT_ACTION and PR_EVENT_LABEL, beside what maintainer_pr and require_review_label read.
review_label_stage() {
  local path previous count=0 outside="" pushed="${PR_UPDATED_AT:-}" head head_ref head_repo created
  [ -f "$1" ] || die "review-label stage needs the file listing the PR's changed files"
  [[ "${PR_CHANGED_FILES:-}" =~ ^[0-9]+$ ]] || die "review-label stage needs PR_CHANGED_FILES, the event's file count"
  while IFS=$'\t' read -r path previous; do
    count=$((count + 1))
    [[ "$path" == docs/* && ( -z "$previous" || "$previous" == docs/* ) ]] || outside=1
  done < "$1"
  # The API lists at most 3000 files; a shorter list than the event's count could hide a change outside docs/.
  [ "$count" -eq "$PR_CHANGED_FILES" ] || die "listed $count of the PR's $PR_CHANGED_FILES changed files"
  [ -n "$outside" ] && ! maintainer_pr || return 0
  [ -n "${GITHUB_REPOSITORY:-}" ] && [ -n "${PR_NUMBER:-}" ] ||
    die "a PR not by the maintainer needs GITHUB_REPOSITORY and PR_NUMBER to read its timeline"
  # A label event's updated_at is the event's own time, no older than the review label, so any label's run checks the
  # push another way (#321), never by skipping, which would count as a pass. The event's payload names the head when the
  # label changed, and that head must still be the PR's head. Adding security-reviewed then needs nothing more: it went
  # on at this head. Any other label event reads when this head arrived (head_arrived).
  if [ "${PR_EVENT_ACTION:-}" = labeled ] || [ "${PR_EVENT_ACTION:-}" = unlabeled ]; then
    head="$(gh api "repos/$GITHUB_REPOSITORY/pulls/$PR_NUMBER" \
      --jq '[.head.sha, .head.ref, (.head.repo.full_name // ""), .created_at] | @tsv')" ||
      die "could not read the head of PR #$PR_NUMBER"
    IFS=$'\t' read -r head head_ref head_repo created <<<"$head"
    [ -n "${PR_HEAD_SHA:-}" ] && [ "$head" = "$PR_HEAD_SHA" ] ||
      { report "PR #$PR_NUMBER was pushed to after security-reviewed was added: review the new commits and add the \
label again"; return; }
    pushed=""
    if [ "$PR_EVENT_ACTION" != labeled ] || [ "${PR_EVENT_LABEL:-}" != security-reviewed ]; then
      pushed="$(head_arrived "$head" "$head_ref" "$head_repo")" ||
        die "could not tell when the head of PR #$PR_NUMBER arrived from the review-gate runs"
      # The runs are the branch's, not the PR's: on a reused branch name an older PR's run could date the head too
      # early, so it arrived no sooner than this PR was opened.
      [ -n "$created" ] || die "could not read when PR #$PR_NUMBER was opened"
      [[ "$created" < "$pushed" ]] || pushed="$created"
    fi
  else
    [ -n "$pushed" ] || die "a PR not by the maintainer needs PR_UPDATED_AT, the event's update time"
  fi
  require_review_label "$pushed"
}

# Stages with a budget from issue #1 print their duration on exit, and a warning when over budget (stage-timing.sh).
timing_script="$check_dir/stage-timing.sh"
current_branch() {
  local branch
  branch="$(git branch --show-current 2> /dev/null)"
  echo "${branch:-detached}"
}
# The run log (issue #269): one line per building run in this clone's git dir, newest first, the last 50.
# "<stage> <branch> <start> <end> <pass|fail> <built|reused>"; read by scripts/check-status.sh.
# Per-step times of a building stage (#314): "restore=12s build=80s ...", printed when the run ends and kept as the run
# log line's last field, so a slow step is named by data.
timed_step() { # timed_step NAME COMMAND...: run COMMAND and note how long it took; its exit code is returned
  local name="$1" began=$SECONDS code
  shift
  "$@"
  code=$?
  step_times+=("$name=$((SECONDS - began))s")
  # full's steps go to the live log (scripts/live-log.sh) as they finish.
  [ -z "$live_log" ] || live_line "$name" "$code" "$((SECONDS - began))"
  return "$code"
}
log_run() { # log_run EXIT_CODE
  local file result=pass times="" from to
  [ "$1" -eq 0 ] || result=fail
  [ "${#step_times[@]}" -eq 0 ] || times=" ${step_times[*]}"
  file="$(git rev-parse --absolute-git-dir 2> /dev/null)/check-run-log" || return 0
  [ "$file" != "/check-run-log" ] || return 0
  TZ=UTC0 printf -v from '%(%Y-%m-%dT%H:%M:%SZ)T' "$started"
  TZ=UTC0 printf -v to '%(%Y-%m-%dT%H:%M:%SZ)T' -1
  {
    echo "$stage $(current_branch) $from $to $result $reused$times"
    cat "$file" 2> /dev/null || true
  } | head -n 50 > "$file.new" && mv "$file.new" "$file"
}
finish() {
  local code=$?
  rm -rf "${tmp:-}"
  # github-outage.sh's classify matches this line's start (#321).
  [ "${#step_times[@]}" -eq 0 ] || echo "step times: ${step_times[*]}"
  [ -z "$run_logged" ] || log_run "$code" || true
  [ -z "${waited:-}" ] || rm -f "${METAPHOR_CHECK_LOCK:-${TMPDIR:-/tmp}/metaphor-check.lock}.waiting/$BASHPID"
  [ -z "${locked:-}" ] || rm -rf "$locked"
  [ -z "$live_log" ] || live_end "$code"
  [ "${#timed[@]}" -eq 0 ] ||
    bash "$timing_script" report "${timed[0]}" "${timed[1]}" "$started" "$EPOCHSECONDS" \
      "${timed[@]:2}"
  exit "$code"
}

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
    # The pre-push hook must not wait: GitHub closes an idle push connection and git exits 141 (#284).
    # github-outage.sh's classify matches "every build slot is busy", "waiting for another build check" and "step
    # times:" to call a push cut after them a real failure, not an outage (#321): keep that wording in step.
    [ "${1:-}" != nowait ] || die "every build slot is busy (see scripts/check-status.sh), so this push would wait with its connection open and be cut. Run 'bash scripts/check.sh full' first, then push: the hook reuses the recorded pass."
    if [ -z "${waited:-}" ]; then
      echo "check: waiting for another build check to finish ($base, $slots slots)" >&2
      local now
      printf -v now '%(%s)T' -1
      mkdir -p "$base.waiting" 2> /dev/null && echo "$(current_branch) $now" > "$base.waiting/$BASHPID"
    fi
    waited=1
    sleep "${METAPHOR_CHECK_POLL:-5}"
  done
  rm -f "$base.waiting/$BASHPID"
  # scripts/check-status.sh reads these; pid goes last, since a lock with no pid yet is never taken over. $BASHPID is $$
  # in a run of the script, and the subshell's own pid when the tests run main in one.
  current_branch > "$lock/branch"
  echo "$EPOCHSECONDS" > "$lock/since"
  echo "$BASHPID" > "$lock/pid"
  locked="$lock"
}

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
    # In a subshell, which starts its own count: docs_stage changes directory, and an earlier failure isn't a docs one.
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

# full_dotnet_checks: the .NET part of the full stage. Tests and formatting still run after a failed build, so one run
# shows everything.
full_dotnet_checks() {
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  # In CI, restore fails when a lock file no longer matches its project: the security job audits the lock files
  # alone (#324), so the flag goes on the command line, not only in Directory.Build.props, where a project could
  # override it.
  local restore_flags=()
  [ -z "${GITHUB_ACTIONS:-}" ] || restore_flags=(--locked-mode)
  timed_step restore dotnet restore "$solution" "${restore_flags[@]}" -v q ||
    report "build: restore failed, see the errors above"
  timed_step build dotnet build "$solution" -c Release --no-restore -v q || report "build: failed, see the errors above"
  timed_step tests dotnet test "$solution" -c Release --no-build -v q --filter "$(category_filter)" ||
    report "tests: Unit or RoundTrip tests failed"
  timed_step format dotnet format whitespace "$solution" --verify-no-changes ||
    report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
  # The smoke-test and deploy scripts' own tests, against the stand-in game (no game needed). Pester 5 or later, every
  # test of every file, each file split across processes run at once (scripts/run-pester.sh). Then the check-script
  # suites (scripts/run-check-suites.sh).
  # CI sets SKIP_SCRIPT_TESTS=1 because its own jobs run both.
  if [ -z "${SKIP_SCRIPT_TESTS:-}" ]; then
    timed_step pester bash "$check_dir/run-pester.sh" ||
      report "tests: smoke or deploy script tests failed (run: bash scripts/run-pester.sh)"
    timed_step check-scripts bash "$check_dir/run-check-suites.sh" --select ||
      report "tests: check-script suites failed (run: bash scripts/run-check-suites.sh)"
  fi
  # Record only a clean pass of an unchanged tree (nothing edited while the checks ran), under the kind full_kinds reads.
  if [ "$failures" -eq 0 ] && [ -n "${full_tree_id:-}" ] && [ "$(full_tree)" = "$full_tree_id" ]; then
    record_pass "$(full_kinds | cut -d ' ' -f 1)" "$full_tree_id"
  fi
}

# Run when executed; when sourced, stop at the definitions.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then main "$@"; fi
