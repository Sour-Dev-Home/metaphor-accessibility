---
status: accepted
---

# Pull requests run a fast Linux path; the merge queue and `main` run the full Windows check

Decided by the maintainer on 2026-11-16
([#100](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/100),
[#121](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/121),
[#141](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/141)). A pull request runs build and tests on Linux,
with Pester and CodeQL in parallel; each job skips what the change can't affect. The merge queue and pushes to `main`
run the full Windows check. Push CI runs on `main` only. Auto-merge is on for every PR the maintainer's agents open, so
a green fast path queues it. Players run Windows, so Windows stays the gate. Measurements:
`docs/raw-sources/linux-build-spike-2026-11-16.md`; the `timing` job and
[#18](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/18) alert when a stage runs slow.

## Considered options

- **Move the reproducible rebuild off the merge queue.** Deferred: it is the check that catches a build that differs.
- **Run the merge queue on Linux.** Rejected: it would stop testing the platform players use.
- **Leave PRs on Windows.** Slow feedback on every push.

## Consequences

Queue speed stays as it is. A change that passes on Linux can still fail in the queue, and then leaves it.
