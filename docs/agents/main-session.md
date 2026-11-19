# Briefing agents (main session)

What the main session decides when it hands work to an agent and when it takes the result back. The agent's own flow,
from claim to report, is in [`workflow.md`](workflow.md).

- Model: Sonnet for docs-only and mechanical tickets (fix lists, mechanical CI edits, tested library code) and for
  reviewing docs-only or test-only changes; Opus for reverse engineering, hooks, security, design and any change to
  the guards (the check scripts, git hooks, check-script tests and CI gates), perf work on them included.
- Hand-back review, scaled to risk (a review round costs about as much as the implementation):
  - `security-reviewer` on every change to the security-review scope, as required.
  - The `code-review` skill's Spec axis, on Opus, for changes to hooks, guards or security; it checks the change against
    the issue's acceptance criteria. `full` and the security review both passed #331, which still failed CI.
  - Its Standards axis, on Sonnet, only for such changes over about 100 changed lines; it checks
    [`coding-standards.md`](../coding-standards.md).
  - Docs-only and small mechanical changes get neither axis.
  - A re-review after a fix round covers only what changed since the last pass, not the whole diff.
- Review findings: fix before merge what a player would notice or what weakens a guard; file pure hardening as a
  follow-up ticket instead of another review round.
- Keep briefs to pointers (the issue, this file, the files to read first) and a word limit for the report.
