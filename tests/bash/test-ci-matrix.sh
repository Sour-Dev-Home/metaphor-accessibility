#!/usr/bin/env bash
# CI's `pester` job runs one matrix entry per tests/pester/*.Tests.ps1 file (#314). A test file missing from the matrix
# would silently stop running in CI, so the two lists must agree. Reads the repository, writes nothing.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
cd "$ROOT" || exit 2

matrix="$(sed -nE 's/^[[:space:]]*file: \[(.*)\]$/\1/p' .github/workflows/ci.yml | tr -d ' ' | tr ',' '\n' | sort)"
files="$(git ls-files 'tests/pester/*.Tests.ps1' | sed -E 's#.*/(.*)\.Tests\.ps1#\1#' | sort)"

failed=0
if [ -z "$matrix" ] || [ -z "$files" ]; then
  echo "FAIL could not read the matrix or the test files"
  failed=1
elif [ "$matrix" != "$files" ]; then
  echo "FAIL the pester matrix in ci.yml and tests/pester disagree:"
  diff <(echo "$matrix") <(echo "$files") | sed 's/^/  /'
  failed=1
fi
if ! grep -qE '^  scripts:$' .github/workflows/ci.yml; then
  echo "FAIL ci.yml lost the job named scripts, a required check"
  failed=1
fi
[ "$failed" -eq 0 ] && echo "ci matrix tests: 2 passed, 0 failed"
exit "$failed"
