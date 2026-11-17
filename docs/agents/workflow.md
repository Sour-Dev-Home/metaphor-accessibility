# Agent workflow

The rules an agent follows from claim to report. Briefs link here instead of repeating them. Hooks and config enforce
the mechanics; this page holds what they don't say. Setup, checks and the security review are in
[`CONTRIBUTING.md`](../../.github/CONTRIBUTING.md); judgement rules are in
[`coding-standards.md`](../coding-standards.md); tracker commands are in [`issue-tracker.md`](issue-tracker.md).

## Start

1. Branch from the base your brief names (`origin/main` unless it says an integration branch), then reset onto it:
   `git fetch origin && git reset --hard origin/<base>`.
2. Build test-first with the `tdd` skill.
3. Run the narrow test file while iterating, then the full suite once. Read the summary line of each; a trimmed pipe
   can hide a failed start.

`check.sh full` records a pass for the index's tree (`git write-tree`) in your clone's git dir, and `full` or `pre-push`
on that same tree prints `reused the recorded pass for tree <hash>` and skips the 10-15 minutes. A reviewer can rely on
that line: any change, staged or committed, runs the checks again. Don't rerun `full` on a tree it has passed.

## Commit

- Subjects: the format and the 72-character limit are in the header of [`scripts/check.sh`](../../scripts/check.sh)
  (`commit-msg` hook). Merge commits need a conventional subject too; git's default is rejected.
- Add the trailer the session's attribution reminder gives (`Co-Authored-By: ...`).
- Never force-push. Deny rules in [`.claude/settings.json`](../../.claude/settings.json) already block it.

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

## Borrowed ideas and code

Credit them by the rule in [`AGENTS.md`](../../AGENTS.md#ground-rules) (rule 7) and record them in
[`CREDITS.md`](../../CREDITS.md).

## Report

Brief: the PR number or branch, what changed, anything left undone.
