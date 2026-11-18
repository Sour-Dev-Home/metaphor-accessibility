# Agent workflow

The rules an agent follows from claim to report. Briefs link here instead of repeating them. Hooks and config enforce
the mechanics; this page holds what they don't say. Setup, checks and the security review are in
[`CONTRIBUTING.md`](../../.github/CONTRIBUTING.md); judgement rules are in
[`coding-standards.md`](../coding-standards.md); tracker commands are in [`issue-tracker.md`](issue-tracker.md).

## Start

0. Claim the issue. Others may take the same one by accident: first look for a branch, PR or "working on this" comment
   that mentions it (`gh pr list --search "<n>"`, `git branch -r | grep <n>`, the issue's comments). If you find one,
   stop and report it. Otherwise assign yourself or comment that you're on it.
1. Branch from the base your brief names (`origin/main` unless it says an integration branch), then reset onto it:
   `git fetch origin && git reset --hard origin/<base>`.
2. Build test-first with the `tdd` skill.
3. Run the narrow test file while iterating, then the full suite once. Read the summary line of each; a trimmed pipe
   can hide a failed start.

`check.sh full` records a pass for the index's tree (`git write-tree`) in your clone's git dir, and `full` or `pre-push`
on that same tree prints `reused the recorded pass for tree <hash>` and skips the 10-15 minutes. A reviewer can rely on
that line: any change, staged or committed, runs the checks again. Don't rerun `full` on a tree it has passed.

A check that looks stalled: `bash scripts/check-status.sh` shows who holds each of the two machine-wide build slots
(`METAPHOR_CHECK_SLOTS`, default 2), who waits, each worktree's last runs and idle build workers (with the command to
stop them). The status line shows the `--short` form.

## Commit

- Subjects: the format and the 72-character limit are in the header of [`scripts/check.sh`](../../scripts/check.sh)
  (`commit-msg` hook). Merge commits need a conventional subject too; git's default is rejected.
- Add the trailer the session's attribution reminder gives (`Co-Authored-By: ...`).
- Never force-push. Deny rules in [`.claude/settings.json`](../../.claude/settings.json) already block it.
- Stop only processes you started in this task. A check or lock held by someone else: wait, or hand back and say so.

## Hook, loading, install or update code

The scope and the stamp are in
[`CONTRIBUTING.md`](../../.github/CONTRIBUTING.md#security-review-and-deny-rules). Two steps are yours:

1. Merge your base's tip (`main`, or the integration branch) into your branch first, so a conflict fix never lands
   after the review and voids the stamp.
2. Stage the change and run `bash scripts/check.sh full` yourself. On an uncommitted tree the build is marked dirty, so
   `SourceRevisionTests.AnAssemblyFromThisBuildKnowsItsFullCommit` fails; that one failure is expected and passes once
   committed (#169). Report any other failure.
3. Hand the staged change back to the main session. A `security-reviewer` launched in the background ends with your
   turn, before it reports, and the stamp is lost. The main session runs `security-reviewer`, then commits and pushes.

Commit with the stamp. `SECURITY_REVIEW_SKIP` is the maintainer's.

## Deliver

- **On an integration branch** (the brief names one): merge its tip into your branch and push your branch. The
  integration branch takes no pushes and no PR from you. After the main session merges your branch into it, comment on
  the ticket `merged into <branch> at <sha>`, so the next agent finds the ticket done.
- **Anywhere else**: open a PR with the `pr` skill's template, then `gh pr merge <n> --auto`. The merge queue refuses
  `--squash` and `--delete-branch`.
- Check every push's own exit code (`git push ...; echo $?`), never a pipe's or an echo after it: a pre-push hook that
  fails or times out on the build lock leaves the branch unpushed while the command looks quiet.

## Integration branches behind main

Integration branches are the heads of open draft PRs. When `main` moves, the
[`Conflict alert`](../../.github/workflows/conflict-alert.yml) workflow runs
[`scripts/integration-sync.sh`](../../scripts/integration-sync.sh) (#242): it test-merges `main` into each draft PR's
branch in memory and comments once, with either the conflicting files or "mergeable cleanly, needs a merge from `main`
(and a review if hook files change)". It stays quiet while the outcome is unchanged. CI never pushes the merge: one that
brings hook-layer code from `main` needs a fresh security-review stamp (the 2026-11-17 rule, which #246 may relax), and
CI can't give one. When you see the comment, merge `main` into the branch yourself, following the steps above if hook
files changed.

## Borrowed ideas and code

Credit them by the rule in [`AGENTS.md`](../../AGENTS.md#ground-rules) (rule 7) and record them in
[`CREDITS.md`](../../CREDITS.md).

## Report

Brief: the PR number or branch, what changed, anything left undone.
