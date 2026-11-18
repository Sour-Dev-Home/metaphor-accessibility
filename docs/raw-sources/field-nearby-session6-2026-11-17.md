# Nearby things in capture session 6 (#178)

- **Source:** capture session 6 (capture evening 2, #250): Debug mod from branch `capture-kit-2` at `1ff25fc` through
  Reloaded-II with `opt-in-field.txt`; the `field-near` record (`FieldWatch.cs`), defined in
  `docs/raw-sources/capture-2-field-leads-2026-11-17.md` on `capture-kit-2`: each non-empty list of the 21 in
  `fldUnitManager`, its count, the first unit's class, and its three nearest units by `+0x80` with id (`+0x9F8`),
  distance and world heading. Read from the session's `message-log.txt` (298 `field-near` records) with grep and awk,
  against the narration (walking past people, a door, a chest and enemies) and frames of the OBS recording. Log,
  narration and recording stay local (game text). Video time is log time − 161 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Findings

**The lists hold every unit of the map, by kind; "nearby" comes from sorting by distance.** 16 of the 21 lists were
non-empty somewhere, each with one class (`@fld@app`): `enemy` `fldEnemy`, `npc` `fldNpc`, `member` `fldMember`, `door`
`fldGmcDoor`, `treasure` `fldGmcTreasure`, `ladder` `fldGmcLadder`, `gather` `fldGmcGather`, `object` `fldGmcObject`,
`other` `fldGmcOther`, `scriptHit` `fldScriptHit`, `actionHit` `fldActionHit`, `gayaHit` `fldGayaHit`, `cameraBox`
`fldCameraBox`, `envBox` `fldEnvBox`, `mobGenerator` `fldMobGenerator`, `mobUnit` `fldMobUnit`. Counts are whole-map
(dungeon: 42 enemies, 130 objects, 8 doors; town: 91 or 140 people and 14–27 crowd units), set on load and stable
after, except enemies: 42 → 39 after the first battle and 39 → 37 after the second, so defeated enemies leave the list.

**Position (`+0x80`) works for the nearest-three sort.** Distances and headings changed smoothly while walking; the
nearest enemy fell to about 215 as enemies attacked (log ~848 s, a frame shows the player striking one), and the
nearest treasure to about 100 twice (log ~903 s and ~1110 s, the second at the narrated chest opening). Right after a
load, positions read `0.0` or `3000.0` for about a second before they are set.

**The id at `+0x9F8` is a small per-list index** (0 to count−1), stable for a unit across records. Not a name.

**Door model node: broken.** Every door's `node` distance read the same huge value (291 records): the node pointer or
offset is wrong `[NEEDS VERIFICATION]`. The door's own `+0x80` gave sane distances.

**Not found:** names (no field read gives a name), shops (no shop list among the 21; shopkeepers may be `fldNpc`), and
which `scriptHit` or `door` entries are exits `[NEEDS VERIFICATION]`. Whether a `fldNpc` is a person you can talk to is
not told by the list.

How sure: high that the lists, their kinds and positions are right for enemies, people, party members, doors and
chests; medium on the chest distances (frames around the chest were busy with a battle, so not checked by eye).

## Steps

1. Collapse `field-near` records on change of the list names and counts, with video time.
2. For log 800–1170 s, print the nearest `enemy`, `npc` and `treasure` entries every few records; match minima to the
   narration and to frames.
3. Count door `node` values.
