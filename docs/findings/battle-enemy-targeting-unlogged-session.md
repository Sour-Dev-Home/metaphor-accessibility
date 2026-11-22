# Choosing an enemy target logs no cursor record, and an enemy's hit carries its max HP

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** none, see Steps; builds on
  [`battle-target-cursor-index-static.md`](battle-target-cursor-index-static.md)

In the 2026-11-22 session's message log (Debug build of #631), moving the target cursor over the enemies logged only pad
reads: no `cursor`, `selected` or other record names the targeted enemy, as the static note expects of a cursor with no
cursor manager. So the targeted enemy is still `[NEEDS VERIFICATION]` (#631). Each enemy's `BattleHitResult` request
did log a max HP (`hpMax`) beside its HP left, so the details key can say "of max" for enemies.

## Evidence

- **Targeting.** Between the battle's last list cursor record and the first enemy hit, the log holds about 25 `input
  pad0` reads (buttons `0x8000` pressed and released, then `0x1000`) and nothing else but the always-on `pane` and
  `register` records. That these reads are the target choice, not the command panel, is inferred from their place
  just before the hit `[NEEDS VERIFICATION]`.
- **Max HP.** All 8 enemy-side `request via=BattleHitResult` records give `hpMax`, such as `hp=-360 hpLeft=840
  hpMax=1200`: three enemies with different maxima in one action.
- **Leads left.** The static note's slot-4 index (a vtable swap the maintainer hasn't approved) and session 5's
  `UnitAffinity` unit argument. The details key now logs each known enemy's record address on every press
  (`request via=BattleDetails`, Debug builds) to match against either.

## Steps

1. Debug build of branch `247-battle-hits-lists` with main merged, 2026-11-22 session, one battle with three enemies
   targeted by pad.
2. In the session's `message-log.txt`: `grep -c "side=Enemy"`; then the records before the first enemy hit, without
   `pane`, `register`, `cue` and text lines.
