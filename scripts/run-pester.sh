#!/usr/bin/env bash
# Runs the smoke and deploy script tests (tests/smoke/*.Tests.ps1), every test of every file, in parallel (#314, #348).
#
# - The stand-in game (tests/Metaphor.StandInGame) is built once, here; METAPHOR_STANDIN_BUILT=1 then tells each file's
#   BeforeAll to skip its own build, so no process starts MSBuild or races on the game's obj folder.
# - Each file is split across Pester processes, about METAPHOR_PESTER_BATCH tests each (default 4; Pester's line
#   filter on the `It` lines), since a file's tests take seconds each and its file-level BeforeAll only defines helpers.
#   A process costs about 2 s to start, so one per test is slower. A file whose Describe has its own BeforeAll runs
#   whole in one process: its tests share that setup, which would otherwise run once per process.
# - Longest first: files start in the order of their slowest process in the last run, recorded in the clone's git dir
#   (pester-times, "FILE SECONDS" lines); a file with no record starts first, as it may be slow.
# - Each process gets its own temporary folder (TEMP and TMP, so Pester's TestDrive and the scripts' own temporary
#   folders), removed at the end. Tests already keep their games, configs and mod projects in their own folders, and
#   find processes by full path, so nothing else is shared.
# - In Git Bash, starting a process takes about 0.3 s and the starts don't overlap (#348), so the loop below starts
#   nothing but the Pester processes themselves: one fork each, and builtins for the rest. They run in check.sh's
#   worker pool (scripts/check/pool.sh), which keeps each process's output in its own file.
#
# A "[done/total] FILE ok|FAIL (seconds s)" line is printed, and written to the clone's live log (scripts/live-log.sh),
# as each file's last process finishes. Then a summary line per file, with a failing file's whole output. A file fails
# when a test fails, or when its processes ran fewer tests than it has. Exit 0 when every file passes, 1 when any
# fails, 2 when Pester can't start.
#
#   bash scripts/run-pester.sh [FILE...]    METAPHOR_TEST_JOBS=N runs N processes at once (default: pool_default_jobs)
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.." || exit 2
command -v pwsh > /dev/null || { echo "run-pester: pwsh isn't on PATH" >&2; exit 2; }
files=("$@")
[ "${#files[@]}" -gt 0 ] || mapfile -t files < <(git ls-files 'tests/smoke/*.Tests.ps1')
[ "${#files[@]}" -gt 0 ] || { echo "run-pester: no test files" >&2; exit 2; }

# shellcheck source=check/pool.sh
source scripts/check/pool.sh || exit 2
jobs="$(pool_default_jobs)"
batch="${METAPHOR_PESTER_BATCH:-}"
[[ "$batch" =~ ^[1-9][0-9]*$ ]] || batch=4

tmp="$(mktemp -d)" || exit 2
trap 'rm -rf "$tmp"' EXIT
# The folder as pwsh will see it (a Windows path in Git Bash).
wtmp="$tmp" sep=/
! command -v cygpath > /dev/null || { wtmp="$(cygpath -w "$tmp")" && sep='\'; } || exit 2

if command -v dotnet > /dev/null; then
  dotnet build tests/Metaphor.StandInGame -c Release -v q --nologo > "$tmp/standin.out" 2>&1 ||
    { cat "$tmp/standin.out"; echo "run-pester: the stand-in game didn't build" >&2; exit 1; }
  export METAPHOR_STANDIN_BUILT=1
fi

# The units to run: unit_file (an index into files) and unit_lines (the `It` lines to run, empty for the whole file).
# A file's tests are dealt out in turn to ceil(tests / batch) units, so neighbouring slow tests land in different ones.
unit_file=() unit_lines=() file_left=() file_units=()
shopt -s nocasematch # PowerShell's keywords: It, it, BeforeAll, beforeall
for f in "${!files[@]}"; do
  its=() shared='' number=0
  [ -r "${files[f]}" ] || { echo "run-pester: can't read ${files[f]}" >&2; exit 2; }
  while IFS= read -r line || [ -n "$line" ]; do
    number=$((number + 1))
    if [[ "$line" =~ ^[[:space:]]+BeforeAll([^[:alnum:]_-]|$) ]]; then shared=1; fi
    if [[ "$line" =~ ^[[:space:]]*It[[:space:]] ]]; then its+=("$number"); fi
  done < "${files[f]}"
  [ -z "$shared" ] || its=()
  n=$(((${#its[@]} + batch - 1) / batch))
  [ "$n" -gt 1 ] || { n=1 its=(); }
  file_units[f]="$n" file_left[f]="$n"
  first="${#unit_file[@]}"
  for ((u = 0; u < n; u++)); do unit_file+=("$f") unit_lines+=(""); done
  for t in "${!its[@]}"; do
    u=$((first + t % n))
    unit_lines[u]="${unit_lines[u]:+${unit_lines[u]} }${its[t]}"
  done
done
shopt -u nocasematch
temps=()
for i in "${!unit_file[@]}"; do temps+=("$tmp/$i.temp"); done
mkdir "${temps[@]}" || exit 2

times="$(git rev-parse --absolute-git-dir 2> /dev/null)" && times="$times/pester-times" || times=/dev/null
declare -A last=()
[ ! -f "$times" ] || while read -r name secs; do
  [[ "$secs" =~ ^[0-9]+$ ]] && last["$name"]="$secs"
done < "$times"
mapfile -t order < <(for i in "${!unit_file[@]}"; do
  echo "${last[${files[${unit_file[i]}]}]:-999999} $i"
done | sort -k 1,1nr -k 2,2n | cut -d ' ' -f 2)

# Each process prints "pester-counts PASSED FAILED RAN TOTAL" last; RAN counts the tests it ran, TOTAL the file's.
# shellcheck disable=SC2016 # PowerShell, expanded by pwsh
pester='
  Import-Module Pester -MinimumVersion 5.0
  $c = New-PesterConfiguration
  $c.Run.Path = $env:FILE; $c.Run.PassThru = $true; $c.Output.Verbosity = "Normal"
  if ($env:LINES) { $p = (Resolve-Path $env:FILE).Path; $c.Filter.Line = @(-split $env:LINES | ForEach-Object { "${p}:$_" }) }
  $r = Invoke-Pester -Configuration $c
  $failed = $r.FailedCount + $r.FailedBlocksCount + $r.FailedContainersCount
  "pester-counts $($r.PassedCount) $failed $($r.TotalCount - $r.NotRunCount) $($r.TotalCount)"
  if ($r.Result -ne "Passed") { exit 1 }'

# shellcheck source=live-log.sh
source scripts/live-log.sh || exit 2
live_start run-pester "${#files[@]}"
start=$SECONDS
unit_secs=() file_code=() file_secs=() file_summary=()

# finished UNIT CODE SECONDS (the pool's callback): note a unit's result; when it was its file's last, check the file
# and print its line. A unit that crashed or was killed has a non-zero code, and one that printed no counts ran none of
# the file's tests, so either fails the file.
finished() {
  local i="$1" f j line p n r t pass=0 fail=0 ran=0 total=0 slowest=0
  f="${unit_file[i]}"
  unit_secs[i]="${3%.*}"
  [ "$2" = 0 ] || file_code[f]=1
  file_left[f]=$((file_left[f] - 1))
  [ "${file_left[f]}" -eq 0 ] || return 0
  file_secs[f]=$((SECONDS - start))
  for j in "${!unit_file[@]}"; do
    [ "${unit_file[j]}" = "$f" ] || continue
    [ "${unit_secs[j]}" -le "$slowest" ] || slowest="${unit_secs[j]}"
    while IFS= read -r line; do
      line="${line%$'\r'}"
      [[ "$line" =~ ^pester-counts\ ([0-9]+)\ ([0-9]+)\ ([0-9]+)\ ([0-9]+)$ ]] || continue
      p="${BASH_REMATCH[1]}" n="${BASH_REMATCH[2]}" r="${BASH_REMATCH[3]}" t="${BASH_REMATCH[4]}"
      pass=$((pass + p)) fail=$((fail + n)) ran=$((ran + r)) total="$t"
    done < "${pool_file[$j]}"
  done
  last["${files[f]}"]="$slowest"
  file_summary[f]="$pass passed, $fail failed, ${file_units[f]} processes, the slowest $slowest s"
  # Split across processes, a file must still run each of its tests once: an `It` the split missed would never run.
  if [ "$ran" -ne "$total" ] || [ "$total" -eq 0 ]; then
    file_code[f]=1 file_summary[f]="${file_summary[f]}; ran $ran of its $total tests"
  fi
  live_line "${files[f]}" "${file_code[f]:-0}" "${file_secs[f]}"
}

run_unit() { # run_unit UNIT: the unit's Pester process, in place of the pool's job subshell (exec: no second fork)
  local temp="$wtmp$sep$1.temp"
  TEMP="$temp" TMP="$temp" FILE="${files[${unit_file[$1]}]}" LINES="${unit_lines[$1]}" \
    exec pwsh -NoProfile -Command "$pester"
}

# The outputs go to the same folder as the units' temporary folders: N.out by start number, beside UNIT.temp.
pool_init "$tmp" "$jobs" finished || exit 2
for i in "${order[@]}"; do pool_run "$i" run_unit "$i"; done
pool_wait

failed=0
for f in "${!files[@]}"; do
  result=ok
  [ -z "${file_code[f]:-}" ] || { result=FAIL; failed=$((failed + 1)); }
  echo "== ${files[f]} $result: ${file_summary[f]} (${file_secs[f]} s)"
  [ "$result" = ok ] && continue
  for i in "${!unit_file[@]}"; do
    [ "${unit_file[i]}" != "$f" ] || cat "${pool_file[$i]}"
  done
done
[ "$times" = /dev/null ] || { for name in "${!last[@]}"; do echo "$name ${last[$name]}"; done | sort > "$tmp/times" &&
  mv "$tmp/times" "$times"; }
echo "run-pester: $((${#files[@]} - failed)) files passed, $failed failed"
live_end "$((failed > 0))"
[ "$failed" -eq 0 ]
