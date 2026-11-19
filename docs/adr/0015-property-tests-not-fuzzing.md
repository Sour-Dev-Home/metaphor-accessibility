---
status: accepted
---

# Property-based tests guard the in-game parsers; coverage-guided fuzzing waits for evidence

Decided by the maintainer on 2026-11-19 (#443).

The parsers that run inside the game get FsCheck property tests: `MemoryText`, `MessageLibrary`, `ModSettings` and
`InspectorCommands`. For any input, each must not throw, must keep its output bounded, and must fall back to a
documented safe result. About 100 cases run with a fixed seed in every check. The weekly slow suite runs 10,000 cases
with a fresh seed and only reports.

The reason is player safety: a bad parse inside the game crashes it, and .NET can't catch an access violation. The
dev-only parsers read hash-pinned or self-made files, so they're left out. Property tests need no instrumented build,
corpus or new CI job, and Stryker counts them. FsCheck is the only C# library Scorecard's Fuzzing check detects
(`checks/raw/fuzzing.go`, ossf/scorecard), which breaks the tie.

## Considered options

- **SharpFuzz with libFuzzer:** the strongest at deep binary edge cases, but it brings an instrumented build, a
  corpus and crash triage, and Scorecard doesn't detect it.
- **CsCheck:** a better API for C#, but Scorecard doesn't detect it. It's the fallback if FsCheck's licence isn't
  GPL-3.0-compatible (ADR 0005).

## Consequences

Four weeks after the tests land, #443 records what they found. SharpFuzz comes back only on evidence: a bug in a
parser that reads binary data, or a player's crash traced to parsing.
