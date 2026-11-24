# In a battle, the game's Retry ("turn back the clock") is on L3

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do (played by ear with the mod loaded)
- **Sources:** none, see Steps; answers the `[NEEDS VERIFICATION]` on whether the game uses L3 in a battle, from
  [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md)

The game uses L3 in a battle: it is the button for Retry, which turns back the clock (#262). So the mod's status read
(#631) keeping L3 from the game took Retry away from pad players (#925).

## Evidence

The maintainer's by-ear session of 2026-11-24 (#925): with the mod's `battleHits` on, L3 in a battle said the party's
HP and kept Retry from the game, as the issue's report of that session records. Whether L3 does anything else in a
battle is `[NEEDS VERIFICATION]`.

## Steps

1. Build the mod at 2fdf9f63 (fe67a150 with #917 merged), Debug, and deploy it with `scripts/dev/session.ps1 deploy`
   to the game build above.
2. Start a battle with a pad, the default settings on, and press L3.
