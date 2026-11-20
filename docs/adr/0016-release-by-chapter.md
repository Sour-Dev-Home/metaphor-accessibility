---
status: accepted
---

# After the current sprint, features and releases go chapter by chapter

Decided by the maintainer on 2026-11-20 (#491).

The current sprint keeps the system-based plan in #491: five game sessions, each tackling many systems at once, for fast
progress on discovery. After it, feature work and releases follow the game's chapters. One chapter is made playable
blind from start to finish: the maintainer plays it, and we iterate until its systems are right. That slice is
released, then the next chapter and the systems or menus it introduces. A system is built only as deep as the current
chapter needs.

A chapter is done when it has been played start to finish with a screen reader, ideally by a blind tester, and every
blocker is fixed.

The comparable-mods survey and the ygol-access comparison show small, frequent releases keep a mod usable and
tested. A chapter is the smallest slice a player can finish, and the game's own order
(`docs/raw-sources/metaphor-chapter-structure-2026-11-20.md`) says which systems come first.

## Considered options

- **Keep releasing by system:** broad but shallow; nothing is playable end to end for a long time.
- **Release by chapter, now:** the sprint's discovery sessions are cheaper done by system.

## Consequences

Battle and field basics move forward if chapter 1 needs them. PR #294's battle branch is resolved when chapter 1 needs
it, not before. Milestones "Chapter 1: playable blind" and onward hold the work. The chapter list is unverified until
played ([NEEDS VERIFICATION], see the raw source).

The per-chapter routine is in [plan a chapter](../contributors/how-to/plan-a-chapter.md).
