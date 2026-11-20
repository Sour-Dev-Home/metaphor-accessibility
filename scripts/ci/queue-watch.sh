#!/usr/bin/env bash
# Flags open PRs the merge queue dropped, and PRs CI never started on (issue #504). For each open, non-draft PR whose
# branch is in this repo:
#
#   dropped: its required checks passed, it is mergeable, has no auto-merge and isn't queued, yet its timeline since the
#     last push shows auto-merge or a queue entry and then a removal from the queue (any reason but `merged`) or
#     auto-merge disabled for a reason other than a person turning it off. GitHub ends auto-merge with the removal and
#     says nothing (#474, #479). One comment per drop, keyed on the drop's time, naming the reason.
#   no CI run: no check run from a `pull_request` workflow run for the head commit, 10 minutes (NO_CI_AFTER seconds)
#     after the push that made it the head (#329). One comment per head commit, with the close-and-reopen fix.
#
# It comments and never re-enables auto-merge. Enabling it needs `contents: write` on top of what commenting needs,
# GitHub starts no workflow run for most events the job's token causes, and a PR the queue threw out for a failed run
# needs a person to read that run before it goes back in; re-queuing it unread would only fail again.
#
# Usage: queue-watch.sh [REPO]   (REPO defaults to $GITHUB_REPOSITORY)
# Needs `gh` signed in (GH_TOKEN) with contents: read, checks: read and pull-requests: write. Environment: DRY_RUN=1
# prints instead of commenting; NOW (epoch seconds) stands in for the clock in tests.
set -uo pipefail

repo="${1:-${GITHUB_REPOSITORY:-}}"
case "$repo" in
  */*) ;;
  *) echo "usage: queue-watch.sh [OWNER/REPO]" >&2; exit 2 ;;
esac
now="${NOW:-$(date +%s)}"
no_ci_after="${NO_CI_AFTER:-600}"
bot=github-actions # the login GitHub gives the job token's comments

# The PR's queue and auto-merge state, its head commit's checks and its timeline. Timeline items come oldest first.
query='query($owner: String!, $name: String!, $number: Int!) {
  repository(owner: $owner, name: $name) {
    pullRequest(number: $number) {
      isInMergeQueue
      autoMergeRequest { enabledAt }
      commits(last: 1) { nodes { commit { statusCheckRollup { contexts(first: 100) { nodes {
        __typename
        ... on CheckRun { isRequired(pullRequestNumber: $number) conclusion checkSuite { workflowRun { event } } }
        ... on StatusContext { isRequired(pullRequestNumber: $number) state }
      } } } } } }
      timelineItems(last: 50, itemTypes: [PULL_REQUEST_COMMIT, HEAD_REF_FORCE_PUSHED_EVENT, AUTO_MERGE_ENABLED_EVENT,
          AUTO_MERGE_DISABLED_EVENT, ADDED_TO_MERGE_QUEUE_EVENT, REMOVED_FROM_MERGE_QUEUE_EVENT]) { nodes {
        __typename
        ... on PullRequestCommit { commit { committedDate } }
        ... on HeadRefForcePushedEvent { createdAt }
        ... on AutoMergeEnabledEvent { createdAt }
        ... on AutoMergeDisabledEvent { createdAt reasonCode }
        ... on AddedToMergeQueueEvent { createdAt }
        ... on RemovedFromMergeQueueEvent { createdAt reason }
      } }
    }
  }
}'
# First line: in-queue, auto-merge, pull_request check runs, required checks (pass or wait). Then one line per timeline
# item: type, reason, time. "-" fills an empty field, since read merges adjacent tabs.
filter='.data.repository.pullRequest as $p
| ($p.commits.nodes[0].commit.statusCheckRollup.contexts.nodes // []) as $c
| [$c[] | select(.isRequired)] as $r
| ([($p.isInMergeQueue | tostring), ($p.autoMergeRequest != null | tostring),
    ([$c[] | select(.checkSuite.workflowRun.event? == "pull_request")] | length | tostring),
    (if ($r | length) > 0 and ($r | all(.conclusion == "SUCCESS" or .conclusion == "SKIPPED"
      or .conclusion == "NEUTRAL" or .state == "SUCCESS")) then "pass" else "wait" end)] | @tsv),
  ($p.timelineItems.nodes[] | [.__typename, (.reasonCode // .reason // "-"), (.createdAt // .commit.committedDate // "-")]
    | @tsv)'

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

# commented NUMBER KEY: whether this job already left the comment marked KEY. Fails if gh does.
commented() {
  local keys
  keys="$(gh pr view "$1" --repo "$repo" --json comments --jq ".comments[] | select(.author.login == \"$bot\")
    | .body | capture(\"<!-- queue-watch:(?<k>[^ ]+) -->\").k")" || return 2
  grep -qxF "$2" <<<"${keys//$'\r'/}"
}

# drop_of: reads timeline lines and prints "REASON<TAB>TIME" of a drop still standing, or nothing.
drop_of() {
  local type reason at armed=0 drop=""
  while IFS=$'\t' read -r type reason at; do
    at="${at%$'\r'}"
    case "$type" in
      PullRequestCommit | HeadRefForcePushedEvent) armed=0 drop="" ;;
      AutoMergeEnabledEvent | AddedToMergeQueueEvent) armed=1 drop="" ;;
      AutoMergeDisabledEvent)
        # A person's disable also comes just before every queue entry, which re-arms; one with no entry after it stands.
        [ "$armed" = 1 ] || continue
        if [ "$reason" = manually_disabled ]; then armed=0 drop=""; else drop="$reason"$'\t'"$at"; fi ;;
      RemovedFromMergeQueueEvent)
        [ "$armed" = 1 ] && [ "$reason" != merged ] && drop="$reason"$'\t'"$at" ;;
    esac
  done
  [ -z "$drop" ] || printf '%s\n' "$drop"
}

prs="$(gh pr list --repo "$repo" --state open --limit 200 \
  --json number,author,isDraft,isCrossRepository,headRefName,headRefOid,mergeable \
  --jq '.[] | select((.isDraft or .isCrossRepository) | not)
    | [.number, .author.login, .headRefName, .headRefOid, .mergeable] | @tsv')" \
  || { echo "queue-watch: could not list the open PRs" >&2; exit 1; }

failed=0
while IFS=$'\t' read -r number author branch head mergeable; do
  mergeable="${mergeable%$'\r'}"
  [ -n "$number" ] || continue
  detail="$(gh api graphql -f owner="${repo%%/*}" -f name="${repo#*/}" -F number="$number" -f query="$query" \
    --jq "$filter")" || { echo "#$number: could not read its state"; failed=1; continue; }
  detail="${detail//$'\r'/}"
  IFS=$'\t' read -r queued auto runs required <<<"${detail%%$'\n'*}"

  if [ "$runs" = 0 ]; then
    # When the head commit arrived: the branch's latest activity, if that put this commit there.
    pushed="$(gh api -X GET "repos/$repo/activity" -f ref="refs/heads/$branch" -f per_page=1 \
      --jq '.[0] | [.after, .timestamp] | @tsv')" || { echo "#$number: could not read its pushes"; failed=1; continue; }
    IFS=$'\t' read -r after at <<<"${pushed%$'\r'}"
    if [ "$after" != "$head" ] || ! at="$(date -d "$at" +%s 2>/dev/null)"; then
      echo "#$number: no CI run, push time unknown, left for the next run"; continue
    fi
    if [ $((now - at)) -lt "$no_ci_after" ]; then echo "#$number: no CI run yet"; continue; fi
    key="no-ci:$head"
    commented "$number" "$key"
    case $? in 0) echo "#$number: no CI run, already commented"; continue ;; 2) failed=1; continue ;; esac
    body="<!-- queue-watch:$key -->
@$author no CI run started for \`${head:0:12}\`, $((no_ci_after / 60)) minutes after the push (#329). Close and reopen the PR to start one: \`gh pr close $number && gh pr reopen $number\`, then turn auto-merge on again: \`gh pr merge $number --auto\`. This is the only comment for this commit."
    post "$number" "no CI run" "$body" || failed=1
    continue
  fi

  [ "$mergeable" = MERGEABLE ] && [ "$queued" = false ] && [ "$auto" = false ] && [ "$required" = pass ] || continue
  drop="$(drop_of <<<"$(tail -n +2 <<<"$detail")")"
  [ -n "$drop" ] || continue
  reason="${drop%%$'\t'*}" at="${drop#*$'\t'}"
  key="dropped:$at"
  commented "$number" "$key"
  case $? in 0) echo "#$number: dropped, already commented"; continue ;; 2) failed=1; continue ;; esac
  body="<!-- queue-watch:$key -->
@$author the merge queue dropped this PR at $at (reason: \`$reason\`), and auto-merge went with it, though its required checks pass. If a queue run failed, read it first (\`gh run list --event merge_group\`). Then turn auto-merge back on: \`gh pr merge $number --auto\`. CI only comments; it never re-queues a PR. This is the only comment for this drop."
  post "$number" dropped "$body" || failed=1
done <<<"$prs"
exit "$failed"
