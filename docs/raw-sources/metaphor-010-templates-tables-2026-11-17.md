# Metaphor 010 Editor templates: skill cost, affinity and equipment stat fields in the game's data tables (#155)

- **Source:** `DeathChaos25/metaphor-010-templates` at commit `cfccaa2f4e4352d2881884b1c7d61c53661259d3` (last push
  2025-02-02; no licence file, so nothing is copied, only field names, order and types), read through the GitHub REST API
  (`gh api .../contents/<file>`): `SkillNormal.bt`, `Skill.bt`, `ClassAffinity.bt`, `ItemArmor.bt`, `ItemWeapon.bt`,
  `ItemConsumable.bt`, `Class.bt`, `metaphor_structs.h`, `metaphor_enums.bt`. Nothing was downloaded from the game or
  run.
- **Saved:** 2026-11-17.
- **Game build:** the source names none, and whether its 2025-02-02 commit matches our build,
  `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018), is unknown, so every layout
  below is `[NEEDS VERIFICATION]` on it.

These are 010 Editor templates for the game's data tables (the files the game loads, not its memory). They give the
static half of several #155 values: what a skill costs, what an item does, what an archetype resists. The per-character,
per-moment half (who has what equipped, current MP) is in `menu-values-community-leads-2026-11-17.md`. Offsets marked
"computed" are added up from the declared field types; the template states only the order and types.

## Skill cost and where it can be used (`SkillNormal.bt`, `metaphor_structs.h`)

| Offset (computed) | Field | Type |
|---|---|---|
| 0x00 | flags | 16 one-bit flags |
| 0x02 | usable in | bits: 0 outside battle, 1 in battle, 3 back row only |
| 0x03 | scaling type | byte enum |
| 0x04 | range | byte enum |
| 0x05 | cost type | byte enum: 1 HP, 2 MP, 3 money |
| 0x06 | percent cost | u16 |
| 0x08 | value cost | u16 |
| 0x0A | turn icon cost | u16 |
| 0x0C, 0x0D, 0x0E | target count, side, condition | bytes |

`Skill.bt` is a separate 0x22-byte record per skill: element (byte), active or passive (byte), two 16-byte message
hashes. Skill IDs are a `ushort` enum (`SkillID`).

## Archetype affinities (`ClassAffinity.bt`)

One 0x26-byte record per archetype: 19 two-byte entries in this order: Slash, Pierce, Strike, Fire, Ice, Electric, Wind,
Light, Dark, Almighty, Poison, Malady, Paralysis, Charm, Daze, Forget, Sleep, Rage, Anxiety. Each entry is a multiplier
byte (units of 5 %, 20 = neutral) and a flags byte: bit 0 block, 1 repel, 2 drain, 3 weak, 4 resist, 5 ailment immune,
6 guaranteed ailment, 7 double ailment chance. The menu's 9-element grid matches the first nine.

## Equipment stats (`ItemArmor.bt`, `ItemWeapon.bt`)

Armor, 0x58 bytes per record after a `u16` count: icon (u32, 0x00), equippable users (bitfield, 0x04), three u16
unknowns, **DEF** u16 at 0x0E, **EVA** u16 at 0x10, then St, Ma, En, Ag, Lk as bytes from 0x12, a passive skill ID,
buy and sell prices (u32), rarity, three message hashes (computed).

Weapon: icon (u32, 0x00), users (0x04), three u16 unknowns, element (`short`, 0x0E), **ATK** u16 at 0x10, **HIT** u16 at
0x12, then St..Lk bytes, a passive skill ID, prices (computed).

Equippable users bit order (`EquippableUsers`): protagonist, Strohl, Hulkenberg, Heismay, Junah, Eupha, Fidelio, Basilio.

## Items and characters

- Consumable, 0x34 bytes: icon (u32), two u16 unknowns, **skill used** (`SkillID`, 0x08), a u16, buy and sell prices,
  two message hashes. An item's effect and targeting therefore come from its skill's `SkillNormal` record.
- `CharacterID` (`ushort`): 1 protagonist, 2 Strohl, 3 Hulkenberg, 4 Heismay, 5 Junah, 6 Eupha, 7 Fidelio, 8 Basilio,
  10 Grius.
- `Unit_Stats` (`metaphor_structs.h`): six bytes, St, Ma, En, Ag, Lk, reserved.
- `Class.bt`: 0x6C bytes per archetype; holds lineage, rarity, model and per-character availability, not rank or EXP.

## What this doesn't give

Current HP and MP, level, EXP, conditions, formation, tactics, item counts, equipped items, archetype rank and EXP, money
and Mag: all runtime state, not table data.
