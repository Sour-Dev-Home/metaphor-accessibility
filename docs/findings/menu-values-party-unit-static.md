# Most missing menu values sit in the party unit, the game-data block or the battle tables

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`menu-values-session5-2026-11-17.md`](../raw-sources/menu-values-session5-2026-11-17.md),
  [`menu-values-community-leads-2026-11-17.md`](../raw-sources/menu-values-community-leads-2026-11-17.md),
  [`metaphor-010-templates-tables-2026-11-17.md`](../raw-sources/metaphor-010-templates-tables-2026-11-17.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - unnamed, at 0x3b0eb0 | 0x3b0eb0 | .ecode | Menus | known only | Returns the address of party unit `n` in the game data block. | finding; docs/findings/battle-target-cursor-index-static.md
  - btl::phase::Finalize::vf2 | 0x229420 | .ecode | Battle | known only | Loops over the party at the battle's end and sets each unit's HP and MP. | finding; docs/findings/battle-target-cursor-index-static.md

Static prep (#493) for game session 2 (#497), for the values #155 still lacks after session 5. Read in Ghidra and the
exe's bytes only, not yet run against the game: every meaning below is `[NEEDS VERIFICATION]` until the session's reads
agree. The script getters session 5 left (`UnitGetLevel`, `UnitGetMaxHP`, `GetClassLevel`'s lookup) jump into `.text1`
and stay unfollowed, but their readable siblings name most fields. The batch files are in
[`docs/sessions/session-2/`](../sessions/session-2/).

`G` is the game-data block at `base+0x2ca66f0` ([money note](money-follower-rank-and-shop-static.md)). Unit `n` is
`G+0x228 + n × 0x16A8` ([battle note](battle-turn-icons-and-ui-components-static.md)), and `n` is the character ID:
`CharaGetEquip`, `SetBattlePartyFormation` and `CharaRecoveryBadStatus` pass their character argument straight to the
unit getter `0x3b0eb0`. So unit 1 is `base+0x2ca7fc0`.

| Value | Best lead | Confidence |
|---|---|---|
| HP, MP (current) | unit `+0xc`, `+0x10`, `int` | High (session 6 checked them in battle) |
| HP, MP (max) | computed in `.text1`; no field | High that there's no static field |
| Level, EXP to next | unit `+0x20` to `+0x93`, by sweep | Low |
| Conditions | unit `+0x1c`, bit mask; `0x80000` is knocked out | High for the field |
| Front/Back | unit `+0x0` bit 22 (`0x400000`) | High for the bit; which side is set is a lead |
| Tactics | unit `+0x163c`, `u16` | Medium |
| Membership | unit `+0x0` bit 0; battle party at `G+0x1c748`, 4 `int`s | Medium; high for the battle party |
| Mag | `G+0x233fc`, `int` (`base+0x2cc9aec`) | High |
| Skill cost, kind | skill table `[base+0x2dff9a0] + id × 0x30`: `+0x5` kind, `+0x6` percent, `+0x8` flat | High in code |
| Skill list | `base+0x2ed0310 + id × 0x58`, 22 `int`s | Medium |
| Skill usable | cost against current HP or MP; table `+0x2` per the template | Medium |
| Item focused | camp Item scene (key 2) `+0x90` | Medium |
| Item usable per target | the item's skill's target fields, and the target's HP and conditions | Low |
| Equipped item per slot | unit `+0x1628 + slot × 4`, 5 `int`s | High |
| Focused item's stat change | weapon table `[base+0x2ebea98]`, armour `[base+0x2ebea68]`; the focused ID is unknown | Medium for the tables, low for the focus |
| Current archetype | unit `+0x94`, `u16` | Medium-high |
| Archetype rank, EXP | archetype records at unit `+0x98 + i × 0x5c`; rank at record `+0x8` | Medium for rank, low for EXP |
| Stat bonuses | per equipped item: weapon `+0x14 + stat`, armour `+0x12 + stat` | Medium |

## Evidence

### 1. The party unit

Every readable function on the unit uses these fields:

- `+0x0` flags. Bit 0: `0x3b3230` returns it for a character, and `btl::phase::Finalize::vf2` (`0x229420`) loops over
  characters 1 to 18 with it before copying battle state back, so it reads as "present". Bit 22: `SetBattlePartyFormation`
  (`0x9103a0`) calls `0x3e8a50`, which sets `0x400000` when the script's second argument is 0 and clears it otherwise;
  `Finalize` writes it back after battle. Bits 25 and 26 quarter and halve skill costs (section 3).
- `+0x4` a `short` kind. 1 for party members (`0xf2a180` reads the character ID only when it is 1); kind 2 skips the
  999 caps below.
- `+0x8` the character ID (`0xf2a180`, `0x3e8980`).
- `+0xc` HP and `+0x10` MP: `0x3e8a00` and `0x3e8a70` set them, capped at 999. `Finalize` sets HP from `0x46555e0` (the
  target of `UnitGetMaxHP`'s thunk) and MP from `0x46578e0`, both in `.text1`, so the maxima are computed, not stored.
  `0x3e3c20` adds to `+0x10` clamped to `0x46578e0`'s result.
- `+0x14` and `+0x18`: `int`s that `0x3e39e0` and `0x3e3a40` add to, clamped to 0 to 999. Meaning `[NEEDS VERIFICATION]`.
- `+0x1c` conditions, a bit mask. `0x3e4010` tests it, `0x3e4310` clears bits (`CharaRecoveryBadStatus`, `0x903ee0`,
  clears `0xffffff`). Bit 19 (`0x80000`) is knocked out: `0x384290` treats it, or HP at or below 0, as down, and
  `0x3e87e0` clears it and sets HP to 1. After battle `Finalize` clears every bit but 0 to 5 and 19, so bits 0 to 5 may
  be the conditions that outlast a fight `[NEEDS VERIFICATION]`.
- `+0x94` the current archetype, a `u16` (section 6).
- `+0x1628` equipment (section 5), `+0x163c` the tactics candidate (below).

**Level and EXP.** No readable code was found reading them. `UnitGetLevel` (`0x912a30`) jumps to `0xa0c8670` in `.text1`.
`HumanParamGetLevel` (`0x90aca0`) is not the character level: `0x3b3610` compares a `short` at `G+0x24668 + id × 2`
with four thresholds from the table `0x8990e0` returns, giving 1 to 5, the shape of a royal virtue's rank. The unit's
unexplained bytes between `+0x20` and `+0x93` are the lead: the session reads them and looks offline for the level and
EXP the Status screen shows. `0x3e4370` zeroes `+0x40` to `+0x43` and bytes from `+0x44` on (battle buffs, likely), so
`+0x20` to `+0x3f` is the likelier half. Confidence: low.

**Tactics.** `0x3e8a40` writes a `u16` at `+0x163c` and `0x3e65d0` reads it. `0xf04130` sets it on all 19 units at once,
`SetBattleParty`'s helper (`0x3b3f90`) copies another unit's value (its index is obfuscated) to a member joining the
party, and `Finalize` copies it
back from the battle's per-character array. A per-member setting that battle can change fits the tactics setting.
Confidence: medium.

**Membership.** The battle party is 4 `int`s at `G+0x1c748` (`base+0x2cc2e38`): `GetBattleParty` (`0x907830`) returns
slot `n` through `0x3b0e30`, and `0x3b3f90` refuses an ID already in a slot. `IsInParty` (`0x90b150`) jumps into
`.text1`; unit bit 0 is the readable candidate for "in the party at all". Confidence: high for the slots, medium for the
bit.

### 2. Mag

`SetMag` (`0x910480`) jumps to `0x3b3e70`, which stores its argument at `G+0x233fc`, capped at 9,999,999. `AddMag`
(`0x903650`) jumps to `0x3b00c0`, which adds to the same field with the same cap. Confidence: high.

### 3. Skill cost, kind and usability

`0x3e13f0` returns `[base+0x2dff9a0] + id × 0x30` (a fixed entry for ID 0). The pointer is written once, by `0x3dd7b0`,
the loader that opens the `COMMON/battle/table/*.TBL` files (`SkillNormal.TBL` among them); it copies 48,000 bytes,
1,000 entries.

`0x3837d0(unit, mode, skill)` computes the cost; `0x387c20` calls it with mode 0 and `0xf2aec0`, a menu accessor, calls
that. By the entry's byte `+0x5`:

- 1, HP: max HP × `u16 +0x6` / 100 + `u16 +0x8`; a passive `0x496` takes 10 off the percentage.
- 2, MP: `u16 +0x8` + `u16 +0x6`, or, when bit 4 of byte `+0x0` is set, max MP × `+0x6` / 100 + `+0x8`.
- 3: `+0x6` + `+0x8`, cut by the passives `0x497` to `0x499`.

With mode 0, unit bit 26 halves the result and bit 25 quarters it; a cost never drops below 1; unit bit 24 makes it 0.
This matches the 010 template's `SkillNormal` (`+0x05` cost type 1 HP, 2 MP, 3 money; `+0x06` percent; `+0x08` value),
so the template's `+0x02` "usable in" byte (bit 0 outside battle) is the lead for usability, with the cost compared to
the unit's current HP or MP. Confidence: high for the cost code; medium for usability.

**Which skills.** `0x3e6c80(unit, archetype)` fills a buffer at `base+0x2ed0310 + id × 0x58`: up to 22 skill IDs, from
`.text1`'s `0x3b23da0(id)`, then the accessory's (slot 3, `+0x1634`) skills when its item type is `0x35`. Whether the
Skill screen lists that buffer is `[NEEDS VERIFICATION]`; reading it right after the screen opens tests it.

### 4. Item usability per target

The camp Item screen's scene is key 2 in the menus' scene map, `fclCampItemMenu@fcl@app`, and `FclShopGetItemID`
(`0x906720`) reads its focused item ID at `+0x90` ([tree note](archetype-tree-scene-and-choice-static.md)). No readable
function decides usability. The lead is the template's: a consumable names its skill at `+0x08`, and the skill's
target fields (`+0x0C` to `+0x0E`) say who it can reach; the game then refuses a heal on full HP or a revive on a member
whose `+0x1c` lacks bit 19. Where `ItemConsumable.TBL` lands is not found: `0x3dd7b0` loads it, but its global wasn't
traced. Confidence: low.

### 5. Equipment

`CharaGetEquip` (`0x903ec0`) calls `0x3e5f40(unit, slot)`, which returns `unit + 0x1628 + slot × 4`. `0xf02f00` walks a
`StaticArray<datUnitEquipType,5>`, so there are five slots. `0x3e6680(unit, stat)` adds, to a base stat from `.text1`
(`0x4639220`), `0x3bc940(item, stat)` for the slots at `+0x1628` to `+0x1634`. `0x3bc940` picks a getter by `ID >> 12`:
category 0 is `0x3bbce0`, a byte at `+0x14 + stat` of `[base+0x2ebea98] + index × 0x90` (`0x3e13a0`); category 1 is
`0x3bb650`, a byte at `+0x12 + stat` of `[base+0x2ebea68] + index × 0x58` (`0x3e12e0`). The armour layout matches the
template (`0x58` bytes, St from `0x12`), so category 0 is likely weapons and its ATK the template's `u16 +0x10`.
Categories 2 and 3 (`0x3bbb30`, `0x3bb4e0`) weren't read.

The focused item's change is computed by the screen; the focused ID's location is `[NEEDS VERIFICATION]`. "Who wears it"
is a scan of every unit's slots. Confidence: high for the slots, medium for the tables.

### 6. Archetype rank, EXP and stat bonuses

`0x3e4570`, `0x3e4590`, `0x3e65f0`, `0x3e6c30` and `0x3e77d0` pass the unit's `u16 +0x94` as the archetype to
`0x3e45a0` (an affinity: it returns 100, session 5's neutral, for elements `0x13` to `0x15`) or `0x3e6c80`.

`0x389ef0(id, archetype)` searches 60 records of `0x5c` bytes from unit `+0x98` for one whose byte `+0x0` has bit 0
set and whose `int +0x4` is the archetype; `PlayerSetterAccess::SetClass` (`0xf03d40`, by its error string) uses it.
`GetClassLevel` (`0x9078f0`) returns byte `+0x8` of a record from `0x3b2bdc0(id, archetype)` in `.text1`. That this
lookup returns the unit's own record, so the rank is at unit `+0xa0 + i × 0x5c`, is the lead; `0x3e8980` writes four
`int`s at `+0x48` of the same lookup's record. Archetype EXP is likely in the record too `[NEEDS VERIFICATION]`.

The archetype table is static: `0x3e1060` returns `base+0x2ebf500 + id × 0x6c`, the template's `0x6C`-byte `Class`
records. Stat bonuses from equipment are section 5's per-item bytes; session 5's `UnitGetParam` totals are the check.

## Tonight

Start with `-OptIn inspector,ui-text-panes` (#497 stop 0). At each stop run its batch file, which holds the inspector's
one connection until it ends; the follow-ups come after it:

```powershell
pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-2/stop-1-status.txt -WaitKey -SaveLog <out>
```

`<out>` is a folder outside the repository. Each batch's comments say what each line should show.

| Stop | Batch file | Follow-ups and the answer that confirms |
|---|---|---|
| 1, Party and Status | `stop-1-status.txt` | `follow` prints `base+0x2ca66f0`. Unit 1's `+0xc` and `+0x10` match HP and MP; `+0x8` is 1; the party ints are character IDs. Mag matches the main menu. Offline: the shown level is a byte or `short`, and the EXP to next a difference of two `int`s, in `+0x20` to `+0x93` (rejects the lead if absent). Front/Back: bit 22 flips on the toggled unit only. Tactics: `+0x163c` changes on the tactics change only |
| 2, Skill | `stop-2-skill.txt` | The buffer holds IDs under 1000. Then `read <table>+id*0x30 0x30` for two of them: an MP skill's `+0x5` is 2 and `+0x8` (plus `+0x6`) its shown cost; an HP skill's `+0x5` is 1 and its cost is max HP × `+0x6` / 100 + `+0x8`. A greyed skill costs more than the unit's current MP or HP, or has `+0x2` bit 0 clear |
| 3, Item | `stop-3-item.txt` | `read <node>+0x10 4` is `02 00 00 00`, `rtti <scene>` is `fclCampItemMenu@fcl@app`; `watch <scene>+0x90 4`, move two rows, `changes`: one item ID (`0x4000` and up, session 5) per move. The healed unit's HP rises |
| 4, Equipment | `stop-4-equipment.txt` | Each unit's 5 `int`s are item IDs whose `ID >> 12` differs by slot; the swap changes one. Then `read <weapon table>+(ID & 0xfff)*0x90 0x90` and `read <armour table>+(ID & 0xfff)*0x58 0x58`: the template's ATK (`+0x10`) and DEF (`+0x0e`) match the screen |
| 5, archetype details | `stop-5-details.txt` | `+0x94` is an archetype ID; one record in the dump has `+0x4` equal to it and bit 0 of `+0x0` set; its `+0x8` is the shown rank (rejects the lead if not). Offline: the rank's EXP to next is an `int` near it |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says
otherwise. Script function addresses come from the registration list.

1. `decompile 0x912a30` (a jump into `.text1`, stopped there), `0x90aca0`, `0x3b3610`, `0x903ec0`, `0x903ee0`,
   `0x9103a0`, `0x912940`, `0x3b0eb0`, `0x3e5f40`, `0x3e4310`, `0x3e8a50`, `0x3e8a00`, `0x3e8a70`, `0x3e39e0`,
   `0x3e3a40`, `0x3e3c20`, `0x3e4010`, `0x3e60e0`, `0x384290`, `0x3e87e0`, `0x3e4370`, `0x3b3230`, `0x3e8080`,
   `0x3e8a40`, `0x3e65d0`, `0xf04130`, `0xf2a180`; `callers 0x3b0eb0`; `decompile 0x229420` (the formation, tactics and
   maxima writes).
2. `decompile 0x910480`, `0x3b3e70`, `0x903650`, `0x3b00c0`, `0x907830`, `0x3b0e30`, `0x910390`, `0x3b3f90`.
3. `decompile 0x3e13f0`, `0x3837d0`, `0x387c20`, `0xf2aec0`, `0x3e77d0`, `0x3e6c80`; `global 0x2dff9a0` (one write, in
   `0x3dd7b0`); `decompile 0x3dd7b0` (the table file names and the 48,000-byte copy).
4. `decompile 0x906720` (via the tree note); `global 0x2c01300` (`fclCampItemMenu`'s type descriptor).
5. `decompile 0xf02f00`, `0x3e6680`, `0x3bc940`, `0x3bbce0`, `0x3bb650`, `0x3e13a0`, `0x3e12e0`, `0xf2f720`.
6. `decompile 0x9078f0`, `0x38ae40`, `0x389ef0`, `0xf03d40`, `0x3e8980`, `0x3e4570`, `0x3e4590`, `0x3e65f0`,
   `0x3e6c30`, `0x3e45a0`, `0x3e1060`; `offset 0x94` (the readers above).

Nothing in `.text1` was decompiled: `0xa0c8670`, `0x46555e0`, `0x46578e0`, `0x4639220`, `0x3b2bdc0`, `0x3b23da0` and
`IsInParty`'s target were noted and not followed. The class, file and function names are the exe's own; no game text
is quoted.
