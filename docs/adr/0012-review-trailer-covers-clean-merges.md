---
status: accepted
---

# A review trailer covers clean merges from `main`

Decided by the maintainer on 2026-11-17 (#174, option (a)) and 2026-11-18 (#246, this ADR's rule). When a PR's diff
touches the security-review scope, CI checks its HEAD commit, not each commit: HEAD passes when it carries a
`Security-Review` trailer matching HEAD's scope hash, or when it is a clean merge from `main` whose first parent passes.
A commit whose scope equals every parent's is a carried review: its first parent's review covers it; the walk
passes at a commit on the base branch.

A merge is clean when its second parent is already on `main` and each scope file, compared with the parents' merge
base, is the first parent's where `main` left it unchanged, or the second's where the branch did. Keeping one side
over the other's change (`-s ours`) could drop a fix unseen, and a conflict resolution matches neither; both need a
stamp. Code from `main` was reviewed there; a fresh review on every merge (2026-11-17, `capture-1` and #226) cost a
round for nothing new.

The stamp stays a hash of the scope's state (`scripts/ci/security-stamp.sh`). Only the walk from HEAD is checked, so old
trailerless merges deeper in a branch don't fail it.

## Considered options

- **Re-review every merge from `main` (the rule before this ADR).** Safe but reviews code already reviewed on `main`.
- **Hash the branch's scope changes against `main` instead of the scope's state.** A clean merge would keep its hash
  for free, but every stamp would change meaning, for the same result.
