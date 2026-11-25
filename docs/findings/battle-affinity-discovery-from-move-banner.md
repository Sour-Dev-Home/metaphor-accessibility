# A move banner's kind-0 id is the skill's `Skill.TBL` id, so the mod can track which affinities a player found

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`metaphor-010-templates-skill-element-2026-11-24.md`](../raw-sources/metaphor-010-templates-skill-element-2026-11-24.md);
  builds on [`battle-move-banner-text-reference-static.md`](battle-move-banner-text-reference-static.md) and
  [`enemy-tables-affinities-exp-drops.md`](enemy-tables-affinities-exp-drops.md)
- **Functions:**
  - SkillRecord | 0x3e14f0 | .ecode | Battle | known only | Returns skill `id`'s 0x22-byte `Skill.TBL` record, a fixed record for id 0. | finding
  - ActionLabel | 0x3bd020 | .ecode | Battle | known only | Picks a name getter by an action id's top 4 bits (its item kind). | finding

For #164 and #255. Where the game keeps the affinities a player has found is **not located**; the mod tracks them
itself instead. `BattleMessageShow`'s kind 0 banner, id not 0, takes its label from `0x3e14f0(id) + 2`, and
`0x3e14f0` returns `id × 0x22` past a table global: the `Skill.TBL` record size, whose `+0x2` is the name label's MD5.
So the move banner names the skill by its id, and that record's `+0x0` element byte gives the element (the raw
source). Confidence: high that the id is a `Skill.TBL` row; that every player skill shows a kind 0 banner is
`[NEEDS VERIFICATION]` by ear.

## Evidence

- **The record getter.** `0x3e14f0(int id)` returns a fixed address for id 0, else `id * 0x22 + [global]`. 0x22 is
  `Skill.TBL`'s record size (`skill-cost-from-skill-tables.md`), and the banner reads the label at `+2`, where that
  record keeps the name's hash.
- **Kind 1 is not the skill.** `0x3bd020(id)` jumps through seven getters by `id >> 12`, with the low 12 bits as the
  index, the encoding of an enemy drop's item kind and id. The 2026-11-22 run's item uses logged kind 1 ids 16477 and
  16493 (`0x405d`, `0x406d`, kind 4), so kind 1 names an item.
- **The found state.** `BTL_AnalyzeChoice`'s vtable (`0x21cc448`) and `AnalyzeInfoLayout`'s (`0x2169900`) were listed;
  the layout's own slots (`0xaf6650`, `0xaf6ea0`, `0xb03570`, `0xaf8910`) each call one getter and return, so no
  found-affinity mask was seen. Not followed further.

## What the mod does

A kind 0 banner sets the move's element (none for id 0, a basic attack, or a healing, support or ailment skill); a hit
on an enemy within 15 s after, before any hit on the party, marks that element found for the enemy's id. A hit with a
number marks it; one without only when the table says null or repel. Found elements last the game session; the mod has
no per-save store. Whether this matches what the Analyse screen reveals is `[NEEDS VERIFICATION]` by ear.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-24:
`vtable app::ui::ope::BTL_AnalyzeChoice`, `vtable app::ui::AnalyzeInfoLayout`, `callees` on the four layout slots
above, `decompile 0x3bd020`, `decompile 0x3bbe20`, `decompile 0x3bb880`, `decompile 0x3e14f0`. Nothing in `.text1`,
`.xcode` or `.xtext` was decompiled.
