# Field actors: the unit manager's lists of NPCs, doors, chests, enemies and the rest (#144, static leads)

- **Source:** our own reading of the game executable on disk, as shipped (ADR 0002), without running the game: a
  throwaway local disassembler (Iced 1.21.0, the NuGet package, on .NET SDK 9.0.319, in a scratch project outside the
  repository) that also maps MSVC run-time type information (RTTI) names to vtables as in
  `ui-focus-candidates-2026-11-16.md`, and `tools/Metaphor.SignatureCheck --probes` (this repository at the commit that
  adds this file). Nothing was patched, unpacked or run. Script-function addresses come from
  `squirrel-script-functions-all-2026-11-16.md`. Community source, read through the GitHub API: `NM-20/Resonant` at
  `6b90020b2a99175e25ea7cfc0ae2dc4efc987c89` (2026-08-02, GPL-3.0), `program/app/field/new/unit/fldUnitManager.h` and
  `program/app/unit/base/UnitList.h`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

RVAs in this build. Identifiers and offsets only (ground rule 4). Nothing here has been seen in a running game, so every
runtime claim is `[NEEDS VERIFICATION]` until session 6. The world singleton and the player are in
`field-position-2026-11-16.md`.

## What was checked first (ground rule 3)

Resonant's `fldUnitManager` is `fldPlayer* p_player` followed by 21 `app::unit::UnitList<T>`, in this order: `fldEnemy`,
`fldMember`, `fldNpc`, `fldGmcGem`, `fldGmcTreasure`, `fldGmcDoor`, `fldGmcObject`, `fldGmcGather`, `fldGmcOther`,
`fldGmcLadder`, `fldActionHit`, `fldCameraBox`, `fldEnvBox`, `fldMobGenerator`, `fldMobUnit`, `fldScriptHit`,
`fldGayaHit`, `fldDbgPosJump`, `fldDbgSceneJump`, `fldDebugWarp`, `fldVehicle`, then `0x10` bytes. A `UnitList` is a
vtable, a `u32` id counter, a vector of `T*` (`m_units`) and a second vector (`m_unitsToDestroy`), `0x40` bytes. No other
public source for the field's actor lists was found (see `field-position-2026-11-16.md`).

## Findings

**The lists are `[[world]+0x20]` (`fldUnitManager`, `0x558` bytes).** The `fldWorld` destructor frees `+0x20` with size
`0x558`. That object's destructor (`0x68D7A0`) writes vtable `0x1808E48`, RTTI
`UnitList<fldVehicle>`, at `+0x508`, and the source path naming `fldUnitManager.cpp` sits right after that vtable
(`0x1808E6F`). So `0x08 + 21 × 0x40 = 0x548`, plus `0x10`, is `0x558`, as Resonant has it.

Each list's `m_units` vector is `begin` at list `+0x10`, `end` at `+0x18`. These script functions walk them from
`begin` to `end`, each element a unit pointer, and match the unit's id at unit `+0x9F8`:

| List (Resonant) | `begin`/`end` in the manager | Read by |
|---|---|---|
| player (one pointer) | `+0x00` | `FldPlayerGetDegToEnemy` `0x8E5070` |
| `fldEnemy` | `+0x18` / `+0x20` | `FldPlayerGetDegToEnemy` |
| `fldMember` (party followers) | `+0x58` / `+0x60` | `FldMemberGetDegToPlayer` `0x8E0600` |
| `fldNpc` | `+0x98` / `+0xA0` | `FldNpcGetDegToPlayer` `0x8E2D00`, `FldNpcGetCharaID` `0x8E2CC0`, `FldNpcSetDeg` `0x8E4370` |
| `fldGmcDoor` | `+0x158` / `+0x160` | `FldDoorGetUidByClosestOnPlayer` `0x8DAD90`, `FldDoorGetType` `0x8DAD00` |
| `fldGmcObject` | `+0x198` / `+0x1A0` | `FldObjGetUidByLayoutName` `0x8E4D50` |
| `fldGmcGather` | `+0x1D8` / `+0x1E0` | `FldGatherGetUidByLayoutName` `0x8DE460` |
| `fldGmcOther` | `+0x218` / `+0x220` | `FldGmcGetUidByLayoutName` `0x8DEF10` |
| `fldGmcLadder` | `+0x258` / `+0x260` | `FldLadderGetUidByClosestOnPlayer` `0x8DFC80` |
| `fldActionHit` | `+0x298` / `+0x2A0` | `FldActionHitGetUidByLayoutName` `0x8D7780` |
| `fldScriptHit` (script trigger volumes) | `+0x3D8` / `+0x3E0` | `FldHitCollisGetUidByLayoutName` `0x8DF9F0` |
| `fldGayaHit` | `+0x418` / `+0x420` | `FldGayaHitGetUidByLayoutName` `0x8DE5B0` |

Every offset we read matches Resonant's order at `0x40` per list. The lists we did not see read (`fldGmcGem` `+0xD8`,
`fldGmcTreasure` `+0x118`, `fldCameraBox` `+0x2D8`, `fldEnvBox` `+0x318`, `fldMobGenerator` `+0x358`, `fldMobUnit`
`+0x398`, `fldVehicle` `+0x518`) follow from the same spacing and are `[NEEDS VERIFICATION]`. RTTI names each element
class (`fldNpc` vtable `0x18085E8`, `fldEnemy` `0x1807838`, `fldGmcDoor` `0x18090E8`, `fldGmcTreasure` `0x1809DE0`,
`fldGimmick` `0x1808EC8`, `fldObject` `0x180A178`, `fldScriptHit` `0x180B140`), so the list a unit came from is its kind.

**Position of each unit.** Characters (player, NPC, enemy, member) keep it at unit `+0x80`, as in
`field-position-2026-11-16.md`. `FldDoorGetUidByClosestOnPlayer` gets each door's position another way: it calls
`0x96F820` on `door+0x10`, which takes the pointer at `+0x48` (or, if null, `+0x20`) of that component and returns the
floats at its `+0x50`, `+0x54`, `+0x58` (zero if both are null). It then keeps the door with the smallest squared
distance to `player+0x80`, and returns that door's id at `+0x9F8`. That function is a ready-made template for a
"nearest of kind" query. Whether gimmicks' `+0x80` is also kept current is `[NEEDS VERIFICATION]`.

**Names and ids.**

- NPCs: `FldNpcGetCharaID` ends in `0x67A380` and `FldNpcGetNpcID` in `0x67A650`; both switch on `npc+0x3630` (values
  0, 1, 5, 6, 7 and more seen) to decide where the id comes from. The chara id is the key for the speaker-name table in
  `name-lookup-candidates-2026-11-16.md` `[NEEDS VERIFICATION]` that the same table names field NPCs.
- Gimmicks (doors, objects, gather points, others): `0x697A70` (`FldGimmickLayoutName`, `const char* (gimmick*)`) reads
  `[[world]+0x38]` (Resonant: `fldBgManager`), passes it to `0x576840`, then calls `0x577F10` with that result and the
  index at `gimmick+0x2888`, and returns a string. The `Fld*GetUidByLayoutName` functions compare it with `strcmp`, so it
  is the gimmick's layout name: an internal key, not display text. A spoken name would need our own table keyed on it,
  or on the door's link row below.
- Doors: `FldDoorGetLinkFlag_ED` (`0x8DABB0`) calls `0x699C00`, which takes the row index at `door+0x28C4` (`-1` means
  none) and looks it up with `0x591B50` in a table at `fldDatabase+0x109B8` (`fldDatabase` is in
  `field-state-and-map-2026-11-16.md`); it returns the row's `+0x58`. RTTI names `fldDoorLink` and `fldMapLink` among the
  field's data tables, which is where a door's destination map would be `[NEEDS VERIFICATION]`. `FldDoorGetType`
  (`0x698C10` → `0x6982E0`, `0x8844F0`) was not followed further.

**Not found:** whether a unit is in the current map or streamed out (beyond being in a list), and display names for
doors, chests and exits. Interaction prompts (the field's action guide, `UUIC_Field_ActionGuide`, and the player's
"in front of" checks `FldDoorIsPlayerInFront` `0x8DB020` and `FldGmcIsPlayerInFront` `0x8DF0C0`) are the next leads
for "what can I use here".

## Byte-pattern probes

`tools/Metaphor.SignatureCheck/field-probes.json`, each one match on disk in this build:
`FldDoorGetUidByClosestOnPlayer` (`0x8DAD90`), `FldNpcGetDegToPlayer` (`0x8E2D00`), `FldGimmickLayoutName`
(`0x697A70`). The list offsets are immediates inside them (`+0x158`/`+0x160`, `+0x98`/`+0xA0`, `+0x2888`).

## To confirm in session 6

Read only.

1. In a town, read the manager (`[[base+0x2EE7510]+0x20]`) and each list's `begin`/`end`. Count NPCs (`(end − begin)/8`)
   and compare with the NPCs on screen; walk into another district and see it change.
2. For each NPC, check its first qword is `base+0x18085E8`, read `+0x80` (position) and `+0x9F8` (id), and call nothing:
   compute distances to the player and check the nearest matches the one beside you.
3. Do the same for doors (`+0x158`): check the `+0x10` component's node position against the door you stand at, and log
   `FldGimmickLayoutName`'s string only to the local log (it is a layout key; don't commit captured strings).
4. Read the lists we only inferred (`+0xD8`, `+0x118`, `+0x398`, `+0x518`) next to a chest and in a dungeon.
