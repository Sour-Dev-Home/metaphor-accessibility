---
status: accepted
---

# A review trailer covers clean merges from `main`

Decided by the maintainer on 2026-11-17 (#174, option (a)) and 2026-11-18 (#246, this ADR's rule). When a PR's diff
touches the security-review scope, CI checks its HEAD commit, not each commit: HEAD passes when it carries a
`Security-Review` trailer matching HEAD's scope hash, or when it is a clean merge from `main` whose first parent passes.
A merge is clean when its second parent is already on `main` and every scope file in it is identical to the first
parent's version or the second parent's. A conflict resolution in the scope matches neither, so it still needs a new
stamp. Code arriving from `main` was reviewed there; a fresh review on every merge (2026-11-17, `capture-1`
and #226) cost a review round for nothing new.

The stamp stays a hash of the scope's state (`scripts/security-stamp.sh`). Only the commit at HEAD is checked, so old
merges without trailers deeper in a branch's history don't fail it.

## Considered options

- **Re-review every merge from `main` (the rule before this ADR).** Safe but reviews code already reviewed on `main`.
- **Hash the branch's scope changes against `main` instead of the scope's state.** A clean merge would keep its hash
  for free, but every stamp would change meaning, for the same result.
