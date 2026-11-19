#!/usr/bin/env bash
# Local test selection (issue #243). Reads changed file paths on stdin and prints what to run and why, one line each:
#   dotnet <test project .csproj>  <reason>
#   pester                         <reason>   (the smoke and deploy script tests)
#   docs                           <reason>   (the docs stage)
#   check-tests                    <reason>   (tests/check/test-check.sh; scripts/check/run-check-suites.sh --select)
#   build <project .csproj>        <reason>   (a changed project, or one depending on it, that no test project
#                                              reaches: build it, nothing to run; #332)
#   solution                       <reason>   (a build-wide file changed: build the whole solution)
# check.sh's pre-push builds only the projects these lines name (#314); a pass with no such line builds the solution.
# A test project is selected when the changed file sits in it, or in a project it references, directly or through
# other projects (the ProjectReference graph in the .csproj files). Build-wide files (Directory.*, global.json, the
# solution) select every test project.
#
# Selection is for local loops only. In CI (GITHUB_ACTIONS set) or with METAPHOR_FULL_TESTS=1 it selects everything, and
# `check.sh full`, which CI and the merge queue run, never calls it. Run from the repository root.
set -uo pipefail

mapfile -t files
[ "${#files[@]}" -gt 0 ] || exit 0

all_tests() { git ls-files -- 'tests/*.csproj' | grep -E 'Tests\.csproj$'; }

if [ "${GITHUB_ACTIONS:-}" = true ] || [ -n "${METAPHOR_FULL_TESTS:-}" ]; then
  why="CI and METAPHOR_FULL_TESTS run everything"
  while read -r p; do echo "dotnet $p $why"; done < <(all_tests)
  echo "pester $why"
  echo "docs $why"
  echo "check-tests $why"
  exit 0
fi

mapfile -t projects < <(git ls-files -- '*.csproj')
declare -A dir_of rdeps hit why
for p in "${projects[@]}"; do
  dir_of[$p]="$(dirname "$p")"
  while read -r r; do
    r="${r//\\//}"
    t="${dir_of[$p]}/$r"
    while [[ "$t" == *"/../"* ]]; do t="$(sed -E 's#[^/]+/\.\./##' <<< "$t")"; done # fold "a/../" away
    t="${t//\/.\//\/}"
    rdeps[$t]+="$p "
  done < <(grep -oE 'ProjectReference Include="[^"]+"' "$p" | sed -E 's/.*Include="([^"]+)"/\1/')
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
# selected test project's build compiles every project it references, so only PROJECT and its dependents (transitively)
# that no test project reaches get a build line (#332): a tool or the stand-in game that uses a changed Metaphor.Core.
declare -A build_seen
need_build() {
  local p="$1" f="$2" via="${3:+via $3, which it references; }" q
  [ -z "${build_seen[$p]:-}" ] || return 0
  build_seen[$p]=1
  reaches_test "$p" || untested+=("build $p $f changed (${via}no test project covers it)")
  for q in ${rdeps[$p]:-}; do need_build "$q" "$f" "$(basename "$p" .csproj)"; done
}

pester="" docs="" check="" every="" owner="" untested=()
for f in "${files[@]}"; do
  case "$f" in
    Directory.Build.* | Directory.Packages.props | global.json | Metaphor.sln | .editorconfig) every="$f" ;;
    scripts/* | tests/pester/* | tests/Metaphor.StandInGame/* | .github/workflows/*) pester="${pester:-$f}" ;;
  esac
  case "$f" in
    docs/* | *.md) docs="${docs:-$f}" ;;
  esac
  # The check scripts, what they read and their tests: test-check.sh's own ground.
  case "$f" in
    scripts/* | tests/check/* | tests/bash/* | .github/* | tools/docs/* | .claude/*) check="${check:-$f}" ;;
  esac
  best="" owner=""
  for p in "${projects[@]}"; do
    if [[ "$f" == "${dir_of[$p]}/"* ]] && [ "${#dir_of[$p]}" -gt "${#best}" ]; then best="${dir_of[$p]}" owner="$p"; fi
  done
  [ -z "$owner" ] || mark "$owner" "$f changed"
  # A code file: projects no test project reaches (a tool) run nothing, but the local build must still compile them.
  if [ -n "$owner" ] && [[ "$f" =~ \.(cs|csproj|props|targets|json)$ ]]; then need_build "$owner" "$f"; fi
done
if [ -n "$every" ]; then
  while read -r p; do mark "$p" "$every changed (build-wide)"; done < <(all_tests)
fi

while read -r p; do
  [ -z "${hit[$p]:-}" ] || echo "dotnet $p ${why[$p]}"
done < <(all_tests)
[ "${#untested[@]}" -eq 0 ] || printf '%s\n' "${untested[@]}"
[ -z "$every" ] || echo "solution $every changed (build-wide)"
[ -z "$pester" ] || echo "pester $pester changed"
[ -z "$docs" ] || echo "docs $docs changed"
[ -z "$check" ] || echo "check-tests $check changed"
exit 0
