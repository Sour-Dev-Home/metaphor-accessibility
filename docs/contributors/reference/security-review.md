# Security review

The rules for the security review, the stamp, the label gate and the deny rules. Why each exists:
[security model](../explanation/security-model.md).

## Scope

[AGENTS.md](../../../AGENTS.md#claude-code-setup) names the security-review scope. Its one home is the `scope` list in
[`scripts/ci/security-stamp.sh`](../../../scripts/ci/security-stamp.sh), which names directories, so a new file under
one is in scope with no edit ([ADR 0014](../../adr/0014-review-scope-by-directory.md)). Paths match in any case. It
includes [`.claude/settings.json`](../../../.claude/settings.json), so the hand-back hook and the deny rules below
can't be removed or weakened without a review (#1004), and [`.claude/hooks/`](../../../.claude/hooks/), whose scripts
those settings run on every shell call (#1023).

Two rules sit next to it. The vulnerability audit (`scripts/ci/vuln-audit.sh`) reads only the committed
`packages.lock.json` files, never a project, and CI's build restores in locked mode, so a lock file must match its
project; only the root `Directory.Build.props` may set the lock-file properties. `markdownlint` configs may live only
in `tools/docs/`: `check.sh docs` fails on any other, in any case, because markdownlint loads one from each linted
folder and a `.cjs` or `.mjs` one runs code.

## The stamp

A commit touching the scope needs a [stamp](../../../GLOSSARY.md#review-and-ci):

1. Stage the change, then run the `security-reviewer` agent. When it finds nothing critical or important, its last step
   runs `scripts/ci/security-stamp.sh stamp`, which hashes the staged scope into a stamp under `.git/`. With no
   command the script prints its usage and exits 2; `stamped` checks the stamp against the staged scope.
2. Commit. The pre-commit hook refuses the commit unless the stamp matches the staged scope, so any edit after the
   review needs a new one. The commit-msg hook appends `Security-Review: <hash>`. The stamp is single-use: the
   post-commit and post-merge hooks remove it once a commit carries its trailer, or commits the stamped scope without
   one (`--no-verify`), and a refused commit keeps it for the retry.
3. CI checks only the pull request's HEAD commit, when the PR's diff touches the scope
   ([ADR 0012](../../adr/0012-review-trailer-covers-clean-merges.md), #174, #246). HEAD passes with a
   `Security-Review` trailer matching its own scope content, or through its first parent when HEAD leaves the scope as
   every parent had it or is a clean merge from `main`; the walk passes at a commit already on the base branch, so older
   merges without a trailer don't fail a branch. A commit stamped before the guards joined the scope carries a hash of
   the old scope only; CI still accepts it when the commit changes no guard file, so earlier reviews stay valid.

A [clean merge](../../../GLOSSARY.md#review-and-ci) from `main` needs no stamp, and the commit-msg hook says so and adds
no trailer. A scope conflict you resolve, a file git merges from both sides' changes, and a side kept over the other's
change (`-s ours`, `checkout --ours` or `--theirs`) all need a stamp.

**Hand-backs (#999).** A `SubagentStop` hook in [`.claude/settings.json`](../../../.claude/settings.json) runs
`scripts/dev/require-stamp-before-handback.sh` in the subagent's working tree. While staged changes touch the scope and
`security-stamp.sh stamped` says no, it blocks the hand-back and tells the agent to keep polling the stamp, or to
unstage and say so. `security-reviewer` and the read-only agents (`Explore`, `Plan`, `claude-code-guide`, `web-fetch`,
`statusline-setup`) always pass. Claude Code runs the copy in the session's starting checkout
(`$CLAUDE_PROJECT_DIR`).

**Override (maintainer only).** `SECURITY_REVIEW_SKIP="<reason>" git commit ...` lets the commit through and records
`Security-Review-Skipped: <reason>`, which CI accepts. Agents are denied it.

## Everyone else's pull requests

The `security-reviewed` label (#127, #309). CI treats a PR as the maintainer's when its author is the repository's owner
or has admin permission (in an organization's repository the maintainer's PRs show `MEMBER`). Every other PR,
collaborators' and members' included, works like this:

- Its HEAD needs the `Security-Review` trailer when it touches the scope, as above. A `Security-Review-Skipped` trailer
  doesn't count.
- If it changes anything outside `docs/`, CI also fails until a maintainer reviews it (running the `security-reviewer`
  agent when it touches the scope) and adds the `security-reviewed` label. The label counts only if the latest label
  event was added by an admin, after the PR's latest push; a push after the review needs a new review and the label
  added again. The `review-gate` check runs again when the label goes on, so add it last: another label added after
  it means adding it again. A docs-only PR needs no label.
- Agents working for a collaborator follow the same rules as their hand-written PRs. Auto-merge only lands once this
  gate passes.

The label gate is its own workflow, [`review-gate.yml`](../../../.github/workflows/review-gate.yml) (#330). It runs on
`pull_request_target`, so GitHub runs `main`'s copy of it, and `main`'s `scripts/check.sh review-label`; a PR can't edit
or delete it. It reads the PR's file list, timeline and head through the API with a read-only token and never checks
out or runs the PR's code. It judges the whole diff, so a change made inside a merge commit counts. The trailer check
in `repo-rules` runs the base branch's copy of `scripts/check.sh` and `scripts/ci/security-stamp.sh`, first in its job.
The PR can still edit `ci.yml`, or add a job of its own named `review-gate` that passes, since the ruleset requires the
check by name. So read a workflow change before approving a fork's CI run or labelling it (the remaining gap:
[security model](../explanation/security-model.md#known-limits)).

## Collaborators

A collaborator has the `write` role: they push branches here and merge through the merge queue. The `main` ruleset
keeps 0 required approvals and no code-owner review, so the maintainer's and agents' PRs keep flowing; the label above is
the review gate. 2FA isn't required. Only admins create, update or delete `v*` tags. Workflow runs from forks need the
maintainer's approval for every outside contributor, not only first-timers (Actions settings). The private patterns
list (`PII_PATTERNS`) is a secret of the `main-push` environment, which only `main` may use, so no branch's run can read
it. Before their first PR, a collaborator's clone uses the repository's git hooks and their own private-patterns file
([set up a clone](../how-to/set-up-a-clone.md#2-clone-and-set-up)).

## Deny rules

[`.claude/settings.json`](../../../.claude/settings.json) denies Claude Code every force push, printing
`.claude/pii-patterns.txt` (the Read tool, `cat`, `type`, `Get-Content`, `head`, `tail`, `less`), setting
`SECURITY_REVIEW_SKIP`, and skipping the git hooks with `--no-verify` (or `git commit -n`). `scripts/check.sh` and a
`grep -F -f` over the patterns file stay allowed. The maintainer's private `~/.claude-metaphor/settings.json` also denies
edits and common shell writes inside the game's install folder and its save folder.

These rules match the command text Claude Code sees, so another spelling of the same command can slip past them. The
stamp guards against a forgotten review, not a determined one: anyone can run the stamp script, and CI checks the
trailer, not that a review happened. They are a strong guard, not a guarantee.
