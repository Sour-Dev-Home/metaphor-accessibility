#!/usr/bin/env bash
# Local test selection (issue #243). Reads changed file paths on stdin and prints what to run and why, one line each:
#   dotnet <test project .csproj>  <reason>
#   pester                         <reason>   (the smoke and deploy script tests)
#   docs                           <reason>   (the docs stage)
#   build <project .csproj>        <reason>   (a changed project no test project reaches: build it, nothing to run)
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
  local p="$1" q
  [[ "$p" == tests/*Tests.csproj ]] && return 0
  for q in ${rdeps[$p]:-}; do reaches_test "$q" && return 0; done
  return 1
}

pester="" docs="" every="" owner="" untested=()
for f in "${files[@]}"; do
  case "$f" in
    Directory.Build.* | Directory.Packages.props | global.json | Metaphor.sln | .editorconfig) every="$f" ;;
    scripts/* | tests/smoke/* | tests/Metaphor.StandInGame/* | .github/workflows/*) pester="${pester:-$f}" ;;
  esac
  case "$f" in
    docs/* | *.md) docs="${docs:-$f}" ;;
  esac
  best="" owner=""
  for p in "${projects[@]}"; do
    if [[ "$f" == "${dir_of[$p]}/"* ]] && [ "${#dir_of[$p]}" -gt "${#best}" ]; then best="${dir_of[$p]}" owner="$p"; fi
  done
  [ -z "$owner" ] || mark "$owner" "$f changed"
  # A code file in a project no test project reaches (a tool): nothing runs, but the local build must still compile it.
  if [ -n "$owner" ] && [[ "$f" =~ \.(cs|csproj|props|targets|json)$ ]] && ! reaches_test "$owner"; then
    untested+=("build $owner $f changed (no test project covers it)")
  fi
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
exit 0
