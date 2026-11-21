#!/usr/bin/env bash
# The flake rule's report (#655): after a CI run, .github/workflows/flake-report.yml downloads its flake-* artifacts
# (scripts/ci/retry-test.sh writes them) and this turns them into the comment it posts on #629.
#
#   bash scripts/ci/flake-report.sh comment DIR RUN_URL   print the comment for every flake.txt under DIR, or nothing
#                                                          when there is none
#
# The records come from the run's own steps, which may be a fork's code, so they are data: each line keeps only
# letters, digits, spaces and . _ - : / ( ) [ ] , = + ' and goes in code spans, at most 200 characters and 20 tests a
# job, so a record can't mention anyone, link anywhere or inject Markdown. The run link is the caller's, never the
# record's.
set -uo pipefail

clean() { # clean TEXT: the record's text as it may appear in the comment
  local text="${1//[^A-Za-z0-9 ._:\/()\[\],=+\'-]/}"
  printf '%s' "${text:0:200}"
}

comment() {
  local dir="$1" url="$2" file line key value job body="" tests
  [[ "$url" =~ ^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/actions/runs/[0-9]+(/attempts/[0-9]+)?$ ]] ||
    { echo "flake-report: not a run link: $url" >&2; return 2; }
  while IFS= read -r -d '' file; do
    job="" tests=0
    local lines=""
    while IFS= read -r line || [ -n "$line" ]; do
      key="${line%%: *}" value="$(clean "${line#*: }")"
      case "$key" in
        job) [ -n "$job" ] || job="$value" ;;
        test) [ "$tests" -ge 20 ] || { [ -z "$value" ] || lines+="  - \`$value\`"$'\n'; tests=$((tests + 1)); } ;;
      esac
    done < "$file"
    [ -n "$job" ] || continue
    body+="- Job \`$job\` failed once and passed on the retry."$'\n'
    [ -n "$lines" ] && body+="$lines" || body+="  - (no failing test named in its output)"$'\n'
  done < <(find "$dir" -name flake.txt -type f -print0 | sort -z)
  [ -n "$body" ] || return 0
  printf 'Flaky test passed on retry (the flake rule, #655), run %s:\n\n%s' "$url" "$body"
}

case "${1:-}" in
  comment) [ "$#" -eq 3 ] || { echo "usage: flake-report.sh comment DIR RUN_URL" >&2; exit 2; }; comment "$2" "$3" ;;
  *) echo "usage: flake-report.sh comment DIR RUN_URL" >&2; exit 2 ;;
esac
