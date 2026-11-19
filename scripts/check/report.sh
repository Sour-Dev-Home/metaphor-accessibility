# shellcheck shell=bash
# check.sh's reporting (ADR 0013): report, warn and die, the failure and warning counts, and the exit code on a usage
# error. Sourced by scripts/check.sh only.
#
# Run globals: updates `failures` and `warnings`, which check.sh's main resets each run. Reads GITHUB_ACTIONS.

die() {
  echo "check: $*" >&2
  exit 2
}

failures=0
warnings=0

report() {
  echo "$1"
  failures=$((failures + 1))
}

# warn MESSAGE [FILE [LINE]]: print a warning that doesn't fail the run. In GitHub Actions it also becomes an annotation
# on the file, so it shows on the pull request.
warn() {
  local message="${1//\\//}" file="${2:-}" # Windows tools print backslashes
  file="${file//\\//}"
  echo "warning: $message"
  warnings=$((warnings + 1))
  if [ "${GITHUB_ACTIONS:-}" = true ]; then
    # Workflow commands need % escaped everywhere, and , and : in properties too.
    message="${message//%/%25}"
    file="${file//%/%25}" && file="${file//,/%2C}" && file="${file//:/%3A}"
    echo "::warning${file:+ file=$file}${3:+,line=$3}::$message"
  fi
  return 0
}
