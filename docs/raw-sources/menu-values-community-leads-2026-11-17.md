# Where party, item, equipment, archetype, money and Mag values may live: community leads (#155)

- **Source:** web and GitHub searches on 2026-11-17, and these pages and files:
  - Cheat Engine table "METAPHOR v9" by schutzdev, as listed on GamePressure
    (<https://www.gamepressure.com/download/metaphor-refantazio-cheat-table-ct-for-cheat-engine-v9-mod/z714f93>, read
    with WebFetch), which links the FearlessRevolution topic <https://fearlessrevolution.com/viewtopic.php?f=4&t=31583>.
    The topic returned HTTP 403 to WebFetch; the table itself was not downloaded or read.
  - "Cheats Menu" 2.0.2 by HaruAjsuru on GameBanana (<https://gamebanana.com/mods/587805>, read through
    `gamebanana.com/apiv11/Mod/587805/ProfilePage`), CC BY-NC-ND 4.0. Only its description was read.
  - `RyoTune/MF.Toolkit` at `b1f876da5b4c1f42b5eef07fc6c2174030beb77b` (AGPL-3.0),
    `MF.Toolkit.Interfaces/Library/MetaphorDefinitions.cs`, read through `gh api`: the delegate signatures below.
  - `NM-20/Resonant` at `6b90020b2a99175e25ea7cfc0ae2dc4efc987c89` (GPL-3.0): its file tree, through `gh api`.
  - Script-function addresses: `squirrel-script-functions-all-2026-11-16.md` (this repository).
- **Saved:** 2026-11-17.
- **Game build:** addresses are for `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build
  18330018), from the capture cited above. None of the community sources names a build. The cheat table was last
  updated 2024-10-28 and MF.Toolkit's file 2024-11-01, weeks after launch, so both likely target an older build
  `[NEEDS VERIFICATION]`; Cheats Menu was last updated 2025-06-11.

Identifiers, signatures and RVAs only (ground rule 4). Static table layouts (skill costs, affinities, equipment stats)
are in `metaphor-010-templates-tables-2026-11-17.md`.

## What exists

- **Cheat table (schutzdev v9).** Feature names only: Money edit, MAG edit, a Party editor (current and max HP and MP,
  level, XP, ATK, DEF and AGI buffs and their durations, combat stats, archetype, archetype skills), Item Count Editor,
  Follower Link Level edit. This says these values sit at findable addresses, but the offsets weren't read, and the
  table predates our build. `[NEEDS VERIFICATION]`
- **Cheats Menu.** A script mod (needs MF.Toolkit and Metaphor Essentials) that adds money, Mag, EXP, archetype EXP and
  stats and heals the party through the game's own script functions. No structure facts. Its licence forbids reuse.
- **Resonant.** Headers for the field, unit, text and UI layers. Nothing on party members, items, equipment, money or
  archetypes.
- **Save editors:** none found (as in `modding-tools-survey-2026-11-13.md`). GitHub repositories named like
  "Metaphor-ReFantazio-Cheat" or "trainer" look like malware lures and weren't opened.

## Script functions that read or write these values

Signature from MF.Toolkit (parameter names are its `param1`…; meanings are guesses `[NEEDS VERIFICATION]`); RVA in our
build from the capture. Whether the registered address is the native getter or a thin wrapper around it is
`[NEEDS VERIFICATION]`.

| Value | Function | MF.Toolkit signature | RVA |
|---|---|---|---|
| Money | `GetMoney` | `int GetMoney()` | 0x90a510 |
| Money (write) | `SetMoney` | `byte SetMoney(int)` | 0x910490 |
| Mag (write only; no getter registered) | `SetMag`, `AddMag` | `byte SetMag(int)`, `byte AddMag(int)` | 0x910480, 0x903650 |
| Item count | `GetBagItem` | `byte GetBagItem(int)` | 0x907810 |
| Party membership | `IsInParty` | `byte IsInParty(int)` | 0x90b150 |
| Battle party slot | `GetBattleParty` | `int GetBattleParty(int)` | 0x907830 |
| Front/Back (write) | `SetBattlePartyFormation` | `byte SetBattlePartyFormation(int, byte)` | 0x9103a0 |
| Level | `HumanParamGetLevel` | `int HumanParamGetLevel(int)` | 0x90aca0 |
| Level, from a unit | `UnitGetLevel` | `int UnitGetLevel(nint)` | 0x912a30 |
| Max HP | `UnitGetMaxHP` | `int UnitGetMaxHP(nint)` | 0x912a40 |
| A unit parameter | `UnitGetParam` | `nint UnitGetParam(nint, int)` | 0x912a50 |
| Unit of a character | `GetPlayerUnitID` | `byte* GetPlayerUnitID(int)` | 0x90a9f0 |
| Equipped item | `CharaGetEquip` | `int CharaGetEquip(int, ushort)` | 0x903ec0 |
| Weapon, armor stat | `UnitGetEquipWeaponParam`, `UnitGetEquipArmorParam` | `int (nint, byte)` | 0x912a10, 0x9129f0 |
| Current archetype | `CharaGetClassID` | `int CharaGetClassID(int)` | 0x903eb0 |
| Archetype rank | `GetClassLevel` | `int GetClassLevel(int, int)` | 0x9078f0 |
| Affinity | `UnitGetAffinity`, `UnitGetAffinityType` | `short (int*, int)`, `byte (int*, int)` | 0x9129b0, 0x9129d0 |
| Conditions (write) | `CharaRecoveryBadStatus` | not checked | 0x903ee0 |
| Character EXP (write) | `Exp_Add_Character_Hero` | not checked | 0x905f30 |

Nothing registered reads current HP or MP by name (only through `UnitGetParam`, if at all), current EXP or EXP to next
level, archetype EXP, conditions, or the tactics setting. `[NEEDS VERIFICATION]`
