#!/usr/bin/env bash
# One line per PR (issue #759): mergeable, merge state, auto-merge, merge-queue position and failing checks. Read-only:
# `gh pr view` and one GraphQL query, no writes.
#
# Usage: pr-status.sh PR_NUMBER...
# Needs `gh` signed in with read access to the repository it runs in. A PR it can't read gets a line saying so, the
# rest still print, and the exit code is 1.
set -uo pipefail

if [ "$#" -eq 0 ]; then
  echo "usage: pr-status.sh PR_NUMBER..." >&2
  exit 2
fi
for n in "$@"; do
  case "$n" in '' | *[!0-9]*) echo "usage: pr-status.sh PR_NUMBER... (numbers only)" >&2; exit 2 ;; esac
done

# A failing check is a completed run that didn't succeed, skip, or stay neutral; a status context that errored or failed.
view_filter='[.mergeable, .mergeStateStatus, (if .autoMergeRequest then "true" else "false" end),
  ([.statusCheckRollup[]? | select((.conclusion // "") as $c | ($c == "FAILURE" or $c == "TIMED_OUT" or $c == "CANCELLED"
    or $c == "STARTUP_FAILURE" or $c == "ACTION_REQUIRED") or (.state // "") == "FAILURE" or (.state // "") == "ERROR")
    | (.name // .context)] | join(","))] | @tsv'
query='query($owner: String!, $name: String!, $number: Int!) { repository(owner: $owner, name: $name) { pullRequest(number: $number) { mergeQueueEntry { position } } } }'
# gh 2.101.0 no longer fills {owner}/{repo} in GraphQL, so resolve them here (#786).
repo="$(gh repo view --json owner,name --jq '.owner.login + "/" + .name')" ||
  { echo "could not resolve the repository" >&2; exit 1; }
repo="${repo//$'\r'/}"

failed=0
for n in "$@"; do
  view="$(gh pr view "$n" --json mergeable,mergeStateStatus,autoMergeRequest,statusCheckRollup --jq "$view_filter")" &&
    queue="$(gh api graphql -F number="$n" -f owner="${repo%%/*}" -f name="${repo#*/}" -f query="$query" \
      --jq '.data.repository.pullRequest.mergeQueueEntry.position // "none"')" ||
    { echo "#$n could not read its state"; failed=1; continue; }
  IFS=$'\t' read -r mergeable state auto failing <<<"${view//$'\r'/}"
  [ "$auto" = true ] && auto=on || auto=off
  echo "#$n mergeable=$mergeable state=$state auto=$auto queue=${queue//$'\r'/} failing=${failing:-none}"
done
exit "$failed"
