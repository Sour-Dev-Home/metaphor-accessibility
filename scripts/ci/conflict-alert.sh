#!/usr/bin/env bash
# Comments once on every open PR (draft or not) that conflicts with its base branch, so a conflicted PR under auto-merge
# doesn't wait unnoticed (issue #170). Usage: conflict-alert.sh [REPO]   (REPO defaults to $GITHUB_REPOSITORY)
#
# A PR gets one comment per conflict. The comment carries a hidden marker; a PR is skipped while its latest marker
# comment is newer than its latest commit, so it stays quiet until someone pushes and the PR conflicts again.
# GitHub computes mergeability lazily after main moves, so UNKNOWN is re-read a few times; any PR still UNKNOWN is
# left for the next run (the scheduled backstop).
#
# Needs `gh` signed in (GH_TOKEN) with pull-requests: write. Environment: DRY_RUN=1 prints instead of commenting;
# RETRIES (default 5) and RETRY_DELAY seconds (default 10) bound the UNKNOWN re-reads.
set -uo pipefail

repo="${1:-${GITHUB_REPOSITORY:-}}"
[ -n "$repo" ] || { echo "usage: conflict-alert.sh [REPO]" >&2; exit 2; }
retries="${RETRIES:-5}"
delay="${RETRY_DELAY:-10}"
marker='<!-- conflict-alert -->'

# list_prs: one "number<TAB>author<TAB>mergeable" line per open PR. gh lists drafts too.
list_prs() {
  gh pr list --repo "$repo" --state open --limit 200 --json number,author,mergeable \
    --jq '.[] | [.number, .author.login, .mergeable] | @tsv'
}

prs="$(list_prs)" || { echo "conflict-alert: could not list the open PRs" >&2; exit 1; }
attempt=0
while grep -qE $'\tUNKNOWN\r?$' <<<"$prs" && [ "$attempt" -lt "$retries" ]; do
  attempt=$((attempt + 1))
  sleep "$delay"
  prs="$(list_prs)" || { echo "conflict-alert: could not list the open PRs" >&2; exit 1; }
done

failed=0
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
  if [ "${DRY_RUN:-}" = 1 ]; then
    echo "#$number: conflicts, would comment (dry run)"
  elif gh pr comment "$number" --repo "$repo" --body "$body" > /dev/null; then
    echo "#$number: conflicts, commented"
  else
    failed=1
  fi
done <<<"$prs"

exit "$failed"
