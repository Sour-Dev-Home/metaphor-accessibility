#!/usr/bin/env bash
# These files changed: what runs (#413). The one place that decides it, for CI's jobs and the local hooks alike.
#
#   what-runs.sh < PATHS                  changed paths on stdin, one per line; prints one line per thing that runs:
#     code                       <reason>   CI's build and security job: anything except docs/ and Markdown files
#     compile                    <reason>   a file that can change the build or the tests' result: the pre-push builds
#     dotnet <test project .csproj> <reason>  a test project to build and run
#     build <project .csproj>    <reason>   a changed project, or one depending on it, that no test project reaches:
#                                           build it, nothing to run (#332)
#     solution                   <reason>   a build-wide file changed: build the whole solution
#     pester                     <reason>   the Pester tests (CI's scripts job; locally run-pester.sh): scripts/,
#                                           tests/pester/, the stand-in game, the installer exe and the workflows
#     pester-reads               <reason>   a file the Pester tests also read, which doesn't run them on its own: the
#                                           projects they build (the hooks, the stand-in game, the signature check)
#                                           and all they reference, the build-wide files, tools/ghidra and the clone
#                                           set-up page. A local merge check counts it (#728)
#     docs                       <reason>   the docs stage
#     check-tests                <reason>   the check-script suites (run-check-suites.sh --select): the check scripts,
#                                           their tests and the files those tests read
#     varied-setup               <reason>   CI's pester-varied-setup job (#655): the player, setup and release
#                                           scripts, the installer exe, the Pester tests, their runner, the Pester install
#                                           and ci.yml
#   what-runs.sh --job code|scripts|varied-setup [BASE HEAD]
#                                           CI: prints "run=true" or "run=false" (and appends it to $GITHUB_OUTPUT when
#                                           set) from the `code`, `pester` or `varied-setup` line for the diff BASE...HEAD
#
# A test project is selected when the changed file sits in it, or in a project it references, directly or through
# other projects (the ProjectReference graph in the .csproj files). Build-wide files (Directory.*, global.json, the
# solution) select every test project. The selection is what the change needs and nothing more: running everything
# (CI, `check.sh full`, METAPHOR_FULL_TESTS=1) is the caller's choice. Run from the repository root.
#
# --job: only pull requests skip anything, except that varied-setup also skips in the merge queue and on a push. Any
# other event (EVENT_NAME: the merge queue and a push to main; for varied-setup the nightly schedule and a run by hand)
# runs everything, as does a diff that can't be read or is empty. A job that gets "false" skips its steps and still
# finishes green, so its required check always reports.
set -uo pipefail

# job JOB BASE HEAD: the --job mode.
job() {
  local kind files
  case "${1:-}" in
    code) kind=code ;;
    scripts) kind=pester ;;
    varied-setup) kind=varied-setup ;;
    *) echo "usage: what-runs.sh --job code|scripts|varied-setup [BASE HEAD]" >&2; exit 2 ;;
  esac
  emit() {
    echo "run=$1"
    [ -z "${GITHUB_OUTPUT:-}" ] || echo "run=$1" >> "$GITHUB_OUTPUT"
    exit 0
  }
  # The varied setup also skips in the merge queue and on main; the nightly run and a run by hand take everything.
  case "$kind ${EVENT_NAME:-}" in
    *" pull_request" | "varied-setup merge_group" | "varied-setup push") ;;
    *) emit true ;;
  esac
  [ -n "${2:-}" ] && [ -n "${3:-}" ] || emit true
  files="$(git diff --name-only "$2...$3")" || emit true
  [ -n "$files" ] || emit true
  # Captured first: under pipefail, grep -q's early exit would fail the pipe and read as "false".
  files="$(select_runs <<< "$files")" || emit true
  if grep -q "^$kind " <<< "$files"; then emit true; else emit false; fi
}

# select_runs: the stdin mode.
select_runs() {
  local -a files projects
  mapfile -t files
  [ "${#files[@]}" -gt 0 ] || return 0

  all_tests() { git ls-files -- 'tests/*.csproj' | grep -E 'Tests\.csproj$'; }

  mapfile -t projects < <(git ls-files -- '*.csproj')
  local -A dir_of rdeps deps hit why build_seen pester_built
  local p r t
  for p in "${projects[@]}"; do
    dir_of[$p]="$(dirname "$p")"
    while read -r r; do
      r="${r//\\//}"
      t="${dir_of[$p]}/$r"
      while [[ "$t" == *"/../"* ]]; do t="$(sed -E 's#[^/]+/\.\./##' <<< "$t")"; done # fold "a/../" away
      t="${t//\/.\//\/}"
      rdeps[$t]+="$p "
      deps[$p]+="$t "
    done < <(grep -oE 'ProjectReference Include="[^"]+"' "$p" | sed -E 's/.*Include="([^"]+)"/\1/')
  done
  # The projects the Pester tests build (the hooks, the stand-in game, the signature check) and everything they
  # reference, transitively, from the graph above: a file in any of them, its lock file included, is read by Pester.
  local todo=(src/Metaphor.Hooks/Metaphor.Hooks.csproj tests/Metaphor.StandInGame/Metaphor.StandInGame.csproj
    tools/Metaphor.SignatureCheck/Metaphor.SignatureCheck.csproj)
  while [ "${#todo[@]}" -gt 0 ]; do
    p="${todo[0]}" todo=("${todo[@]:1}")
    [ -z "${pester_built[$p]:-}" ] || continue
    pester_built[$p]=1
    for t in ${deps[$p]:-}; do todo+=("$t"); done
  done

  mark() { # mark PROJECT REASON REASON: add the project and everything that references it, transitively
    local p="$1" q
    set -- "$1" "$2" "${3:-$2}"
    [ -z "${hit[$p]:-}" ] || return 0
    hit[$p]=1
    why[$p]="$2"
    for q in ${rdeps[$p]:-}; do mark "$q" "$3 (via $(basename "$p" .csproj), which it references)"; done
  }

  reaches_test() { # reaches_test PROJECT: the project, or one referencing it (transitively), is a test project
    # A walk with a visited set, so a reference cycle ends and a shared dependent is read once (#321).
    local -A seen=()
    local todo=("$1") p q
    while [ "${#todo[@]}" -gt 0 ]; do
      p="${todo[0]}" todo=("${todo[@]:1}")
      [ -z "${seen[$p]:-}" ] || continue
      seen[$p]=1
      [[ "$p" == tests/*Tests.csproj ]] && return 0
      for q in ${rdeps[$p]:-}; do todo+=("$q"); done
    done
    return 1
  }

  # need_build PROJECT FILE [VIA]: a code change to FILE reaches PROJECT (through VIA, a project it references). A
  # selected test project's build compiles every project it references, so only PROJECT and its dependents
  # (transitively) that no test project reaches get a build line (#332): a tool or the stand-in game that uses a
  # changed Metaphor.Core.
  need_build() {
    local p="$1" f="$2" via="${3:+via $3, which it references; }" q
    [ -z "${build_seen[$p]:-}" ] || return 0
    build_seen[$p]=1
    reaches_test "$p" || untested+=("build $p $f changed (${via}no test project covers it)")
    for q in ${rdeps[$p]:-}; do need_build "$q" "$f" "$(basename "$p" .csproj)"; done
  }

  local code="" compile="" pester="" reads="" docs="" check="" varied="" every="" owner="" best f untested=()
  for f in "${files[@]}"; do
    case "$f" in
      Directory.Build.* | Directory.Packages.props | global.json | Metaphor.sln | .editorconfig) every="$f" ;;
      scripts/* | tests/pester/* | tests/Metaphor.StandInGame/* | installer/* | .github/workflows/*) pester="${pester:-$f}" ;;
    esac
    # What the Pester tests read besides: the projects they build (pester_built, below), the build-wide files,
    # onboard's tests the clone set-up page, and ghidra.ps1 tools/ghidra.
    case "$f" in
      Directory.Build.* | Directory.Packages.props | global.json | tools/ghidra/* | \
        docs/contributors/how-to/set-up-a-clone.md) reads="${reads:-$f}" ;;
    esac
    # Metaphor.Core.Tests reads the signature check's probe files (SignatureProbesTests), outside its references.
    [[ "$f" != tools/Metaphor.SignatureCheck/*.json ]] ||
      mark tests/Metaphor.Core.Tests/Metaphor.Core.Tests.csproj "$f changed (Metaphor.Core.Tests reads it)"
    case "$f" in
      docs/* | *.md) docs="${docs:-$f}" ;;
      *) code="${code:-$f}" ;;
    esac
    # Files whose change can change the build or the tests' result.
    [[ ! "$f" =~ (\.(cs|csproj|props|targets|sln|json|editorconfig)|(^|/)global\.json)$ ]] || compile="${compile:-$f}"
    # The check scripts, what they read and their tests: test-check.sh's own ground. The policy, release and slow
    # suites in tests/bash also read the licences, the agent rules, tools/slow, tests/fixtures and the download page;
    # test-what-runs.sh the real project graph, test-ci-matrix.sh the Pester test files. A deletion, which a
    # CODEOWNERS path can name (test-repo-config.sh), is the merge check's to add (merge.sh's merge_side).
    case "$f" in
      scripts/* | tests/check/* | tests/bash/* | .github/* | tools/docs/* | .claude/* | LICENSE | docs/LICENSE | \
        AGENTS.md | CLAUDE.md | tools/slow/* | tests/fixtures/* | docs/players/how-to/check-a-download.md | \
        *.csproj | tests/pester/*.Tests.ps1)
        check="${check:-$f}" ;;
    esac
    # What a player's or contributor's culture and paths can break: the installer and the setup scripts, with the
    # job's own tests and how it runs them.
    case "$f" in
      scripts/player/* | scripts/setup/* | scripts/release/* | installer/* | tests/pester/* | scripts/check/run-pester.sh | scripts/ci/install-pester.sh | \
        .github/workflows/ci.yml) varied="${varied:-$f}" ;;
    esac
    best="" owner=""
    for p in "${projects[@]}"; do
      if [[ "$f" == "${dir_of[$p]}/"* ]] && [ "${#dir_of[$p]}" -gt "${#best}" ]; then best="${dir_of[$p]}" owner="$p"; fi
    done
    [ -z "$owner" ] || mark "$owner" "$f changed"
    # The names table (#687) sits outside every project; the field tests check its schema, and the hook layer ships it.
    if [[ "$f" == data/names/* ]]; then
      mark tests/Metaphor.Features.Field.Tests/Metaphor.Features.Field.Tests.csproj "$f changed (the names table, which these tests check)"
    fi
    # So do the objective guidance's data files (#652).
    if [[ "$f" == data/objectives/* ]]; then
      mark tests/Metaphor.Features.Field.Tests/Metaphor.Features.Field.Tests.csproj "$f changed (the objective data, which these tests check)"
    fi
    # The settings reference is generated from the settings in code (#784, ADR 0022); these tests check it is current.
    if [[ "$f" == docs/players/reference/settings.md ]]; then
      mark tests/Metaphor.Settings.Tests/Metaphor.Settings.Tests.csproj "$f changed (generated, which these tests check)"
    fi
    [ -z "$owner" ] || [ -z "${pester_built[$owner]:-}" ] || reads="${reads:-$f}"
    # A code file: projects no test project reaches (a tool) run nothing, but the local build must still compile them.
    if [ -n "$owner" ] && [[ "$f" =~ \.(cs|csproj|props|targets|json)$ ]]; then need_build "$owner" "$f"; fi
  done
  if [ -n "$every" ]; then
    while read -r p; do mark "$p" "$every changed (build-wide)"; done < <(all_tests)
  fi

  [ -z "$code" ] || echo "code $code changed"
  [ -z "$compile" ] || echo "compile $compile changed"
  while read -r p; do
    [ -z "${hit[$p]:-}" ] || echo "dotnet $p ${why[$p]}"
  done < <(all_tests)
  [ "${#untested[@]}" -eq 0 ] || printf '%s\n' "${untested[@]}"
  [ -z "$every" ] || echo "solution $every changed (build-wide)"
  [ -z "$pester" ] || echo "pester $pester changed"
  [ -z "$reads" ] || echo "pester-reads $reads changed"
  [ -z "$docs" ] || echo "docs $docs changed"
  [ -z "$check" ] || echo "check-tests $check changed"
  [ -z "$varied" ] || echo "varied-setup $varied changed"
  return 0
}

if [ "${1:-}" = --job ]; then
  shift
  job "$@"
fi
select_runs
