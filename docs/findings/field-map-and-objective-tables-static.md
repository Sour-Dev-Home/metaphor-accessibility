# Each map's row names its region and its own name, and each field objective shares its flag with the main mission

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's data files from `base.cpk` offline, as
  [`tutorial-table-maps-numbers-to-labels.md`](tutorial-table-maps-numbers-to-labels.md) did; no ADR 0002 row covers
  data files, which no protection guards. The live check is our own Reloaded-II mod's message log.
- **Sources:** none, see Steps; follows [`field-map-region-area-name-static.md`](field-map-region-area-name-static.md)
  and [`mission-title-from-the-mission-table.md`](mission-title-from-the-mission-table.md)

For "where am I" (#742): `MapData.bin` has one `0x2c`-byte row per map id. Its `u16` at `+0x6` indexes `AreaName.bin`,
whose row starts with the `MapName.msg` position of the region; that is the one label the game looked up by position
for every map in five sessions. The map's own name is the label MD5 at row `+0x1c` (map 371: `MapName_FreeD09_05_00`,
"Forsaken Tower"), which no position lookup names. For the mission line (#743): `Mission_Field_Data.bin` lists the
field objectives, each with its label MD5 and the story flag that starts it, and the flags are the main missions' own
(`0x11fd` starts `MissionText_MainMission_07` and `MissionText_Field_08_01_00`).

## Evidence

Hashes are MD5 of the label's text without `@`, matched against every label the 2026-11-22 session's log compiled.

| File (`base.cpk`) | Header | Row | Fields read |
|---|---|---|---|
| `COMMON/init/data/MapData.bin` | `MAPD`, count `0x25e` (606) at `+0x8` | `0x2c` bytes from `+0x10`, row = map id | `u16 +0x6` area row (equal to `u16 +0x12` in every row checked); label MD5 `+0x1c` |
| `COMMON/init/data/AreaName.bin` | count 66 at `+0x8` | `0x24` bytes from `+0x10` | `u16 +0x0` region's `MapName.msg` position; `u16 +0x2` unknown; MD5 `+0x4` region; MD5 `+0x14` area (empty outside free dungeons) |
| `COMMON/mission/source/bin/Mission_Field_Data.bin` | `MSN_F`, count `0x60` (96) | `0x24` bytes from `+0x10` | MD5 `+0x0` objective; MD5 `+0x10` second label, mostly empty; `u16 +0x20` flag; `u16 +0x22` 0–3, unknown |

**Region lookups.** Each `map strip id=` record in the five session logs of 2026-11-21 and 2026-11-22, paired with
the `lookup id=3 label=` before it, against the tables:

| Map | Area row | Region position (lookup seen) | Region label | Map's own label (`+0x1c`) |
|---|---|---|---|---|
| 1, 2, 3, 22, 26 | 0 | 1 (1) | `MapName_Town01_00` | `MapName_Town01_01`, `_02`, `_03`, `MapName_Facility01`, `MapName_Facility05` |
| 58 | 6 | 483 (483) | `MapName_Akademeia01_01` | `MapName_Akademeia01_00` |
| 137, 151 | 8 | 131 (131) | `MapName_Dungeon00_00` | `MapName_Dungeon00_01`, `MapName_Dungeon00_02` |
| 138 | 9 | 137 (137) | `MapName_Dungeon01_00` | `MapName_Dungeon01_01` |
| 344 | 59 | 376 (376) | `MapName_FreeDungeonZone_05` | `MapName_FreeD08_01_00` |
| 371, 372 | 39 | 374 (374) | `MapName_FreeDungeonZone_03` | `MapName_FreeD09_05_00` |

All 12 maps match. So the position lookup is the region, and the place's own name comes by hash, which the mod's
`lookup` record doesn't see. AreaName row 39's area MD5 is also `MapName_FreeD09_05_00`. That the strip's second name
(`0xa8e74c0` in the [strip note](field-map-region-area-name-static.md)) is the map's own name at `+0x1c`, and not the
area row's, is [NEEDS VERIFICATION]: they agree on every free-dungeon map above, and the area row's is empty in towns.

**Objectives.** Each of the main table's 12 rows (11 flags; `0x1185` starts two) has a field row with its flag (`0x1166` `MissionText_Field_01_01` "Walk to the
Royal Capital", `0x11fd` `MissionText_Field_08_01_00` "Put an end to the kingdom's madness"). Rows between them start
the chapter's later objectives (`0x11fb` `MissionText_Field_07_02`, `0x11fc` `MissionText_Field_Sleep` "Rest up for
tomorrow"). Field flags run from `0x1165` to `0x1209`, inside the range the main read scans. At L in the session
(`757290 ms`) the top set flags were `0x11fd,0x11fc,0x11fb,0x11fa`, and the top one with a field row is `0x11fd`.
[NEEDS VERIFICATION]: that the box picks the objective as the top set flag's field row, like the title, since the box
builds it in `.text1` ([mission box note](mission-box-text-source-static.md)); what the second label and `+0x22` choose
(row `0x11fd`'s second label is `MissionText_Field_08_01_01`, "Build strength for the decisive battle"); and the other
two files (`Mission_Dungeon_Data.bin`, `0x28`-byte rows; `Mission_Battle_Data.bin`, `0x14`), not read. The 96 field rows have 96 distinct flags.

## Steps

1. `Find-MetaphorGame` (`scripts/player/MetaphorInstall.psm1`) for the install; `sha256sum` of the exe matches the
   build above.
2. Build `src/Metaphor.Core` and call `CpkArchive.ReadFile` from PowerShell for the five paths in the tables; all are
   stored uncompressed. Copies stay in a scratch folder, never the repository.
3. Labels: from the session log of 2026-11-22 13.05.43, every `  | @<label>` line of its `compile` records (18,478
   labels). A Python 3.12 script hashes each with MD5 and decodes the rows above.
4. Pairs: `grep -E "lookup id=3 |map strip id="` over each session's `message-log.txt`, keeping the last lookup before
   each strip record.
5. `FieldTables.From` (`src/Metaphor.Features.Field/FieldTables.cs`) on the real archive reads 606 maps and 96
   objectives, map 371's hash is `MapName_FreeD09_05_00`'s, and flag `0x11fd`'s objective is
   `MissionText_Field_08_01_00`'s.

## What the next session must check

Load the save in the Forsaken Tower and press L: expect "Forsaken Tower, Principality of Oceana. Mission: The Final
Battle: Put an end to the kingdom's madness", the log's `field tables: 606 maps, 96 objectives`, and the mission box on
screen showing the same objective.
