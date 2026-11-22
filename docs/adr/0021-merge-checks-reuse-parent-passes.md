---
status: accepted
---

# A merge's `full` reuses its parents' recorded passes

Decided by the maintainer on 2026-11-22 (#728): agents keep checking locally, and each merge check does less. A
pass is never claimed for code that wasn't built and tested together.

Locally, `full` on a merge (committed, or in progress with the result staged) whose parents' trees have recorded
`full` passes runs the repo rules, then only what both sides' changes reach. A side is the files where the merge
differs from one parent: the other parent's changes, plus any conflict resolution, which matches neither.
`scripts/ci/what-runs.sh` maps each side:

- A project is built and tested when both sides reach it, or one reaches it and the other changed a build-wide file.
  A project only one side reaches has the other parent's inputs, so that parent's result.
- The Pester tests and the check-script suites each run when both sides touch what they read.
- A parent without a recorded pass counts as reaching everything.

The pass names the parent passes it relied on (`full <tree> merge <parent tree|-> <parent tree|->`). The rule is only
as good as the map: a test reading files outside it goes into the map, as `pester-reads` does. CI and
`METAPHOR_FULL_TESTS=1` never narrow; the merge queue still runs everything.

## Considered options

- **Reuse only for a clean merge.** A changelog conflict, the common case, would rebuild everything.
- **Run the union of both sides.** Reruns what a parent already passed.
