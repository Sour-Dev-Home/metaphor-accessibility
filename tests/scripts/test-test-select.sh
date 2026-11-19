#!/usr/bin/env bash
# Tests for scripts/test-select.sh (issue #243): which suites a change selects, and that CI and `full` never narrow.
set -uo pipefail
unset GITHUB_ACTIONS METAPHOR_FULL_TESTS
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
cd "$ROOT" || exit 2
passed=0 failed=0
check() { local n="$1"; shift; if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $n"; fi; }
sel() { printf '%s\n' "$@" | bash scripts/test-select.sh; }
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
out="$(sel scripts/deploy-dev.ps1)"
check "deploy script runs Pester" has "pester"

out="$(sel docs/README.md)"
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

# Selection is local only
out="$(GITHUB_ACTIONS=true sel docs/README.md)"
check "CI selects every test project" has "Dialogue.Tests"
check "CI selects Pester" has "pester"
out="$(METAPHOR_FULL_TESTS=1 sel docs/README.md)"
check "METAPHOR_FULL_TESTS selects everything" has "Menus.Tests"
check "check.sh full never calls the selector" bash -c \
  "! sed -n '/^if \[ \"\$stage\" = full \]/,\$p' scripts/check.sh | grep -q test-select"

printf 'test selection tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
