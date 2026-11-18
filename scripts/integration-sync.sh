#!/usr/bin/env bash
# Keeps integration branches (the heads of open draft PRs) in step with main, or flags them (issue #242). Extends
# conflict-alert.sh (#170), which only knows what GitHub's mergeable flag says. Usage: integration-sync.sh [REPO]
# (REPO defaults to $GITHUB_REPOSITORY). Run from a clone whose `origin` is the repo, with full history.
#
# CI never pushes the merge. A merge that brings in hook-layer code (the scope in scripts/security-stamp.sh) needs a
# fresh security-review stamp (2026-11-17 rule; #246 may relax it), and the stamp can't come from CI. So for each draft
# PR whose branch lacks main's tip, the script test-merges in memory (git merge-tree: nothing is checked out and no PR
# code runs) and comments with one of:
#   - conflicts in <files>
#   - mergeable cleanly, needs a merge from main (and a review if hook files change)
# The comment carries a hidden marker with a state key (outcome, files, whether hook files change). A PR is skipped
# while its latest marker comment has the same state, so an unchanged situation is not repeated.
#
# Environment: DRY_RUN=1 prints instead of commenting. Needs `gh` signed in (GH_TOKEN) with pull-requests: write.
set -uo pipefail

repo="${1:-${GITHUB_REPOSITORY:-}}"
[ -n "$repo" ] || { echo "usage: integration-sync.sh [REPO]" >&2; exit 2; }
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
base="${BASE_BRANCH:-main}"

# The security-review scope, read from the stamp script so the two never drift.
read -ra scope <<<"$(sed -n '/^scope=(/,/)$/p' "$here/security-stamp.sh" | tr '\n' ' ' | sed 's/^scope=(//; s/).*$//')"
[ "${#scope[@]}" -gt 0 ] || { echo "integration-sync: could not read the scope from security-stamp.sh" >&2; exit 1; }

git fetch --quiet origin "$base" || { echo "integration-sync: could not fetch $base" >&2; exit 1; }
tip="$(git rev-parse "origin/$base")" || exit 1

prs="$(gh pr list --repo "$repo" --state open --draft --limit 200 \
  --json number,author,headRefName,isCrossRepository \
  --jq '.[] | select(.isCrossRepository | not) | [.number, .author.login, .headRefName] | @tsv')" \
  || { echo "integration-sync: could not list the open PRs" >&2; exit 1; }

failed=0
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
  hookfiles="$(git diff --name-only "$(git merge-base "$tip" "$head")" "$tip" -- "${scope[@]}" | tr '\n' ' ')"
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
  if [ "${DRY_RUN:-}" = 1 ]; then
    echo "#$number: $outcome, would comment (dry run)"
  elif gh pr comment "$number" --repo "$repo" --body "$body" > /dev/null; then
    echo "#$number: $outcome, commented"
  else
    failed=1
  fi
done <<<"$prs"

exit "$failed"
