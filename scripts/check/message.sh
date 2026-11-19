# shellcheck shell=bash
# check.sh's commit-message shape (ADR 0013): check_message, which the commit-msg and commits stages run. Sourced by
# scripts/check.sh only; it uses report from report.sh.
#
# Run globals: report updates `failures`.
# Private to this module: max_subject_chars, max_body_lines, scissors_re, trailer_re, char_count and
# every_line_a_trailer.

# The agreed shape's limits; docs/contributors/reference/commit-messages.md describes the shape.
max_subject_chars=72
max_body_lines=5

# Builtins throughout, where a sed, a wc and two greps were four starts per message (#328).
scissors_re='^# -* >8 -*$'
trailer_re='^[A-Za-z][A-Za-z0-9-]*: '

# char_count VAR TEXT: set VAR to TEXT's length in UTF-8 characters, not bytes.
char_count() {
  local LC_ALL=C.UTF-8
  printf -v "$1" '%s' "${#2}"
}

# every_line_a_trailer TEXT: exit 0 when every line of TEXT reads "Key: value".
every_line_a_trailer() {
  local line
  while IFS= read -r line; do [[ "$line" =~ $trailer_re ]] || return 1; done <<< "$1"
}

# check_message WHO: check the commit message on stdin against the agreed shape, reporting as WHO. Comment lines and
# everything below git's scissors line are dropped first, because the commit-msg hook sees the message before git does.
check_message() {
  local label="$1" message="" line subject body last length lines
  # Like git: drop CR, trailing spaces, comment lines and everything below the scissors line, then trailing and leading
  # blank lines.
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}" && line="${line%"${line##*[![:space:]]}"}"
    [[ "$line" =~ $scissors_re ]] && break
    [ "${line:0:1}" = "#" ] || message+="$line"$'\n'
  done
  message="${message%"${message##*[!$'\n']}"}" # trailing blank lines
  message="${message#"${message%%[!$'\n']*}"}" # leading blank lines
  if [ -z "$message" ]; then
    report "$label: message is empty"
    return
  fi
  subject="${message%%$'\n'*}"
  body=""
  [ "$message" = "$subject" ] || body="${message#*$'\n'}"

  # Git writes these subjects itself (git revert, git commit --fixup), so they pass whole; PRs are squash-merged, so
  # they never reach main.
  if ! [[ "$subject" =~ ^(Revert\ \"|fixup!\ |squash!\ |amend!\ ) ]]; then
    [[ "$subject" =~ ^(feat|fix|docs|test|refactor|perf|build|ci|chore|style|revert)(\([^\)]+\))?!?:\ [^[:space:]] ]] ||
      report "$label: subject needs a type prefix, such as \"feat: \" or \"fix(check): \""
    char_count length "$subject"
    [ "$length" -le "$max_subject_chars" ] ||
      report "$label: subject is $length characters; the limit is $max_subject_chars"
  fi

  [ -z "$body" ] && return
  [ -z "${body%%$'\n'*}" ] || report "$label: leave a blank line after the subject"
  # The last paragraph holds trailers (Co-Authored-By:, Signed-off-by:) when every line is "Key: value", as git reads
  # them. They don't count, even when they are the whole body.
  body="${body#$'\n'}"
  last="${body##*$'\n\n'}"
  if every_line_a_trailer "$last"; then
    [ "$last" = "$body" ] && body="" || body="${body%$'\n\n'*}"
  fi
  lines=0
  while IFS= read -r line; do [[ "$line" != *[^[:space:]]* ]] || lines=$((lines + 1)); done <<< "$body"
  [ "$lines" -le "$max_body_lines" ] || report "$label: body has $lines lines; the limit is $max_body_lines"
}
