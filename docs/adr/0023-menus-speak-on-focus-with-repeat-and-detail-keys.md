---
status: accepted
---

# Menus speak on focus, with keys to repeat, read more and say where you are

Decided by the maintainer on 2026-11-23, after a by-ear session, so every menu follows one rule.

- **On focus, automatically:** the focused row's essentials (name, key value, position), interrupting the line
  before, so holding a direction skims a list instead of queuing it. No chord is needed to hear a row.
- **On opening a screen or switching a tab:** its name once, then the focused row.
- **On demand, by context** (defaults; the player can rebind every key): repeat the focused row (`R`); read more,
  such as its description (`F`, `R3`); say where you are: screen, tab and position (`Shift+R`). In dialogue the
  repeat key repeats the last line. Pad bindings for repeat and where-am-I are open: never a button the game uses
  on that screen (not `X`, which combat uses).
- **Settings:** how much focus says (brief or full), the "n of total" suffix, and an optional tick per move.

## Considered options

- **Speak only on a playback chord:** a 30-row list would take 60 presses, and it isn't how screen readers work.
- **Queue every focus line:** fast scrolling falls behind.

## Consequences

Each menu screen needs a name and a short text per row; a long text is optional ("read more" then says "No more").
A screen that misses its screen or tab name, like the battle item tabs, is a bug against this ADR.
