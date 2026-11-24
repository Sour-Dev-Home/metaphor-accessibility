# An enemy id indexes `Enemy.TBL` (EXP, drops) and `EnemyAffinity.TBL`; `EnemyID.bin` names it by its `EnemyName` label

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own data archives (CPK) offline with our read-only tool
  (`tools/Metaphor.CpkExtract`), output kept in git-ignored `extracted/`; opening the shipped executable in a
  disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** field names from `DeathChaos25/metaphor-010-templates` at `cfccaa2f4e4352d2881884b1c7d61c53661259d3`
  (`Enemy.bt`, `EnemyAffinity.bt`, `EnemyID.bt`, `Encount.bt`, `metaphor_structs.h`; no licence file, so only field
  names, order and types are used), read through `gh api` as
  [`metaphor-010-templates-tables-2026-11-17.md`](../raw-sources/metaphor-010-templates-tables-2026-11-17.md) did;
  follows [`battle-banner-enemy-name-serials-static.md`](battle-banner-enemy-name-serials-static.md),
  [`battle-results-rewards-static.md`](battle-results-rewards-static.md) and
  [`skill-cost-from-skill-tables.md`](skill-cost-from-skill-tables.md)
- **Functions:**
  - BattleTableLoad | 0x3dd7b0 | .ecode | Battle | known only | Loads the battle tables by path, `Enemy.TBL` and `EnemyAffinity.TBL` among them (also `SkillNormal.TBL`).
  - ResrcInitLoad | 0x8994a0 | .ecode | Battle | known only | Loads the init tables by path, `EnemyID.bin` among them.

For #164, #255 and #166. **Found, as data with no hook.** The enemy id is a row index shared by three files in the
player's `base.cpk`, all stored uncompressed: `Enemy.TBL` (level, HP, EXP, money, drops), `EnemyAffinity.TBL` (the 19
affinities) and `EnemyID.bin` (the MD5 of the enemy's `EnemyName.msg` label). So a banner's name serial gives the
label, the label's MD5 gives the id or ids, and the id gives weaknesses, EXP and drops, without the protected Analyse
code. Confidence: high for the layouts and the join; the three Skeleton06 rows tie it to #739's session.

## Evidence

### The files

| File | Size | Layout | Rows |
|---|---|---|---|
| `COMMON/battle/table/Enemy.TBL` | 80,100 | no header, `0x64`-byte rows | 801 |
| `COMMON/battle/table/EnemyAffinity.TBL` | 30,438 | no header, `0x26`-byte rows | 801 |
| `COMMON/init/data/EnemyID.bin` | 14,146 | `ENEMY\0\0\0`, `u32` count 785, `u32` 0, then `0x12`-byte rows | 785 |
| `COMMON/battle/table/Encount.TBL` | 56,012 | `0x1c`-byte rows, 12 bytes left over | 2,000 |

Each size fits its row size exactly (bar `Encount.TBL`'s tail). The template's fields, with offsets added up from their
types; each checked against the values below:

- **`Enemy.TBL` row.** `+0x00` `u16` flags, `+0x02` `u16` category, `+0x04` `u32` level, `+0x08` `u32` max HP,
  `+0x0c` `u16` MP, `+0x10` six stat bytes, `+0x16` eight `u16` skill ids, `+0x26` `u16` EXP, `+0x28` `u16`
  archetype EXP (the template's `AEXP`), `+0x2a` `u16` magla (`MAG`), `+0x2c` `u16` money, `+0x32` common drop
  (`u16`: item id in the low 12 bits, item kind in the high 4) and `+0x34` its rate, `+0x36` rare drop and `+0x38`
  its rate, `+0x48` the attack's element, accuracy, attack, defence, evasion, `+0x54` 16 bytes that are
  `d41d8cd9…`, the MD5 of nothing, in all 801 rows.
- **`EnemyAffinity.TBL` row.** 19 two-byte entries: Slash, Pierce, Strike, Fire, Ice, Electric, Wind, Light, Dark,
  Almighty, then the ten ailments. Each is a multiplier byte (units of 5 %; 0 or 20 is plain) and a flags byte: bit 0
  null, 1 repel, 2 drain, 3 weak, 4 resist, 5 ailment immune, 6 ailment guaranteed, 7 double ailment chance, the
  same entry as `ClassAffinity.TBL`'s (raw source above).
- **`EnemyID.bin` row.** 16 bytes of MD5, then an `s16` (−1 to 352, 267 distinct; ids that share a name share it,
  so presumably a name or book index `[NEEDS VERIFICATION]`). The row's index is the enemy id.

### The join

- **Labels.** The MD5 of each `EnemyName.msg` label (ASCII, as `Skill.TBL` hashes skill labels) matches 398
  `EnemyID.bin` rows; the other 387 hold the empty MD5. 359 of the 421 labels are used; 37 are shared by two ids or
  more (76 ids).
- **Named rows are real enemies.** All 398 named rows have stats in `Enemy.TBL`; 324 of the 387 unnamed ones are the
  placeholder row (level 1, 110 HP). All 2,644 enemy slots of `Encount.TBL`'s 2,000 encounters (`+0x8`, six `u16`)
  name a named id (357 distinct, up to 784).
- **The session.** #739's review logged banner serials `0x97`, `0x9f` and `0xa7` with enemies of 1,440, 1,320 and
  1,200 HP. By `EnemyID.bin` ids 49, 57 and 65 are `sword_`, `lancer_` and `arrow_Skeleton06` (serials 151, 159,
  167, the same three), and `Enemy.TBL` gives them 1,440, 1,320 and 1,200 HP, level 67; 1,440 and 1,320 occur in no
  other row.
- **Affinities look right.** The three Skeleton06 rows: Slash and Pierce resist, Strike weak, Electric null, Light weak,
  Dark resist. That the game shows those is `[NEEDS VERIFICATION]` by ear. Over the named rows' ten damage
  elements: 473 resist, 378 weak, 207 null, 155 repel, 42 drain; 142 rows have no weakness.
- **Rewards.** Row 49: EXP 160, archetype EXP 60, magla 350, money 125, common drop kind 4 item 167 at 4 %. Whether
  the results screen's EXP is this value or one scaled by level or by a bonus is `[NEEDS VERIFICATION]` (#166). Item
  kind 4 is consumables and 0 equipment by the template's enum; kinds 1 and 2, which some rows use, it doesn't name
  `[NEEDS VERIFICATION]`. Item ids are not yet joined to names.
- **No constant offset.** Ids are not label position + a constant: it holds for rows 44 to 67 (+20) but not past 300,
  so only `EnemyID.bin` joins them.

### Shared names

Of the 37 names with several ids, 18 differ in their damage affinities and 28 in EXP; in 31 every id has a different
max HP. A name serial alone gives one row for 322 of the 359 names; for 31 of the other 37 the max HP a hit shows
picks it, and the last 6 need another field (or the `head=` id below).

## Build plan

A runtime reader in the Battle project, as the skill-cost reader (Battle round 4) does for skills:

1. **Tables.** An `EnemyTables` reader in `Metaphor.Core` (beside the skill-cost reader): read the three files from
   `base.cpk` with `CpkArchive` once at start-up (bounded: 80,100, 30,438 and 14,146 bytes, and the row sizes must
   divide them; anything else is "no enemy data", logged). Index id to `(level, max HP, EXP, archetype EXP, magla,
   money, drops, affinities)` and label MD5 to ids. Unit tests on made-up tables. Credit the 010 templates in
   `CREDITS.md` (rule 7).
2. **Which id.** From a banner's name serial: serial − 122 is the `EnemyName.msg` position (`MessageSerials` gives the
   label), MD5 of the label gives the ids; when there are several, take the one whose max HP matches the hit's
   "of max". The Debug `head=` records (`BtlPackageUpdate`) are the check: if a unit's record holds the id, read it
   there and drop the name step.
3. **Speech.** Weaknesses on the existing target or HP keys behind a testing setting (#164, #255): "Weak to Strike,
   Light. Resists Slash, Pierce, Dark. Nulls Electric." Rewards on the results screen (#166) only after the shown values
   are checked against the table.
4. **Open question for the maintainer.** The game shows an affinity only once it's found in play; this data gives them
   all up front. Whether to speak only found ones needs the found state, which isn't located.

## Steps

1. `Find-MetaphorGame` (`scripts/player/MetaphorInstall.psm1`) for `METAPHOR_GAME_EXE`; the exe's SHA-256 matches the
   build above. .NET SDK 9.0.319, `tools/Metaphor.CpkExtract` at `a020a205`, 2026-11-24:
   `dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk`, filtered for `battle/table` and
   `init/data`; then `extract base.cpk COMMON/battle/table/Enemy.TBL COMMON/battle/table/EnemyAffinity.TBL
   COMMON/battle/table/Encount.TBL COMMON/init/data/EnemyID.bin --out extracted/enemy`.
2. The templates: `gh api repos/DeathChaos25/metaphor-010-templates/contents/<file>?ref=cfccaa2f…` for the files under
   Sources, read, not saved.
3. Labels: the 421 `@EnemyName_` lines of a local 2026-11-21 Debug message log (grep only, not committed), in file
   order; position 29 is `EnemyName_sword_Skeleton06`, as the banner note has it.
4. Python 3.12.10, throwaway scripts outside the repository's tracked files: the rows above parsed; MD5 of each label
   matched against `EnemyID.bin`; `Encount.TBL`'s slots checked against the named ids; counts printed.
5. Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project on the build above, `scripts/dev/ghidra.ps1
   batch`: `global` on the path strings `0x17e84d8` (`Enemy.TBL`), `0x17e84f8` (`EnemyAffinity.TBL`) and `0x210c690`
   (`EnemyID.bin`), then `decompile 0x8994a0` (the file names it loads, not quoted) and `callers 0x8994a0`.
