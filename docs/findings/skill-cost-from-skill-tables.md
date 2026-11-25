# A skill list row's cost can be read offline: its name label's MD5 finds the skill id in `Skill.TBL`, the cost in `SkillNormal.TBL`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's data files from `base.cpk` offline, as
  [`objective-map-icon-points-static.md`](objective-map-icon-points-static.md) did (no ADR 0002 row covers data files,
  which no protection guards)
- **Sources:** [`metaphor-010-templates-tables-2026-11-17.md`](../raw-sources/metaphor-010-templates-tables-2026-11-17.md);
  follows [`menu-values-party-unit-static.md`](menu-values-party-unit-static.md) and
  [`item-skill-message-serials-static.md`](item-skill-message-serials-static.md)
- **Functions:**
  - SkillSelectDecide | 0xd77810 | .ecode | Battle | known only | Reads the focused row's skill id from the component. | finding; docs/findings/battle-skill-select-decide-and-ui-sound-static.md

For #692 (b). **Found, as data with no hook.** The battle skill list already names each row by the serial it fetches
from `Skill_Message_1.msg` (#247). That serial's label (`SkillName_FireMagic_01`), hashed with MD5, equals the 16 bytes
at `+0x2` of exactly the `Skill.TBL` records whose index is the skill id; `SkillNormal.TBL` at the same index holds the
cost type and amounts. Both files sit uncompressed in the player's `base.cpk`, readable with the mod's `CpkArchive` at
start-up, so the `.text1`-only skill table global
([party unit note](menu-values-party-unit-static.md#3-skill-cost-kind-and-usability)) isn't needed.

## Evidence

- **The files.** `CpkArchive.Lookup` on this build's `base.cpk`: `COMMON/battle/table/SkillNormal.TBL` is 48,000 bytes
  (1,000 records of `0x30`, no header) and `COMMON/battle/table/Skill.TBL` 51,000 bytes (1,500 records of `0x22`);
  both `Read`, so stored uncompressed. `0x3dd7b0` loads `SkillNormal.TBL`'s 48,000 bytes into the table the cost code
  reads (`menu-values-party-unit-static.md`, section 3).
- **The hash.** Hashing each label of `Skill_Message_1.msg` (as compiled in the maintainer's local 2026-11-23 19.22.38
  session log, grep only) with MD5 over its ASCII name: 1,149 of the 1,500 `Skill.TBL` records' `+0x2` bytes match a
  label, and none of their `+0x12` bytes do (those are presumably the description's). Record 1 is
  `SkillName_FireMagic_01` ("Bot"), record 253 `SkillName_MADOU_FIRE_01` (label position 245), record 173
  `SkillName_ATK_KAI_01`. The label's position in the file is its serial minus 6749, but it isn't the skill id.
- **The cost.** `SkillNormal.TBL` per the 010 template (Sources): `+0x5` cost type (1 HP, 2 MP, 3 money), `+0x6`
  percent, `+0x8` flat value, `+0xA` turn icons. Record 1 (Bot): type 2, 0 %, 4, so 4 MP; records 2–4 (the next fire
  spells): 10, 18, 22 MP. Record 173: type 1, 15 %, 0, so 15 % of max HP. Over records 0–999: 692 MP, 13 HP, 6 type 3,
  289 none. Records 1,000 and up in `Skill.TBL` (249 with a name) have no `SkillNormal` record; they are presumably
  passives `[NEEDS VERIFICATION]`.
- **Shared names.** Over records 1–999, 830 distinct name hashes; 22 are shared by more than one record, and in the 14
  where the costs differ, exactly one record has a non-zero cost (an enemy or item variant costs 0, for example 607 at
  6 MP and 864 at 0). So "the record with a cost" resolves every shared name.
- **What the table doesn't say.** The game's own cost function `0x3837d0` also halves or quarters a cost for unit bits
  25 and 26, sets it to 0 for bit 24, and adjusts HP and money costs for some passives (same note, section 3). So the
  table gives the base cost; whether the list shows a changed one when those apply is `[NEEDS VERIFICATION]`. That Bot
  is shown as 4 MP in game is `[NEEDS VERIFICATION]` too.
- **Other routes, not needed.** `SkillSelectDecide` (`0xd77810`) reads the focused row's skill id at component
  `+0x4260`, but only on confirm ([decide note](battle-skill-select-decide-and-ui-sound-static.md)); no per-row id or
  cost was looked for in the skill list's layout.

## Steps

1. `Metaphor.Core.dll` (Release build of `main`) loaded in PowerShell 7; `CpkArchive.Lookup` on `base.cpk` for the two
   paths above, each saved to a scratch folder outside the repository.
2. Labels: the `@` lines after the `compile … Skill_Message_1.msg` record in the local session log, in file order.
3. A throwaway PowerShell parse: MD5 of each label (ASCII), matched against each `Skill.TBL` record's 16 bytes at `+0x2`
   and `+0x12`; `SkillNormal.TBL` bytes `+0x5`, `+0x6`, `+0x8`, `+0xA` printed for the matches; names grouped by hash.

## Build plan

1. **Tables.** A `SkillCosts` reader in `Metaphor.Core` (beside `FieldTables`): read both files with `CpkArchive` once
   at start-up (bounded sizes: 48,000 and 51,000 bytes; anything else is "no costs", logged). Map name hash to skill id,
   keeping the record with a non-zero cost when a hash is shared, then id to `(type, percent, value)`.
2. **Row.** In `BattleListFocus.Skills`, the row's name serial gives its label through `MessageSerials.FindBySerial`;
   MD5 of the label gives the cost. Say it after the name: "Bot, 4 MP"; an HP skill "15 percent HP" (or the amount from
   the unit's max HP, which `battleHpMp` already reads, once the shown form is checked); no cost, nothing.
3. **Setting.** Part of the skill list's existing setting, or its own (rule 18) if the maintainer wants it separate.
4. **Tests.** A fake pair of tables and a fake message: an MP skill, an HP skill, a shared name, a damaged table.
   By ear: open the skill list with Bot, an HP skill, and a skill under a cost-halving effect; compare with the screen.
