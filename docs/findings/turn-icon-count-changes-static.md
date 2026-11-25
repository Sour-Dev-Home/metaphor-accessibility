# Turn icons change in an action's Participate::vf19: a full icon turns half, or a full or half icon is spent

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [battle-turn-icons-and-ui-components-static.md](battle-turn-icons-and-ui-components-static.md)
- **Functions:**
  - btl::action::Participate::vf19 | 0x1401e95b0 | .ecode | Battle | known only | Spends or converts turn icons after an action, through the three setters. | finding
  - SetTurnIconsA | 0x1403b4170 | .ecode | Battle | known only | Writes the `short` at `G+0x25464`. | finding
  - SetTurnIconsB | 0x1403b4130 | .ecode | Battle | known only | Writes the `short` at `G+0x25466`. | finding

For #695. The counts the turn-icon UI shows (A at `G+0x25464`, B at `G+0x25466`, C at `G+0x25468` or `+0x2546a` by
side) change during an action in one place, `Participate::vf19`, by three steps: A − 1 with B + 1, B − 1, and
A − 1 or C − 1. Read with the UI's total (B + 2 × (A + C), in half icons), the first is a full icon turned half (a
turn gained), the others an icon spent. That reading is a lead `[NEEDS VERIFICATION]`; a live watch settles it.

## Evidence

- **Writers.** `callers 0x3b4170` and `callers 0x3b4130`: battle set-up `0xc9580` and `0xc9680` (A = −1, B = 0, before
  a battle), `Participate::vf20` (`0x1ab9a0`) twice (A = a count, B = 0, C = a count: a turn's start),
  `Participate::vf19` (`0x1e95b0`) six or seven times, and `0x148bf0`, `0x219110`, `Finalize::vf2` (`0x229420`),
  `Series::vf1` (`0x2490d0`), `0x3d2540`, `0x5d40d0`, `0xa535d0`. The older note listed only the first three.
- **The patterns in `vf19`.** Four branches, each testing C (`0x3b0ee0`), B (`0x3b0ed0`) and A (`0x3b0f10`):
  - one step: C − 1 if C > 0, else B − 1 if B > 0, else A − 1 with B + 1 (a full icon turned half);
  - n steps (n from the action), each C − 1, else B − 1, else A − 1: n icons spent, halves first;
  - n steps when action flag bit 28 is set and the side is 1: each A − 1 with B + 1, else C − 1, else B − 1;
  - 2 × n steps when action flag bit 27 is set: twice the cost.

  Which branch a weakness, a critical, a miss or a block takes is `[NEEDS VERIFICATION]`: the conditions read action
  flags at `+0xc` and `+0x10`, not decoded here. The one-step branch fits press-turn's weakness rule and the doubled
  one a miss, as leads only.
- **The words.** The situation banners already include turn-icon lines (for example "Gained one Turn Icon!"), said by
  `BattleBanners` (#692, [battle-situation-banner-text-static.md](battle-situation-banner-text-static.md)). A feature
  that reads A, B and C after each hit can add the count left ("3 turns left") without repeating those words.
- `0x148bf0` both writes the counts and calls the banner show `0xf187d0`, but its decompile timed out; not read.

## Live check

Debug session with `opt-in-inspector.txt`. `G+0x25460` is `base+0x2ccbb50`.

```text
[player-turn-start]
# Side (int), A (short), B (short), C player (short), C enemy (short).
read base+0x2ccbb50 12
watch base+0x2ccbb50 12
[after-weakness]
# Expect A down 1, B up 1.
changes
[after-miss]
# Expect two icons spent, or B and A both down.
changes
[after-plain-hit]
changes
[enemy-turn]
changes
```

With the Debug log, each change also lines up with a `BattleHitResult` record and any kind-2 banner (`kind=2 id=…`).

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23.

1. `decompile` `0x3b4170`, `0x3b4130`, `0xc9580`, `0xc9680`, `0x1ab9a0`, `0x1e95b0`; `callers 0x3b4170`,
   `0x3b4130`. `decompile 0x148bf0` timed out in the decompiler.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
