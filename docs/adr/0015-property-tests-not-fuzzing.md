---
status: accepted
---

# Property-based tests guard the in-game parsers; coverage-guided fuzzing waits for evidence

Decided by the maintainer on 2026-11-19 in a grilling session on Scorecard's Fuzzing score of 0 (#443).

## Decision

The parsers that run inside the game get property-based tests with FsCheck: `MemoryText` (game memory),
`MessageLibrary` (message text), `ModSettings` (the JSON file players edit) and `InspectorCommands` (the opt-in dev
pipe). For any input, each must not throw, must keep its output bounded, and must fall back to a documented safe
result. About 100 cases run with a fixed seed in every check. The weekly slow suite runs 10,000 cases with a fresh
seed and only reports. Only the regression tests made from what the runs find block a merge.

## Why

The reason is player safety, not the score. A bad parse inside the game crashes it for a blind player, and .NET can't
catch an access violation. A parser that only dev tools use (the executable and import readers, the session-log
converter) reads hash-pinned files or our own output, so fuzzing it would mostly find crashes on inputs that never
happen.

Property tests give most of the protection for a fraction of the upkeep. They live next to each parser's existing
tests. They need no instrumented build, no corpus, no new CI job and no crash triage. Stryker also counts them in the
mutation score. FsCheck is the only C# library Scorecard's Fuzzing check detects (`checks/raw/fuzzing.go` in
ossf/scorecard), so between two otherwise equal libraries it decides the tie.

## Considered options

- **Coverage-guided fuzzing with SharpFuzz and libFuzzer.** It is the strongest at deep binary edge cases, but it
  needs an instrumented build, a scheduled job, a corpus and someone to triage crashes. Scorecard doesn't detect it,
  and ClusterFuzzLite doesn't support .NET. The upkeep isn't worth it for text and small structures.
- **CsCheck.** It has a better API for C# and shrinks failing inputs well, but Scorecard doesn't detect it. It is the
  fallback if FsCheck's licence turns out not to be GPL-3.0-compatible (ADR 0005's rule).
- **Do nothing.** That would leave the one class of bug that crashes the player's game without a test aimed at it.

## Consequences

Four weeks after the tests land, the review records on #443 what they found. SharpFuzz comes back only on evidence: a
real bug in a parser that reads binary data, or a player's crash traced to parsing.
