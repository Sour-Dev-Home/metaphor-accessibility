#!/usr/bin/env bash
# Black-box tests for scripts/ci/queue-watch.sh with a fake `gh` that serves fixtures and logs what it was asked to do.
# The fake ignores --jq and prints the fixture, already in the shape the jq filter produces. It keeps the markers of the
# comments it was given, as GitHub would, so a second run sees them.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SCRIPT="$ROOT/scripts/ci/queue-watch.sh"

mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" <<'FAKE'
#!/usr/bin/env bash
# Fixtures in $FIX: list (the `pr list` output), pr-NUM (the graphql output), push-NUM (the activity output for PR NUM's
# branch, named NUM in the list), fail-NUM (graphql fails for NUM), markers-NUM (comment markers); calls go to $FIX/log.
echo "$*" >> "$FIX/log"
case "$1 $2" in
  "pr list") cat "$FIX/list" ;;
  "api graphql")
    for arg in "$@"; do case "$arg" in number=*) n="${arg#number=}" ;; esac; done
    [ ! -f "$FIX/fail-$n" ] || exit 1
    cat "$FIX/pr-$n" ;;
  "api -X")
    for arg in "$@"; do case "$arg" in ref=refs/heads/*) n="${arg#ref=refs/heads/}" ;; esac; done
    cat "$FIX/push-$n" ;;
  "pr view") cat "$FIX/markers-$3" 2>/dev/null || true ;;
  "pr comment")
    printf 'COMMENT %s :: %s\n' "$3" "$*" >> "$FIX/comments"
    sed -n 's/.*<!-- queue-watch:\([^ ]*\) -->.*/\1/p' <<<"$*" >> "$FIX/markers-$3" ;;
  *) exit 1 ;;
esac
FAKE
chmod +x "$WORK/bin/gh"

passed=0
failed=0
NOW_AT="$(date -d 2026-11-20T02:00:00Z +%s)"
HEAD=0123456789abcdef0123456789abcdef01234567

setup() { # setup: a fresh fixture folder
  FIX="$WORK/fix"
  rm -rf "$FIX"
  mkdir -p "$FIX"
  : > "$FIX/list"
  export FIX
}

pr() { # pr NUM MERGEABLE: lists PR NUM (author user-NUM, branch NUM, head $HEAD)
  printf '%s\tuser-%s\t%s\t%s\t%s\n' "$1" "$1" "$1" "$HEAD" "$2" >> "$FIX/list"
}

state() { # state NUM QUEUED AUTO RUNS REQUIRED [TYPE REASON TIME]...: PR NUM's state line and timeline
  local n="$1"
  printf '%s\t%s\t%s\t%s\n' "$2" "$3" "$4" "$5" > "$FIX/pr-$n"
  shift 5
  while [ "$#" -gt 0 ]; do printf '%s\t%s\t%s\n' "$1" "$2" "$3" >> "$FIX/pr-$n"; shift 3; done
}

# A PR that was queued from auto-merge, as GitHub records it (#479), then removed by a failed queue run.
QUEUED=(PullRequestCommit - 2026-11-20T00:27:08Z AutoMergeEnabledEvent - 2026-11-20T00:27:25Z
  AutoMergeDisabledEvent manually_disabled 2026-11-20T00:57:14Z AddedToMergeQueueEvent - 2026-11-20T00:57:16Z)
DROPPED=("${QUEUED[@]}" RemovedFromMergeQueueEvent failed_checks 2026-11-20T01:00:34Z)

run() { # run: the script against the fixtures
  out="$(PATH="$WORK/bin:$PATH" NOW="$NOW_AT" bash "$SCRIPT" owner/repo 2>&1)"
  code=$?
  comments="$(cat "$FIX/comments" 2>/dev/null || true)"
}

expect() { # expect NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '%s\n' "$out" "$comments"; fi
}

commented() { [ "$(grep -c "^COMMENT $1 " <<<"$comments")" -eq 1 ]; }
silent() { ! grep -q "^COMMENT $1 " <<<"$comments"; }
untouched() { ! grep -qE "^pr (merge|edit|close|reopen)|mutation" "$FIX/log"; }

# Dropped PRs, and the ones left alone.
setup
pr 1 MERGEABLE && state 1 false false 3 pass "${DROPPED[@]}"
pr 2 MERGEABLE && state 2 true false 3 pass "${QUEUED[@]}"
pr 3 MERGEABLE && state 3 false false 3 pass PullRequestCommit - 2026-11-20T00:27:08Z
pr 4 MERGEABLE && state 4 false true 3 pass "${DROPPED[@]}"
pr 5 MERGEABLE && state 5 false false 3 wait "${DROPPED[@]}"
pr 6 CONFLICTING && state 6 false false 3 pass "${DROPPED[@]}"
pr 7 MERGEABLE && state 7 false false 3 pass "${DROPPED[@]}" PullRequestCommit - 2026-11-20T01:28:47Z
pr 8 MERGEABLE && state 8 false false 3 pass PullRequestCommit - 2026-11-20T00:27:08Z \
  AutoMergeEnabledEvent - 2026-11-20T00:27:25Z AutoMergeDisabledEvent manually_disabled 2026-11-20T00:40:00Z
pr 9 MERGEABLE && state 9 false false 3 pass PullRequestCommit - 2026-11-20T00:27:08Z \
  AddedToMergeQueueEvent - 2026-11-20T01:29:00Z RemovedFromMergeQueueEvent invalid_merge_commit 2026-11-20T01:29:16Z
pr 10 MERGEABLE && state 10 false false 3 pass "${QUEUED[@]}" RemovedFromMergeQueueEvent merged 2026-11-20T01:40:38Z
pr 11 MERGEABLE && state 11 false false 3 pass PullRequestCommit - 2026-11-20T00:27:08Z \
  AutoMergeEnabledEvent - 2026-11-20T00:27:25Z AutoMergeDisabledEvent some_other_reason 2026-11-20T00:40:00Z
run
expect "dropped: exits zero" [ "$code" -eq 0 ]
expect "dropped: a PR the queue removed gets one comment" commented 1
expect "dropped: the comment carries the marker keyed on the drop" \
  grep -qF '<!-- queue-watch:dropped:2026-11-20T01:00:34Z -->' <<<"$comments"
expect "dropped: the comment names the reason" grep -q 'failed_checks' <<<"$comments"
expect "dropped: the comment mentions the author" grep -q '@user-1' <<<"$comments"
expect "dropped: the comment says how to turn auto-merge back on" grep -qF 'gh pr merge 1 --auto' <<<"$comments"
expect "dropped: a PR still in the queue is left alone" silent 2
expect "dropped: a PR never under auto-merge is left alone" silent 3
expect "dropped: a PR with auto-merge on is left alone" silent 4
expect "dropped: a PR whose required checks haven't passed is left alone" silent 5
expect "dropped: a conflicting PR is left alone (conflict-alert's job)" silent 6
expect "dropped: a push after the drop clears it" silent 7
expect "dropped: a person turning auto-merge off is left alone" silent 8
expect "dropped: a PR queued directly and removed counts" commented 9
expect "dropped: a merged queue exit isn't a drop" silent 10
expect "dropped: auto-merge disabled by GitHub counts" commented 11
expect "dropped: it never merges, edits or reopens" untouched

# Repeated runs: the markers from the first run keep the second quiet; a new drop gets a new comment.
rm -f "$FIX/comments"
run
expect "repeat: exits zero" [ "$code" -eq 0 ]
expect "repeat: no comment is repeated" [ -z "$comments" ]
state 1 false false 3 pass "${DROPPED[@]}" AutoMergeEnabledEvent - 2026-11-20T01:10:00Z \
  AddedToMergeQueueEvent - 2026-11-20T01:10:02Z RemovedFromMergeQueueEvent failed_checks 2026-11-20T01:20:00Z
run
expect "repeat: a later drop gets its own comment" commented 1
expect "repeat: keyed on the later drop" grep -qF 'queue-watch:dropped:2026-11-20T01:20:00Z' <<<"$comments"

# No CI run (#329).
setup
pr 21 MERGEABLE && state 21 false false 0 wait PullRequestCommit - 2026-11-20T01:00:00Z
printf '%s\t2026-11-20T01:49:00Z\n' "$HEAD" > "$FIX/push-21"
pr 22 MERGEABLE && state 22 false false 0 wait PullRequestCommit - 2026-11-20T01:00:00Z
printf '%s\t2026-11-20T01:55:00Z\n' "$HEAD" > "$FIX/push-22"
pr 23 MERGEABLE && state 23 false false 0 wait PullRequestCommit - 2026-11-20T01:00:00Z
printf 'fedcba9876543210fedcba9876543210fedcba98\t2026-11-20T01:00:00Z\n' > "$FIX/push-23"
pr 24 MERGEABLE && state 24 false false 2 wait PullRequestCommit - 2026-11-20T01:00:00Z
run
expect "no-ci: exits zero" [ "$code" -eq 0 ]
expect "no-ci: a head with no run 11 minutes after its push gets the hint" commented 21
expect "no-ci: the hint says to close and reopen" grep -qF 'gh pr close 21 && gh pr reopen 21' <<<"$comments"
expect "no-ci: the marker names the commit" grep -qF "<!-- queue-watch:no-ci:$HEAD -->" <<<"$comments"
expect "no-ci: 5 minutes after the push is too soon" silent 22
expect "no-ci: a push time for another commit is no evidence" silent 23
expect "no-ci: a PR whose CI started is left alone" silent 24
expect "no-ci: it never closes or reopens a PR itself" untouched
rm -f "$FIX/comments"
run
expect "no-ci: repeated runs don't repeat the hint" [ -z "$comments" ]

# Failures: an unreadable PR fails the run without stopping the others.
setup
pr 31 MERGEABLE && touch "$FIX/fail-31"
pr 32 MERGEABLE && state 32 false false 3 pass "${DROPPED[@]}"
run
expect "failure: an unreadable PR fails the run" [ "$code" -eq 1 ]
expect "failure: the next PR is still checked" commented 32

setup
out="$(PATH="$WORK/bin:$PATH" env -u GITHUB_REPOSITORY bash "$SCRIPT" 2>&1)"
code=$?
comments=""
expect "no repo is a usage error" [ "$code" -eq 2 ]

echo "queue-watch tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
