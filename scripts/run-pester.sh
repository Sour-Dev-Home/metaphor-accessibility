#!/usr/bin/env bash
# Runs the smoke and deploy script tests (tests/smoke/*.Tests.ps1), one Pester process per file, a few at a time (#314).
# The files share nothing but the stand-in game's build, which this script builds first (each file's own `dotnet build`
# is then an up-to-date no-op). Each file's output is shown whole once it finishes, in file order, so a failure reads the
# same as a serial run. Exit 0 when every file passes, 1 when any fails, 2 when Pester can't start.
#
#   bash scripts/run-pester.sh [FILE...]    METAPHOR_TEST_JOBS=1 runs one file at a time (default: up to 4)
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2
command -v pwsh > /dev/null || { echo "run-pester: pwsh isn't on PATH" >&2; exit 2; }
files=("$@")
[ "${#files[@]}" -gt 0 ] || mapfile -t files < <(git ls-files 'tests/smoke/*.Tests.ps1')
[ "${#files[@]}" -gt 0 ] || { echo "run-pester: no test files" >&2; exit 2; }

jobs="${METAPHOR_TEST_JOBS:-}"
if ! [[ "$jobs" =~ ^[1-9][0-9]*$ ]]; then
  jobs="$(nproc 2> /dev/null || echo 2)"
  [ "$jobs" -le 4 ] || jobs=4
fi

tmp="$(mktemp -d)" || exit 2
trap 'rm -rf "$tmp"' EXIT

run_one() { # run_one INDEX FILE
  local start=$SECONDS
  FILE="$2" pwsh -NoProfile -Command \
    'Import-Module Pester -MinimumVersion 5.0; $r = Invoke-Pester $env:FILE -PassThru -Output Normal; exit $r.FailedCount' \
    > "$tmp/$1.out" 2>&1
  echo "$?" > "$tmp/$1.code"
  echo "$((SECONDS - start))" > "$tmp/$1.secs"
}

# Build the stand-in game once, so the files' own builds are up-to-date no-ops rather than racing on its obj folder.
if command -v dotnet > /dev/null; then
  dotnet build tests/Metaphor.StandInGame -c Release -v q --nologo > "$tmp/standin.out" 2>&1 ||
    { cat "$tmp/standin.out"; echo "run-pester: the stand-in game didn't build" >&2; exit 1; }
fi

for i in "${!files[@]}"; do
  while [ "$(jobs -rp | wc -l)" -ge "$jobs" ]; do wait -n; done
  run_one "$i" "${files[$i]}" &
done
wait

failed=0
for i in "${!files[@]}"; do
  echo "== ${files[$i]} ($(cat "$tmp/$i.secs") s)"
  cat "$tmp/$i.out"
  [ "$(cat "$tmp/$i.code")" = 0 ] || failed=$((failed + 1))
done
[ "$failed" -eq 0 ] || echo "run-pester: $failed of ${#files[@]} test files failed"
[ "$failed" -eq 0 ]
