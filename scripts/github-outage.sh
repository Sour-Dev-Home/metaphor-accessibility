#!/usr/bin/env bash
# GitHub outage plan for agents (issue #312): tell an outage from a real failure, park work that can't be pushed, and
# resume it once GitHub is back. The rules are in docs/agents/workflow.md ("GitHub outage").
#
#   github-outage.sh classify EXIT < output   prints "outage" (exit 0), "real" (exit 1) or "ok" (exit 3)
#   github-outage.sh status                   the public status API: prints its indicator; exit 0 when "none"
#   github-outage.sh park BRANCH ISSUE [--title T --body-file F]   record a branch whose push (and PR) is pending
#   github-outage.sh list                     the parked branches
#   github-outage.sh resume                   push every parked branch, open the PRs, enable auto-merge
#
# Parked work lives in <git common dir>/parked-pushes/<branch>/, never committed. The status API is public and gets
# no request data. Nothing here forces a push, skips a hook or touches a check.
set -uo pipefail

status_url="${METAPHOR_STATUS_URL:-https://www.githubstatus.com/api/v2/status.json}"

# store: sets STORE to the parked-work directory. No subshell beyond git itself (forks are slow under Git Bash).
store() {
  local common
  common="$(git rev-parse --path-format=absolute --git-common-dir)" || return 1
  STORE="$common/parked-pushes"
}

# Real failures win: an auth error, a rejected push, a failed hook or a failing check is never an outage.
classify() {
  local code="${1:-}" text
  text="$(cat)"
  [ "$code" != 0 ] || { echo ok; return 3; }
  if grep -qiE 'permission denied|authentication failed|could not read (username|password)|bad credentials|HTTP (401|403|404|422)|\[(remote )?rejected\]|non-fast-forward|hook declined|pre-push|protected branch|checks? (failed|failing)' <<< "$text"; then
    echo real
    return 1
  fi
  if grep -qiE 'returned error: 5[0-9]{2}|HTTP 5[0-9]{2}|internal server error|bad gateway|service unavailable|gateway time-?out|timed out|timeout|could not resolve host|hung up unexpectedly|failed to connect|error connecting to|connection (reset|refused)' <<< "$text" ||
    { [ "$code" = 124 ] && [ -z "$text" ]; }; then
    echo outage
    return 0
  fi
  echo real
  return 1
}

status() {
  local body ind
  body="$(curl -fsS -m 15 "$status_url" 2> /dev/null)" || { echo "unreachable"; return 2; }
  ind="$(sed -n 's/.*"indicator":"\([a-z]*\)".*/\1/p' <<< "$body")"
  echo "${ind:-unknown}"
  [ "$ind" = none ]
}

park() {
  local branch="${1:-}" issue="${2:-}" title="" bodyfile="" dir
  [ -n "$branch" ] && [ -n "$issue" ] || { echo "usage: park BRANCH ISSUE [--title T --body-file F]" >&2; return 2; }
  shift 2
  while [ $# -gt 0 ]; do
    case "$1" in
      --title) title="${2:-}"; shift 2 ;;
      --body-file) bodyfile="${2:-}"; shift 2 ;;
      *) echo "unknown option $1" >&2; return 2 ;;
    esac
  done
  git rev-parse --verify -q "refs/heads/$branch" > /dev/null || { echo "no local branch $branch" >&2; return 2; }
  store || return 2
  dir="$STORE/${branch//\//%2F}"
  mkdir -p "$dir" || return 2
  echo "$branch" > "$dir/branch"
  echo "$issue" > "$dir/issue"
  if [ -n "$title" ] && [ -f "$bodyfile" ]; then
    echo "$title" > "$dir/title"
    cp "$bodyfile" "$dir/body"
    printf '%s\n' push pr automerge > "$dir/pending"
  else
    printf '%s\n' push > "$dir/pending"
  fi
  echo "parked: GitHub outage: $branch (issue $issue), pending: $(tr '\n' ' ' < "$dir/pending")"
}

list() {
  local d s any=0 b i p
  store || return 1
  s="$STORE"
  for d in "$s"/*/; do
    [ -f "${d}branch" ] || continue
    any=1
    read -r b < "${d}branch"
    read -r i < "${d}issue"
    p="$(tr '\n' ' ' < "${d}pending")"
    echo "$b (issue $i): $p"
  done
  [ "$any" -eq 1 ] || echo "none parked"
}

# done_step DIR STEP: drop a finished step from the record.
done_step() { grep -vx "$2" "$1/pending" > "$1/pending.new"; mv "$1/pending.new" "$1/pending"; }
pending() { grep -qx "$2" "$1/pending"; }

resume() {
  local s d branch pr out code rc=0 any=0
  store || return 1
  s="$STORE"
  for d in "$s"/*/; do
    d="${d%/}"
    [ -f "$d/branch" ] || continue
    any=1
    branch="$(cat "$d/branch")"
    if pending "$d" push; then
      out="$(git push -u origin "$branch" 2>&1)"
      code=$?
      if [ "$code" -ne 0 ]; then
        echo "$branch: push failed (exit $code), still parked: $(classify "$code" <<< "$out")"
        rc=1
        continue
      fi
      done_step "$d" push
      echo "$branch: pushed"
    fi
    if pending "$d" pr; then
      pr="$(gh pr list --head "$branch" --state open --json number -q '.[0].number' 2> /dev/null)"
      if [ -z "$pr" ]; then
        out="$(gh pr create --head "$branch" --title "$(cat "$d/title")" --body-file "$d/body" 2>&1)"
        code=$?
        if [ "$code" -ne 0 ]; then
          echo "$branch: PR not opened (exit $code), still parked: $(classify "$code" <<< "$out")"
          rc=1
          continue
        fi
        pr="${out##*/}"
      fi
      echo "$pr" > "$d/pr"
      done_step "$d" pr
      echo "$branch: PR #$pr"
    fi
    if pending "$d" automerge; then
      pr="$(cat "$d/pr")"
      out="$(gh pr merge "$pr" --auto 2>&1)"
      code=$?
      if [ "$code" -ne 0 ]; then
        echo "$branch: auto-merge not enabled on #$pr (exit $code), still parked: $(classify "$code" <<< "$out")"
        rc=1
        continue
      fi
      done_step "$d" automerge
      echo "$branch: auto-merge enabled on #$pr"
    fi
    rm -rf "$d"
    echo "$branch: resumed"
  done
  [ "$any" -eq 1 ] && return "$rc"
  echo "nothing parked"
  return 0
}

# Sourced (by the tests), the functions load and nothing runs.
[ "${BASH_SOURCE[0]}" = "$0" ] || return 0
cmd="${1:-}"
[ $# -eq 0 ] || shift
case "$cmd" in
  classify) classify "$@" ;;
  status) status ;;
  park) park "$@" ;;
  list) list ;;
  resume) resume ;;
  *) echo "usage: github-outage.sh classify|status|park|list|resume" >&2; exit 2 ;;
esac
