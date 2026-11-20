#!/usr/bin/env bash
# Flags open PRs that have fallen behind main, so none waits unnoticed. Two passes, run in this order:
#
#   alert (issue #170): comments once on every open PR (draft or not) that GitHub's mergeable flag says conflicts with
#     its base, so a conflicted PR under auto-merge doesn't sit there. A PR is skipped while its latest
#     `<!-- conflict-alert -->` comment is newer than its latest commit, so it stays quiet until someone pushes and the
#     PR conflicts again. GitHub computes mergeability lazily after main moves, so UNKNOWN is re-read a few times; any
#     PR still UNKNOWN is left for the next run (the scheduled backstop).
#   sync (issue #242): for each open draft PR (an integration branch) whose head lacks main's tip, test-merges main in
#     memory (git merge-tree: nothing is checked out and no PR code runs) and comments with either the conflicting
#     files or "mergeable cleanly, needs a merge from main" (naming hook-layer files main changed). The comment's
#     hidden `<!-- integration-sync:KEY -->` marker hashes the outcome, so an unchanged situation isn't repeated.
#
# CI never pushes a merge: one bringing in hook-layer code (the scope in scripts/ci/security-stamp.sh) needs a fresh
# security-review stamp, and the stamp can't come from CI.
#
# Usage: conflict-alert.sh [--alert-only | --sync-only] [REPO]   (REPO defaults to $GITHUB_REPOSITORY)
# With no flag both passes run, and the sync pass only if the alert pass succeeded. The sync pass runs from a clone
# whose `origin` is the repo, with full history. Needs `gh` signed in (GH_TOKEN) with pull-requests: write.
# Environment: DRY_RUN=1 prints instead of commenting; RETRIES (default 5) and RETRY_DELAY seconds (default 10) bound
# the UNKNOWN re-reads; BASE_BRANCH (default main) is the branch the sync pass merges.
set -uo pipefail

usage() { echo "usage: conflict-alert.sh [--alert-only | --sync-only] [REPO]" >&2; exit 2; }

passes=all
case "${1:-}" in
  --alert-only) passes=alert; shift ;;
  --sync-only) passes=sync; shift ;;
  -*) usage ;;
esac
repo="${1:-${GITHUB_REPOSITORY:-}}"
[ -n "$repo" ] || usage
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# post NUMBER OUTCOME BODY: comments, or prints under DRY_RUN; fails if gh does.
post() {
  if [ "${DRY_RUN:-}" = 1 ]; then
    echo "#$1: $2, would comment (dry run)"
  elif gh pr comment "$1" --repo "$repo" --body "$3" > /dev/null; then
    echo "#$1: $2, commented"
  else
    return 1
  fi
}

# list_mergeable: one "number<TAB>author<TAB>mergeable" line per open PR. gh lists drafts too.
list_mergeable() {
  gh pr list --repo "$repo" --state open --limit 200 --json number,author,mergeable \
    --jq '.[] | [.number, .author.login, .mergeable] | @tsv'
}

alert_pass() {
  local retries="${RETRIES:-5}" delay="${RETRY_DELAY:-10}" marker='<!-- conflict-alert -->'
  local prs attempt=0 failed=0 number author state alerted pushed body
  prs="$(list_mergeable)" || { echo "conflict-alert: could not list the open PRs" >&2; return 1; }
  while grep -qE $'\tUNKNOWN\r?$' <<<"$prs" && [ "$attempt" -lt "$retries" ]; do
    attempt=$((attempt + 1))
    sleep "$delay"
    prs="$(list_mergeable)" || { echo "conflict-alert: could not list the open PRs" >&2; return 1; }
  done

  while IFS=$'\t' read -r number author state; do
    state="${state%$'\r'}"
    [ -n "$number" ] || continue
    case "$state" in
      CONFLICTING) ;;
      UNKNOWN) echo "#$number: mergeable state still unknown, left for the next run"; continue ;;
      *) continue ;;
    esac

    alerted="$(gh pr view "$number" --repo "$repo" --json comments \
      --jq "[.comments[] | select(.body | contains(\"$marker\")) | .createdAt] | max // \"\"")" || { failed=1; continue; }
    pushed="$(gh pr view "$number" --repo "$repo" --json commits --jq '.commits[-1].committedDate')" || { failed=1; continue; }
    alerted="${alerted%$'\r'}" pushed="${pushed%$'\r'}"
    if [ -n "$alerted" ] && [[ "$alerted" > "$pushed" ]]; then
      echo "#$number: conflicts, already commented"
      continue
    fi

    body="$marker
@$author this PR conflicts with \`main\`, so auto-merge can't land it. Merge \`main\` into the branch and resolve the conflicts. This is the only comment until the branch is pushed to and conflicts again."
    post "$number" conflicts "$body" || failed=1
  done <<<"$prs"
  return "$failed"
}

sync_pass() {
  local base="${BASE_BRANCH:-main}" scope tip prs failed=0
  local number author branch head merged code hookfiles files outcome state key last text body
  # The security-review scope, read from the stamp script so the two never drift.
  read -ra scope <<<"$(sed -n '/^scope=(/,/)$/p' "$here/security-stamp.sh" | tr '\n' ' ' | sed 's/^scope=(//; s/).*$//')"
  [ "${#scope[@]}" -gt 0 ] || { echo "conflict-alert: could not read the scope from security-stamp.sh" >&2; return 1; }

  git fetch --quiet origin "$base" || { echo "conflict-alert: could not fetch $base" >&2; return 1; }
  tip="$(git rev-parse "origin/$base")" || return 1

  prs="$(gh pr list --repo "$repo" --state open --draft --limit 200 \
    --json number,author,headRefName,isCrossRepository \
    --jq '.[] | select(.isCrossRepository | not) | [.number, .author.login, .headRefName] | @tsv')" \
    || { echo "conflict-alert: could not list the open draft PRs" >&2; return 1; }

  while IFS=$'\t' read -r number author branch; do
    branch="${branch%$'\r'}"
    [ -n "$number" ] || continue
    if ! git fetch --quiet origin "$branch"; then
      echo "#$number: could not fetch $branch"; failed=1; continue
    fi
    head="$(git rev-parse FETCH_HEAD)"
    if git merge-base --is-ancestor "$tip" "$head"; then
      echo "#$number: already has $base"; continue
    fi

    merged="$(git merge-tree --write-tree --name-only "$tip" "$head" 2>/dev/null)"
    code=$?
    # In any case, as Windows reads paths: a Hooks/ that Windows merges into hooks/ needs the stamp too (#436).
    hookfiles="$(GIT_ICASE_PATHSPECS=1 git diff --name-only "$(git merge-base "$tip" "$head")" "$tip" -- "${scope[@]}" | tr '\n' ' ')"
    hookfiles="${hookfiles% }"
    case "$code" in
      0) files=""; outcome=clean ;;
      1) files="$(sed -n '2,/^$/p' <<<"$merged" | sed '/^$/d' | sort -u | tr '\n' ' ')"; files="${files% }"; outcome=conflict ;;
      *) echo "#$number: could not test the merge"; failed=1; continue ;;
    esac

    state="$outcome|${files// /,}|${hookfiles:+hook}"
    key="$(printf '%s' "$state" | sha256sum | cut -c1-16)"
    last="$(gh pr view "$number" --repo "$repo" --json comments \
      --jq '[.comments[].body | capture("<!-- integration-sync:(?<k>[0-9a-f]+) -->").k] | last // ""')" \
      || { failed=1; continue; }
    if [ "${last%$'\r'}" = "$key" ]; then
      echo "#$number: $outcome, already commented"; continue
    fi

    if [ "$outcome" = conflict ]; then
      text="This branch conflicts with \`$base\` in: ${files// /, }. Merge \`$base\` in and resolve them."
    else
      text="Mergeable cleanly, needs a merge from \`$base\` (and a review if hook files change)."
      [ -z "$hookfiles" ] || text="$text Hook-layer files changed on \`$base\`: ${hookfiles// /, }, so the merge needs a fresh security-review stamp."
    fi
    body="<!-- integration-sync:$key -->
@$author $text CI never pushes this merge. This is the only comment until the outcome changes."
    post "$number" "$outcome" "$body" || failed=1
  done <<<"$prs"
  return "$failed"
}

case "$passes" in
  alert) alert_pass ;;
  sync) sync_pass ;;
  all) alert_pass && sync_pass ;;
esac
exit $?
