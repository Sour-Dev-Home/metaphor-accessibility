---
status: accepted
---

# Hooks in `.text1` stay opt-in in every build

Decided by the maintainer after spike session 1 (2026-11-16) and confirmed on 2026-11-17 (#206): a hook on a function in
the exe's `.text1` section is opt-in (`OptInFile`) or development-only (`DevOnly`), in Debug and Release alike, and is
dropped rather than worked around if the protection ever reacts to a detour there. Most of our signatures match in `.ecode`;
the dialogue window's script functions reach their bodies in `.text1` through `jmp` stubs
(`docs/raw-sources/msg-window-functions-2026-11-16.md`). The game uses Denuvo, whose EULA clause makes tampering with it
a material breach
([`eula-and-mod-stance-2026-11-16.md`](../raw-sources/eula-and-mod-stance-2026-11-16.md), section 18), and ground rule 5
forbids circumventing it. Hooks there have run whole sessions with no reaction
(`docs/raw-sources/spike-session-1-2026-11-16.md`), but a player should never meet that risk by default. What `.text1`
holds and why its code doesn't decode is [NEEDS VERIFICATION].

Each entry in `hook-signatures.json` names its `section`, read from the exe's section table
(`docs/raw-sources/hook-signature-sections-2026-11-17.md`), and architecture rule 15 fails a `.text1` hook with neither
flag. The rule lived only in a note until #153 shipped always-on dialogue window hooks there that no review caught (#167).

## Considered options

- **Ship `.text1` hooks on in Release once a session shows no reaction.** One quiet session says nothing about a later
  patch or another machine, and a crash there lands on a player who didn't choose it.
- **Keep the rule in review notes only.** #153 shows that fails.

## Consequences

A feature needing a `.text1` function finds another default route: the dialogue window moves to a read-only flag
(#232). A function only called through `Calls` (`sq_getstring`) isn't patched, so the rule skips it.
