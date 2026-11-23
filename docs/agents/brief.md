# Standard agent brief

A brief says "follow `docs/agents/brief.md`" plus the task. These are the steps every implementing agent takes; each
links the rule where it lives, so this page never drifts from it.

1. **Claim the issue first**: assign yourself and comment; stop if a branch or PR already covers it
   ([Start, step 0](workflow.md#start)).
2. **Branch from `origin/main`** after `git fetch origin`, unless the brief names an integration branch
   ([Start, step 1](workflow.md#start)). Leave `core.hooksPath` alone.
3. **Test first**, with the `tdd` skill: the narrow test file while iterating, then `bash scripts/check.sh full` alone
   in its own call, both summaries read ([Start, steps 2 to 5](workflow.md#start)).
4. **In the security-review scope** (`scripts/ci/security-stamp.sh`): run `security-reviewer` in the foreground and wait
   for its stamp before you commit. Never hand back staged, unstamped changes
   ([the guards](workflow.md#hook-loading-install-or-update-code-and-the-guards),
   [the stamp](../contributors/reference/security-review.md#the-stamp)).
5. **Commit** with a typed subject and a body of at most 5 lines, plus the attribution trailer
   ([commit messages](../contributors/reference/commit-messages.md)).
   A feature that needs a by-ear check merges as testing (default off), not as a held PR
   ([workflow](workflow.md#a-feature-that-needs-a-by-ear-check)).
6. **Deliver**: open a PR whose body says `Closes #<issue>`, then `gh pr merge <n> --auto`
   ([Deliver](workflow.md#deliver)).
7. **Report** in the length the brief gives, or 150 words ([Report](workflow.md#report)).

Helpers, both read-only: `scripts/dev/pr-status.sh <n...>` prints one line per PR (mergeable, merge state, auto-merge,
queue position, failing checks); `scripts/dev/session-marks.sh <session folder> <report-log>` digests a session's F8
marks beside its logs.
