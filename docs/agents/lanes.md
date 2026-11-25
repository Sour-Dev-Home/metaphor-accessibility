# Lanes

A lane is one agent working a batch of issues in one area (Battle, Field, Menus, Dialogue, ...) while other lanes run
beside it. It follows [`workflow.md`](workflow.md) for every PR; this page adds what a batch needs. Work through the
whole batch in order, and ask only where a rule below says to.

## Claim

Claim each issue before you work on it, as in [Start, step 0](workflow.md#start). Stay in your area's files where you
can; the other lanes own theirs.

## Chunks

- Group the batch into chunks of 3 to 5 related issues: one branch and one PR per chunk, from the latest `origin/main`.
- Base each chunk on `main`. When it touches files that an unmerged PR of yours also touches, wait for that PR to
  merge (`bash scripts/dev/queue.sh`) or work a different chunk meanwhile.
- When the merge queue rejects a PR for a conflict, merge `main`, regenerate the generated files, rerun `full` and push a
  fix commit.

## Each issue

- **Rule 1.** Take engine details from `docs/findings/`, the code or the issue's evidence. When an issue needs a read or
  hook nobody has pinned, pin it statically
  ([query the Ghidra project](../contributors/how-to/query-the-ghidra-project.md)). What stays unpinned: build the
  pinned part, mark the rest `[NEEDS VERIFICATION]`, comment on the issue with exactly what's missing, and move on.
- **Hooks** go through the hook loader's build check ([coding standards](../coding-standards.md), rules 6, 8 and 15).
  A lane's hooks sit outside `.text1` and outside the `.ecode` jump stubs into it.
- **Probes.** When an issue needs a live read whose lead is pinned statically, add a Debug-only `capture` probe in the
  same PR (the #992 pattern) and put its trigger on the session checklist.
- **Rule 9.** Run each new test with your source change reverted and see it fail; say so in the PR's Evidence.
- **New player-facing features and settings** merge as testing
  ([how](workflow.md#a-feature-that-needs-a-by-ear-check)), with the settings page and snapshots regenerated and a
  changelog fragment. A bug fix to a feature that is already on stays on.
- **Umbrella or spec issues** whose work looks done: comment on the issue with what covers it (PRs, features) and list it
  in your report as "looks done".
- **A maintainer decision**: comment the options on the issue, skip it, and list it in your report.

## Session checklist

Write your PR's by-ear items, and each probe's trigger, on the next game session's checklist yourself: the open
"Capture evening" issue ([capture skill, step 3](../../.claude/skills/capture/SKILL.md#3-checklist)). When none is
open, put them in your report.

## Each PR

- Edit files with the Edit tool. Commit subjects are typed, bodies at most 5 lines; a merge from `main` is
  `chore: merge main into <branch>`.
- `bash scripts/check.sh full` passes before every push, printing to the console. Quote its summary line in your reply.
- In the security-review scope, follow [the guards' steps](workflow.md#hook-loading-install-or-update-code-and-the-guards).
- PR body in the `pr` skill's shape (Summary, Evidence before and after, Merge Danger), then
  [deliver](workflow.md#deliver) with auto-merge.

## Report

Under 25 lines, or the brief's limit:

1. Per PR: its number, the issues it closes, one line each.
2. Issues left out and why; "looks done" umbrellas; decisions the maintainer needs to make.
3. `[NEEDS VERIFICATION]` items.
4. A short by-ear checklist for the next game session.
