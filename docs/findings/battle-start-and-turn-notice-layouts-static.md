# The battle-start and turn banners are two layouts, BattleNoticeLayout and TurnInfoLayout, picked by animation number

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [battle-situation-banner-text-static.md](battle-situation-banner-text-static.md),
  [ui-component-open-points-static.md](ui-component-open-points-static.md) and
  [battle-turn-icons-and-ui-components-static.md](battle-turn-icons-and-ui-components-static.md)
- **Functions:**
  - BattleNoticeLayout::vf20 | 0x140aea030 | .ecode | Battle | known only | Opens the notice: animation 0xa66 or 0xa6d by a flag at `+0x470`, plus 1 when the turn side is 1.
  - TurnInfoLayout::vf20 | 0x140aea7c0 | .ecode | Battle | known only | Opens the turn panel: animation 0xa0a, 0xa0b, 0xa0c, 0xa23, 0xa24 or 0xa25 by flags at `+0x8d40`, `+0x8d43`, `+0x8d78`.
  - btl::phase::Beginning::vf2 | 0x140227b60 | .ecode | Battle | known only | Starts each round: counts a player turn when the side is 0, an enemy turn otherwise, then moves to `Fight`.

For #626. The pre-emptive and enemy-turn banners are not `UUIC_Battle_Message` banners, so #692's kind-2 reading (and
issues #251 and #254) don't cover them. They are layouts that open through the shared start-an-animation `0xa6ffd0`, which the
Debug `ui-anim` record (#691) already logs with the layout's class and animation number. The flag is the turn side at
`G+0x25460`; the text, if any, is not found statically. So this needs a live log, then wiring, not more static work.

## Evidence

- **The side is the turn's owner.** `Beginning::vf2` (`0x227b60`) reads `0x3b0f80` (`G+0x25460`): on 0 it adds one to
  the package's `+0x2d8` and calls `0x1400c78f0` on the elapsed-player-turn object `0x27293b0`
  (`BtlGetElapsedPlayerTurn`'s); otherwise it adds one to `+0x2dc`. So side 0 is the player's turn, and nonzero the
  enemy's. High: a static reading of one branch.
- **BattleNoticeLayout** (vtable `0x21691f8`, constructor `0xae07f0`). `vf20` reads the side with `0x3b0f80` and opens
  animation `0xa66 + (side == 1)` when its byte `+0x470` is set, else `0xa6d + (side == 1)`, then sets a second part
  to `0xa74 + (side == 1)`. Its `vf17` binds a text part (`parts_msg_text`, `text_area`, `font_rect_C4_message`), so it
  may carry a message too `[NEEDS VERIFICATION]`. That it is the pre-emptive or ambush notice is a lead.
- **TurnInfoLayout** (vtable `0x21675f8`, constructor `0xae2b00`). `vf20` opens one of six animations by its bytes
  `+0x8d78`, `+0x8d43`, `+0x8d40`; its `vf17` binds icon and face parts (`icon01` to `icon08`, `mm01` to `mm08`,
  `(tmp)_parts_kingface`) and no text part. That it is the player-turn or enemy-turn panel is a lead.
- **Not phase transitions.** `AdvantageousTransition` and `PinchTransition` are made only by `Fight::vf2`
  (`0x228160`), mid-fight, and their `vf2` goes back to `Fight`; they aren't the start banners. What they are is
  `[NEEDS VERIFICATION]`.
- **The package flags.** `Beginning::vf2` sets bit 1 of package `+0x248` and, when `+0x260` is 1 with no `+0x6e8`,
  starts a screen fade. Which package bit marks a pre-emptive start is not found `[NEEDS VERIFICATION]`. The string
  `PREEMPTIVE_ATTACK` (`0x17c494e`) has no direct reference in analysed code.

## Live check

Debug build, no opt-in needed for the log. Fight three battles: one started by striking the enemy first, one where
the enemy strikes first, and one plain. In each, note the `ui-anim` records with `class=BattleNoticeLayout@ui@app` and
`class=TurnInfoLayout@ui@app` and their `anim=` numbers at the start and at each turn change. With
`opt-in-inspector.txt` also on, read the side at each:

```text
[battle-start]
# The turn side (int): 0 player, nonzero enemy (static reading); then icon counts A, B, C.
read base+0x2ccbb50 12
```

Each (class, animation) pair then maps to its words in the feature; a pair that turns up with a `resolve` record in
the same millisecond has text to read instead.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23.

1. `vtable` `btl::phase::Beginning`, `btl::phase::AdvantageousTransition`, `btl::phase::PinchTransition`,
   `app::ui::UUIC_Battle_TacticalSituation`, `app::ui::TurnInfoLayout`, `app::ui::BattleNoticeLayout`.
2. `decompile` `0x227b60`, `0x227600`, `0x22bcd0`, `0x250c20`, `0xf26810`, `0xf26b90`, `0xaea030`, `0xaef2d0`,
   `0xaea7c0`, `0xaf4270`.
3. `global` `0x17db490`, `0x17db420`, `0x21675f8`, `0x21691f8`, `0x17c494e`; `callers 0xf187d0`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
