# shellcheck shell=bash
# check.sh's build and tests (ADR 0013): test selection, the pre-push build and tests, full's .NET checks, and the slow
# and signatures stages. Sourced by scripts/check.sh only; it uses report and die from report.sh, and cached_pass,
# record_pass, full_kinds, full_tree, take_lock and timed_step from run.sh.
#
# Run globals: full_dotnet_checks reads `solution` and `full_tree_id`, which main sets; pre_push_stage sets `reused`,
# which main resets (its `root` and `solution` are local); both read `failures`, which report updates. `check_dir` is
# check.sh's constant, which select_tests, run_selected_tests and full_dotnet_checks read. Reads GITHUB_ACTIONS,
# METAPHOR_FULL_TESTS, SKIP_SCRIPT_TESTS and METAPHOR_GAME_EXE; exports DOTNET_CLI_TELEMETRY_OPTOUT and DOTNET_NOLOGO.
# Private to this module: code_path_re.

# The test categories to run. WindowsOnly tests pin down Windows behaviour (the mod only runs there), so they run on
# Windows alone; on Linux (the pull-request build in CI) they are left out.
category_filter() {
  case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*) echo "Category=Unit|Category=RoundTrip|Category=WindowsOnly" ;;
    *) echo "Category=Unit|Category=RoundTrip" ;;
  esac
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

# select_tests UNKNOWN CHANGED: print test-select.sh's lines for the pushed change; nothing when the whole solution runs
# (an unknown change set, CI, or METAPHOR_FULL_TESTS=1).
whole_solution() { [ "$1" -eq 1 ] || [ "${GITHUB_ACTIONS:-}" = true ] || [ -n "${METAPHOR_FULL_TESTS:-}" ]; }
select_tests() {
  whole_solution "$1" && return 0
  sed '/^$/d' <<< "$2" | bash "$check_dir/ci/test-select.sh"
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

# run_selected_tests SOLUTION UNKNOWN SELECTION: local test selection (issue #243, scripts/ci/test-select.sh). Runs only the
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
      timed_step pester bash "$check_dir/check/run-pester.sh" ||
        report "tests: smoke or deploy script tests failed (run: bash scripts/check/run-pester.sh)"
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
  # test of every file, each file split across processes run at once (scripts/check/run-pester.sh). Then the check-script
  # suites (scripts/check/run-check-suites.sh).
  # CI sets SKIP_SCRIPT_TESTS=1 because its own jobs run both.
  if [ -z "${SKIP_SCRIPT_TESTS:-}" ]; then
    timed_step pester bash "$check_dir/check/run-pester.sh" ||
      report "tests: smoke or deploy script tests failed (run: bash scripts/check/run-pester.sh)"
    timed_step check-scripts bash "$check_dir/check/run-check-suites.sh" --select ||
      report "tests: check-script suites failed (run: bash scripts/check/run-check-suites.sh)"
  fi
  # Record only a clean pass of an unchanged tree (nothing edited while the checks ran), under the kind full_kinds reads.
  if [ "$failures" -eq 0 ] && [ -n "${full_tree_id:-}" ] && [ "$(full_tree)" = "$full_tree_id" ]; then
    record_pass "$(full_kinds | cut -d ' ' -f 1)" "$full_tree_id"
  fi
}
