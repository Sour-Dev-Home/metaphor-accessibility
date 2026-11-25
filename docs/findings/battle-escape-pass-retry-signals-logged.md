# Escape and Pass already reach the player as situation banners; Retry and Auto Battle have no logged signal yet

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do; opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-situation-banner-text-static.md`](battle-situation-banner-text-static.md) and
  [`battle-turn-side-start-and-end-layouts-logged.md`](battle-turn-side-start-and-end-layouts-logged.md)
- **Functions:**
  - BTL_RewindComfirm vtable | 0x21c69f8 | .udata | Battle | known only | The Retry prompt's choice: `YesNoChoiceBase<RewindConfirmLayout>`, slot 11 the shared `ChoiceGetCursor`. | finding
  - BTL_Comfirm vtable | 0x21c7308 | .udata | Battle | known only | The battle's other yes/no prompts: `YesNoChoiceBase<BattleConfirmLayout>`, slot 11 the shared `ChoiceGetCursor`. | finding
  - btl::phase::Rewind vtable | 0x17db688 | .udata | Battle | known only | A battle phase class named for the retry; three slots. | finding
  - btl::phase::AllyEscape vtable | 0x17db538 | .udata | Battle | known only | A battle phase class named for the party's escape. | finding

For #256, #261, #262 and #257. Choosing Escape and Pass shows a situation banner (kind 2) the mod already says:
position 149 of `jyokyo_help_Message.msg` ("Escape") and 210 ("Pass to next ally"); a failed or impossible escape
has its own positions, 30 and 29, not yet seen in play. After each of five logged escapes `TurnInfoLayout` played
2570 ("Escaped", #166) about 0.7 s later; after a pass the turn icons drop a full one for a half one. Retry and Auto
Battle left nothing in five logs: no fight used them.

## Evidence

- **Positions.** The 2026-11-23 19.22 log compiles the file with 259 messages (`compile id=10`). Counting its labels
  from 0: `jyokyo_helpText_ESCAPE_IMPOSSIBLE` 29, `_ESCAPE_MISS` 30 ("Couldn't escape!"), `_ESCAPE` 149, `_PASS` 210.
- **Escape.** `BattleMessageShow kind=2 id=149` five times over four logs, each followed within 0.7 s by
  `TurnInfoLayout` anim 2570, then the field's layouts. No `id=30` or `id=29` with kind 2: no escape failed. Five
  `kind=0 id=29` records are another kind's id.
- **Pass.** `kind=2 id=210` 67 times over five logs. In the 2026-11-22 16.44 log the icons read `0,4,0` at a pass's
  banner and `0,3,1` at the next: a full icon turned half. A pass on a half icon reads `0,3,0` after. A second banner
  within a second logs `said=no` (the banners' repeat gap).
- **Confirm prompts.** The RTTI names `BTL_RewindComfirm@ope@ui@app` and `BTL_Comfirm@ope@ui@app`. Both vtables are
  yes/no choices whose slot 11 is `ChoiceGetCursor`, so the shared cursor hook would report their screen class, as it
  does `BTL_OrderTop`. No log has either class: no prompt was opened. `CommandBattle_Message.msg` (compile id 7) holds
  the prompts by label: `CommandBattle_HelpText_05` (retry), `_07` (pass) and `_08` (auto-battle). Which prompt
  `BTL_Comfirm` shows, and whether the game's "confirmations" setting is what opens it, is `[NEEDS VERIFICATION]`.
- **Phases.** The RTTI also names `btl::phase` classes `Rewind`, `AllyEscape`, `PinchEscape` and
  `AdvantageousEscape`. Where the battle keeps its current phase isn't traced `[NEEDS VERIFICATION]`; that would be the
  game's own identity for a retry (coding standard 5).
- **Auto Battle.** No layout, banner or class in the logs or the RTTI names it; the exe's only string is
  `ACTION_AUTO_BATTLE`. Its start, stop and tactic are `[NEEDS VERIFICATION]`. The Tactics list
  (`BTL_OrderTactics`, 6 rows) is spoken by the Order menu's focus (#771).

## Steps

Debug builds of the mod as deployed by `scripts/dev/session.ps1`, sessions of 2026-11-22 (13.05, 16.44, 22.50) and
2026-11-23 (13.52, 19.22); the logs stay local (never committed). In each `message-log.txt`:

1. `grep -o "via=BattleMessageShow kind=[0-9]* id=\(210\|29\|30\|149\) said=[a-z]*"`, counted.
2. `grep -n "kind=2 id=149 \|TurnInfoLayout@ui@app.*anim=257"`, read in order; the `icons=` field of the `id=210`
   records in the 16.44 log.
3. The `jyokyo_help_Message.msg` compile record's `@` labels, counted from 0; the `CommandBattle_Message.msg` labels.
4. `grep -o "screen=BTL_[A-Za-z_]*@ope@ui@app count=[0-9]*"` over all logs, counted.

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23, one run of 5 queries:
`vtable` of `app::ui::ope::BTL_RewindComfirm`, `app::ui::RewindConfirmLayout`, `app::ui::ope::BTL_Comfirm`,
`btl::phase::Rewind` and `btl::phase::AllyEscape`. The class names came from the exe's RTTI type names
(`.?AV...@ope@ui@app@@`, `...@phase@btl@@`). Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
