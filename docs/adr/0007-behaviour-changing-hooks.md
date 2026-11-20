---
status: accepted
---

# Hooks may change the game's behaviour only when a feature needs it, outside the pass-through adapters

Decided by the maintainer on 2026-11-16. Hooks observe by default: the loader's adapters always call the original
function (architecture rule 14, [ADR 0006](0006-hooks-through-reloaded-shared-hooks.md)). A hook may instead change what
the game does, such as skipping a call or swallowing an input, only when a feature needs it. It is written outside the
shared pass-through adapters ("the long way", rule 14's note), still follows rules 8-11, and gets its own security
review before it merges. The first case is [#58](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/58)'s
repeat key, which swallows the bound input only while a dialogue window is open (between `MsgOpen` and `MsgClose`), so
the key behaves normally everywhere else.

## Considered options

- **Observe only, forever.** Keeps every hook equally safe, but the repeat key can't stop the game acting on the same
  input.
- **Let the adapters skip the original on request.** One place to get wrong, and a bug there would affect every hook.

## Consequences

Rule 14's check must make an exception for these hooks, and each one names the game build it was verified on (ground
rule 2). A change that swallows input is a larger risk than an observer, so it never rides along with other work.
