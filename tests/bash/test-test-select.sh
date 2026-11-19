#!/usr/bin/env bash
# Tests for scripts/ci/test-select.sh (issue #243): which suites a change selects, and that CI and `full` never narrow.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
cd "$ROOT" || exit 2
passed=0 failed=0
check() { local n="$1"; shift; if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $n"; fi; }
sel() { printf '%s\n' "$@" | bash scripts/ci/test-select.sh; }
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

out="$(sel src/Metaphor.Features.Menus/Foo.cs)"
check "feature change runs its tests" has "dotnet tests/Metaphor.Features.Menus.Tests/"
check "feature change runs the architecture tests (they reference it)" has "dotnet tests/Metaphor.Architecture.Tests/"
check "feature change skips the other feature's tests" lacks "Dialogue.Tests"
check "feature change skips Core tests" lacks "Core.Tests"
check "feature change skips Pester" lacks "pester"
check "reason is printed" has "src/Metaphor.Features.Menus/Foo.cs changed (via Metaphor.Features.Menus"

out="$(sel src/Metaphor.Core/X.cs)"
check "core change reaches Core tests" has "Core.Tests"
check "core change reaches Dialogue tests" has "Dialogue.Tests"
check "core change reaches Menus tests" has "Menus.Tests"
check "dependent reason names the file and the reference" has "src/Metaphor.Core/X.cs changed (via Metaphor.Core, which it references)"

out="$(sel tests/Metaphor.Core.Tests/A.cs)"
check "a test project change runs only itself" lacks "Menus.Tests"
check "a test project change runs itself" has "Core.Tests"

out="$(sel scripts/check.sh)"
check "scripts change runs Pester" has "pester scripts/check.sh changed"
check "scripts change runs no dotnet tests" lacks "dotnet"
out="$(sel scripts/dev/deploy-dev.ps1)"
check "deploy script runs Pester" has "pester"

# The check scripts' own suite, which Git Bash without WSL runs only when they change (#336)
out="$(sel scripts/check.sh)"
check "a check script change runs test-check.sh" has "check-tests scripts/check.sh changed"
out="$(sel tests/check/test-check.sh)"
check "a check test change runs test-check.sh" has "check-tests tests/check/test-check.sh changed"
out="$(sel .github/workflows/ci.yml)"
check "a workflow change runs test-check.sh" has "check-tests"
out="$(sel src/Metaphor.Core/X.cs)"
check "a code change skips test-check.sh" lacks "check-tests"

out="$(sel docs/README.md)"
check "docs change skips test-check.sh" lacks "check-tests"
check "docs change selects the docs stage" has "docs docs/README.md changed"
check "docs change runs no tests" lacks "dotnet"
check "docs change skips Pester" lacks "pester"

out="$(sel Directory.Packages.props)"
check "build-wide file runs every test project" has "Dialogue.Tests"
check "build-wide file runs Core tests" has "Core.Tests"

# What the local build needs (#314)
out="$(sel tools/Metaphor.SessionLog/X.cs)"
check "a tool no test reaches is built, not tested" has "build tools/Metaphor.SessionLog/Metaphor.SessionLog.csproj"
check "a tool change runs no dotnet tests" lacks "dotnet"
out="$(sel src/Metaphor.Features.Menus/Foo.cs)"
check "a tested change names no extra build" lacks "build "
check "a tested change doesn't ask for the solution" lacks "solution"
out="$(sel docs/README.md)"
check "docs change builds nothing" lacks "build "
check "docs change doesn't ask for the solution" lacks "solution"
out="$(sel Directory.Packages.props)"
check "a build-wide file asks for the solution" has "solution Directory.Packages.props changed"

# Dependents no test reaches (#332): a Core change must still compile the tools and the stand-in game
out="$(sel src/Metaphor.Core/X.cs)"
check "a core change builds a tool that references it" has "build tools/Metaphor.SessionLog/Metaphor.SessionLog.csproj"
check "a core change builds the stand-in game" has "build tests/Metaphor.StandInGame/Metaphor.StandInGame.csproj"
check "a dependent's build names the change" has \
  "src/Metaphor.Core/X.cs changed (via Metaphor.Core, which it references; no test project covers it)"
out="$(sel src/Metaphor.Core/README.md)"
check "a docs file in a project builds nothing" lacks "build "
out="$(sel tests/Metaphor.Core.Tests/A.cs)"
check "a test-only change builds nothing extra" lacks "build "

# Every project that references a changed one, directly or not, is built by some selected line: read from the
# .csproj files here, so a new project can't slip past (#332).
refs_of() { # refs_of PROJECT: the projects its .csproj references, as repository paths
  grep -oE 'ProjectReference Include="[^"]+"' "$1" | sed -E 's/.*Include="([^"]+)"/\1/; s#\\#/#g' |
    while read -r r; do realpath -m --relative-to=. "$(dirname "$1")/$r"; done
}
mapfile -t all_projects < <(git ls-files -- '*.csproj')
declare -A refs
for p in "${all_projects[@]}"; do refs[$p]="$(refs_of "$p" | tr '\n' ' ')"; done
closure() { # closure PROJECT...: the projects and everything they reference, transitively, one per line
  local -A seen=()
  local todo=("$@") p q
  while [ "${#todo[@]}" -gt 0 ]; do
    p="${todo[0]}" todo=("${todo[@]:1}")
    [ -z "${seen[$p]:-}" ] || continue
    seen[$p]=1
    for q in ${refs[$p]:-}; do todo+=("$q"); done
  done
  printf '%s\n' "${!seen[@]}"
}
every_dependent_built() {
  local changed p q built missing=""
  for changed in "${all_projects[@]}"; do
    out="$(sel "$(dirname "$changed")/X.cs")"
    # shellcheck disable=SC2046 # one project per word
    built=" $(closure $(sed -nE 's/^(dotnet|build) ([^ ]+) .*/\2/p' <<< "$out") | tr '\n' ' ')"
    for p in "${all_projects[@]}"; do
      [[ " $(closure "$p" | tr '\n' ' ')" == *" $changed "* ]] || continue # p doesn't depend on the change
      [[ "$built" == *" $p "* ]] || missing+=" $p (changed: $changed)"
    done
  done
  [ -z "$missing" ] || echo "neither tested nor built:$missing"
  [ -z "$missing" ]
}
check "every project reachable from a changed one is tested or built" every_dependent_built

# A reference cycle (#321): reaches_test walks each project once, so it ends without nesting functions ever deeper.
cycle="$(mktemp -d)"
git init -q "$cycle"
mkdir -p "$cycle/src/A" "$cycle/src/B"
printf '<Project><ItemGroup><ProjectReference Include="..\\B\\B.csproj" /></ItemGroup></Project>\n' > "$cycle/src/A/A.csproj"
printf '<Project><ItemGroup><ProjectReference Include="..\\A\\A.csproj" /></ItemGroup></Project>\n' > "$cycle/src/B/B.csproj"
git -C "$cycle" add .
out="$(cd "$cycle" && printf 'src/A/X.cs\n' | FUNCNEST=50 timeout 60 bash "$ROOT/scripts/ci/test-select.sh" 2>&1)"
check "a reference cycle ends" test $? -eq 0
check "a reference cycle nests no function deeper than the graph" lacks "nesting level"
check "a reference cycle builds both projects" has "build src/B/B.csproj"
rm -rf "$cycle"

# Selection is local only
out="$(GITHUB_ACTIONS=true sel docs/README.md)"
check "CI selects every test project" has "Dialogue.Tests"
check "CI selects Pester" has "pester"
check "CI selects test-check.sh" has "check-tests"
out="$(METAPHOR_FULL_TESTS=1 sel docs/README.md)"
check "METAPHOR_FULL_TESTS selects everything" has "Menus.Tests"
check "check.sh full never calls the selector" bash -c \
  "! sed -n '/^if \[ \"\$stage\" = full \]/,\$p' scripts/check.sh | grep -q test-select"

printf 'test selection tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
