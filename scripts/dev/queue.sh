#!/usr/bin/env bash
# The merge queue and the open PRs at a glance (issue #800): the queue's entries (position, PR, state), then one
# `pr-status.sh` line for each open PR. Read-only: `gh pr list`, one GraphQL query and what pr-status.sh reads.
#
# Usage: queue.sh
# Needs `gh` signed in with read access to the repository it runs in. Exit code 1 when anything could not be read.
set -uo pipefail

if [ "$#" -ne 0 ]; then
  echo "usage: queue.sh (no arguments)" >&2
  exit 2
fi
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

query='query($owner: String!, $name: String!) { repository(owner: $owner, name: $name) { mergeQueue { entries(first: 100) { nodes { position state pullRequest { number } } } } } }'
# gh 2.101.0 no longer fills {owner}/{repo} in GraphQL, so resolve them here, as pr-status.sh does (#786, #788).
repo="$(gh repo view --json owner,name --jq '.owner.login + "/" + .name')" ||
  { echo "could not resolve the repository" >&2; exit 1; }
repo="${repo//$'\r'/}"

failed=0
echo "merge queue:"
if entries="$(gh api graphql -f owner="${repo%%/*}" -f name="${repo#*/}" -f query="$query" \
  --jq '.data.repository.mergeQueue.entries.nodes // [] | sort_by(.position)[] | [.position, ("#" + (.pullRequest.number | tostring)), .state] | @tsv')"; then
  entries="${entries//$'\r'/}"
  if [ -n "$entries" ]; then
    while IFS=$'\t' read -r position pr state; do echo "  $position $pr $state"; done <<<"$entries"
  else
    echo "  empty"
  fi
else
  echo "  could not read the merge queue"
  failed=1
fi

echo "open PRs:"
if prs="$(gh pr list --state open --limit 100 --json number --jq '.[].number')"; then
  read -r -d '' -a numbers <<<"${prs//$'\r'/}" || true
  if [ "${#numbers[@]}" -eq 0 ]; then
    echo "  none"
  else
    bash "$here/pr-status.sh" "${numbers[@]}" || failed=1
  fi
else
  echo "  could not list the open PRs"
  failed=1
fi
exit "$failed"
