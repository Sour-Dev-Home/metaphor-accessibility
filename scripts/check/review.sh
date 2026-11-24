# shellcheck shell=bash
# check.sh's security-review checks (ADR 0013): the review trailer check (ADR 0012: stamps, clean merges and carried
# reviews), which pre-commit, commit-msg and the commits stage run, and the review gate (#330), which the review-label
# stage runs from main's copy (review-gate.yml).
# Sourced by scripts/check.sh only; it uses report and die from report.sh.
#
# Run globals: review_trailer reads `tmp`, which main sets; maintainer_pr caches its answer in `maintainer_pr_result`,
# which main resets; report updates `failures`. `check_dir` is check.sh's constant, which stamp_script is built from.
# Reads SECURITY_REVIEW_SKIP, GITHUB_REPOSITORY, PR_NUMBER, PR_AUTHOR_ASSOCIATION, PR_AUTHOR, PR_CHANGED_FILES,
# PR_UPDATED_AT, PR_HEAD_SHA, PR_EVENT_ACTION and PR_EVENT_LABEL.
# Private to this module: stamp_script, and review_by, which review_gate sets for review_trailer.

# The security review's stamp script answers for the repository in the current folder.
stamp_script="$check_dir/ci/security-stamp.sh"

# in_scope [COMMIT]: exit 0 when the staged changes (or COMMIT) touch the security-review scope, 1 when they don't.
in_scope() {
  bash "$stamp_script" touches "$@" > /dev/null
  case $? in
    0) return 0 ;;
    1) return 1 ;;
    *) die "could not tell whether ${1:-the commit} touches the security-review scope" ;;
  esac
}

# skipping: exit 0 when SECURITY_REVIEW_SKIP gives a reason (not just spaces).
skipping() {
  local reason="${SECURITY_REVIEW_SKIP:-}"
  [ -n "${reason//[[:space:]]/}" ]
}

# stamp_check CHECK [COMMIT]: run the stamp script's yes-or-no CHECK; exit 0 for yes, 1 for no, die when it fails.
stamp_check() {
  bash "$stamp_script" "$@"
  case $? in
    0) return 0 ;;
    1) return 1 ;;
    *) die "security review: could not run the stamp script's $1 check${2:+ on $2}" ;;
  esac
}

# review_gate: a staged change to the scope needs a stamp matching it, unless SECURITY_REVIEW_SKIP gives a reason or
# the commit is a clean merge from main (docs/adr/0012-review-trailer-covers-clean-merges.md). Exit 0 when the commit
# may go ahead, setting review_by (skip, stamp or clean-merge), which review_trailer reads to pick the trailer.
review_gate() {
  review_by=skip
  skipping && return 0
  review_by=stamp
  stamp_check stamped && return 0
  review_by=clean-merge
  stamp_check clean-merge && return 0
  report "security review: the staged hook or deploy code has no matching stamp. Run the security-reviewer agent \
(it stamps when it finds nothing critical or important), or set SECURITY_REVIEW_SKIP=\"<reason>\" (maintainer only). A \
clean merge from origin/main needs none (ADR 0012)"
  return 1
}

# review_trailer FILE: on a commit touching the scope, replace any Security-Review trailers in the message FILE with the
# one this commit earns. A clean merge from main earns none: CI judges it by its first parent.
review_trailer() {
  local file="$1" trailer=""
  in_scope || return 0
  review_gate || return 0
  case "$review_by" in
    skip) trailer="Security-Review-Skipped: ${SECURITY_REVIEW_SKIP//[$'\r\n']/ }" ;;
    stamp) trailer="Security-Review: $(bash "$stamp_script" hash)" || die "could not hash the security-review scope" ;;
    clean-merge) echo "security review: not needed, a clean merge from main: each hook-scope file is the version one \
side changed, reviewed there (ADR 0012)" ;;
  esac
  grep -viE '^Security-Review(-Skipped)?:' "$file" > "$tmp/message"
  [ $? -le 1 ] || die "could not read $file"
  if [ -n "$trailer" ]; then
    git interpret-trailers --trailer "$trailer" < "$tmp/message" > "$file" || die "could not add the trailer to $file"
  else
    cp "$tmp/message" "$file" || die "could not write $file"
  fi
}

# check_head_review BASE..HEAD: when the range touches the scope, judge HEAD alone, walking first parents through clean
# merges and commits leaving the scope alone (docs/adr/0012-review-trailer-covers-clean-merges.md).
check_head_review() {
  local base="${1%%..*}" sha="${1#*..}" head parent
  in_scope "$1" || return 0
  head="$(git rev-parse -q --verify "${sha#.}^{commit}")" || die "could not read the head of $1"
  sha="$head"
  while :; do
    git merge-base --is-ancestor "$sha" "$base"
    case $? in
      0) return 0 ;;
      1) ;;
      *) die "could not tell whether $sha is on $base" ;;
    esac
    has_review_trailer "$sha" && return 0
    parent="$(git rev-parse -q --verify "$sha^1")" || break # a root commit has no carried review
    { ! in_scope "$sha" || stamp_check clean-merge "$sha"; } || break
    sha="$parent"
  done
  report "commit $(git rev-parse --short "$sha"): touches the security-review scope without a Security-Review trailer \
matching its content, or a Security-Review-Skipped one on the maintainer's PR$([ "$sha" = "$head" ] || echo "; HEAD \
reaches it through clean merges from main or commits leaving the scope alone (ADR 0012)")"
}

# has_review_trailer SHA: exit 0 when SHA carries "Security-Review: <a hash the stamp script accepts for it>" (its scope
# hash, or its old-scope hash from before #306) or, on the maintainer's PR, a Security-Review-Skipped trailer with a
# reason.
has_review_trailer() {
  local sha="$1" want reviewed skipped hash
  want="$(bash "$stamp_script" accepted "$sha")" || die "could not hash the security-review scope of $sha"
  # Both trailers from one git start, split at a \x01 byte. A trailer holding one of its own would move the split, so
  # more than one fails closed.
  local format='%(trailers:key=Security-Review,valueonly)%x01%(trailers:key=Security-Review-Skipped,valueonly)'
  reviewed="$(git log -1 --no-show-signature --format="$format" "$sha")" || die "could not read $sha" # #455
  skipped="${reviewed#*$'\x01'}"
  [[ "$skipped" != *$'\x01'* && "$reviewed" == *$'\x01'* ]] || die "could not read the trailers of $sha"
  reviewed="${reviewed%%$'\x01'*}"
  for hash in $want; do
    grep -qxF "$hash" <<<"$reviewed" && return 0
  done
  # SECURITY_REVIEW_SKIP is the maintainer's, so a skip trailer counts only on the maintainer's PR (#309).
  [ -n "${skipped//[[:space:]]/}" ] && maintainer_pr
}

# is_admin LOGIN: exit 0 when LOGIN has admin permission on GITHUB_REPOSITORY, 1 when not (or the login isn't one GitHub
# could issue, so it never reaches the API path); dies when the permission can't be read.
is_admin() {
  local permission
  [[ "$1" =~ ^[A-Za-z0-9][A-Za-z0-9-]*(\[bot\])?$ ]] || return 1
  permission="$(gh api "repos/${GITHUB_REPOSITORY:-}/collaborators/$1/permission" --jq .permission)" ||
    die "could not read the permission of $1 on ${GITHUB_REPOSITORY:-}"
  [ "$permission" = admin ]
}

# maintainer_pr: exit 0 when PR_AUTHOR_ASSOCIATION is empty (a local run) or OWNER, or the PR's author (PR_AUTHOR) is
# an admin: in an organization's repository the maintainer's own PRs show MEMBER. Cached after the first call.
maintainer_pr() {
  if [ -z "${maintainer_pr_result:-}" ]; then
    maintainer_pr_result=no
    case "${PR_AUTHOR_ASSOCIATION:-}" in
      "" | OWNER) maintainer_pr_result=yes ;;
      *) [ -n "${PR_AUTHOR:-}" ] && is_admin "$PR_AUTHOR" && maintainer_pr_result=yes ;;
    esac
  fi
  [ "$maintainer_pr_result" = yes ]
}

# require_review_label PUSHED: a PR not by the maintainer changing files outside docs/ needs the security-reviewed
# label, added by an admin after its latest push (#127, #309). CI's token is read-only, so it can't remove a stale
# label; it reads the timeline instead. Push time is the latest of the timeline's commits and force pushes and PUSHED (a
# time GitHub set, so a backdated commit can't make a label look fresh; empty when the caller checked the push another
# way). ISO 8601 UTC timestamps compare as text. Only the latest label event counts, so a non-admin re-adding the label
# replaces the maintainer's.
require_review_label() {
  local repo="${GITHUB_REPOSITORY:-}" pr="${PR_NUMBER:-}" events kind date actor pushed="$1"
  local label_state="" labeled_at="" labeled_by=""
  events="$(gh api --paginate "repos/$repo/issues/$pr/timeline" --jq \
    '.[] | (select((.event=="labeled" or .event=="unlabeled") and .label.name=="security-reviewed")
       | "\(.event)\t\(.created_at)\t\(.actor.login // "")"),
     (select(.event=="head_ref_force_pushed") | "push\t\(.created_at)"),
     (select(.event=="committed") | "push\t\(.committer.date)")')" || die "could not read the timeline of PR #$pr"
  while IFS=$'\t' read -r kind date actor; do
    case "$kind" in
      labeled | unlabeled) label_state="$kind" labeled_at="$date" labeled_by="$actor" ;;
      push) [[ "$date" > "$pushed" ]] && pushed="$date" ;;
    esac
  done <<<"$events"
  [ "$label_state" = labeled ] ||
    { report "PR not by the maintainer changes files outside docs/: a maintainer must review it and add the \
security-reviewed label"; return; }
  is_admin "$labeled_by" ||
    { report "security-reviewed was added by $labeled_by, who isn't an admin: a maintainer must review the PR and add \
the label"; return; }
  [[ "$labeled_at" > "$pushed" ]] ||
    report "security-reviewed was added before the PR's latest push ($pushed): review the new commits and add the label again"
}

# head_arrived SHA REF REPO: print when SHA became the head of the PR from branch REF of repository REPO, from the
# review-gate workflow's runs, whose creation times GitHub sets, where a commit date can be backdated (#321). Every push
# starts a run, so it is the oldest run in the newest unbroken stretch of runs at SHA on that branch: a run at another
# head ends the stretch. Reading only the latest 100 can only make the time later, which fails closed. Fails when no run
# at SHA is listed, or when the workflow's file is renamed, so the gate fails closed then too.
head_arrived() {
  local runs sha created repo arrived=""
  runs="$(gh api -X GET "repos/$GITHUB_REPOSITORY/actions/workflows/review-gate.yml/runs" -f event=pull_request_target \
    -f branch="$2" -F per_page=100 \
    --jq '.workflow_runs[] | "\(.head_sha)\t\(.created_at)\t\(.head_repository.full_name // "")"')" || return 1
  while IFS=$'\t' read -r sha created repo; do
    [ "$repo" = "$3" ] || continue
    [ "$sha" = "$1" ] || break
    arrived="$created"
  done < <(sort -t $'\t' -k2,2r <<<"$runs")
  [ -n "$arrived" ] && echo "$arrived"
}

# review_label_stage FILE: the review gate the review-gate workflow runs from main's copy (#330), so a PR can't edit the
# check that judges it. FILE lists the PR's changed files as the API gives them, never the PR's code: one per line, the
# path, then a tab and the old path for a rename. The event gives PR_CHANGED_FILES, PR_UPDATED_AT, PR_HEAD_SHA,
# PR_EVENT_ACTION and PR_EVENT_LABEL, beside what maintainer_pr and require_review_label read.
review_label_stage() {
  local path previous count=0 outside="" pushed="${PR_UPDATED_AT:-}" head head_ref head_repo created
  [ -f "$1" ] || die "review-label stage needs the file listing the PR's changed files"
  [[ "${PR_CHANGED_FILES:-}" =~ ^[0-9]+$ ]] || die "review-label stage needs PR_CHANGED_FILES, the event's file count"
  while IFS=$'\t' read -r path previous; do
    count=$((count + 1))
    [[ "$path" == docs/* && ( -z "$previous" || "$previous" == docs/* ) ]] || outside=1
  done < "$1"
  # The API lists at most 3000 files; a shorter list than the event's count could hide a change outside docs/.
  [ "$count" -eq "$PR_CHANGED_FILES" ] || die "listed $count of the PR's $PR_CHANGED_FILES changed files"
  [ -n "$outside" ] && ! maintainer_pr || return 0
  [ -n "${GITHUB_REPOSITORY:-}" ] && [ -n "${PR_NUMBER:-}" ] ||
    die "a PR not by the maintainer needs GITHUB_REPOSITORY and PR_NUMBER to read its timeline"
  # A label event's updated_at is the event's own time, no older than the review label, so any label's run checks the
  # push another way (#321), never by skipping, which would count as a pass. The event's payload names the head when the
  # label changed, and that head must still be the PR's head. Adding security-reviewed then needs nothing more: it went
  # on at this head. Any other label event reads when this head arrived (head_arrived).
  if [ "${PR_EVENT_ACTION:-}" = labeled ] || [ "${PR_EVENT_ACTION:-}" = unlabeled ]; then
    head="$(gh api "repos/$GITHUB_REPOSITORY/pulls/$PR_NUMBER" \
      --jq '[.head.sha, .head.ref, (.head.repo.full_name // ""), .created_at] | @tsv')" ||
      die "could not read the head of PR #$PR_NUMBER"
    IFS=$'\t' read -r head head_ref head_repo created <<<"$head"
    [ -n "${PR_HEAD_SHA:-}" ] && [ "$head" = "$PR_HEAD_SHA" ] ||
      { report "PR #$PR_NUMBER was pushed to after security-reviewed was added: review the new commits and add the \
label again"; return; }
    pushed=""
    if [ "$PR_EVENT_ACTION" != labeled ] || [ "${PR_EVENT_LABEL:-}" != security-reviewed ]; then
      pushed="$(head_arrived "$head" "$head_ref" "$head_repo")" ||
        die "could not tell when the head of PR #$PR_NUMBER arrived from the review-gate runs"
      # The runs are the branch's, not the PR's: on a reused branch name an older PR's run could date the head too
      # early, so it arrived no sooner than this PR was opened.
      [ -n "$created" ] || die "could not read when PR #$PR_NUMBER was opened"
      [[ "$created" < "$pushed" ]] || pushed="$created"
    fi
  else
    [ -n "$pushed" ] || die "a PR not by the maintainer needs PR_UPDATED_AT, the event's update time"
  fi
  require_review_label "$pushed"
}
