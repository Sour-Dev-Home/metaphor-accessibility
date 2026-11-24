# Each field map's icon-point file places the current objective's marker, keyed by its field mission row

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's data files from `base.cpk` offline, as
  [`field-map-and-objective-tables-static.md`](field-map-and-objective-tables-static.md) did (no ADR 0002 row covers
  data files, which no protection guards); opening the shipped executable in a disassembler as-is, accepting that
  protected parts may be unreadable
- **Sources:** none, see Steps; follows [`field-map-and-objective-tables-static.md`](field-map-and-objective-tables-static.md),
  [`mission-box-text-source-static.md`](mission-box-text-source-static.md),
  [`field-route-query-for-path-pings-static.md`](field-route-query-for-path-pings-static.md)
- **Functions:**
  - FldMapIconUpdateParam | 0x8e0090 | .ecode | Field | known only | Script function; thunk to `0x6d11f0`, which re-evaluates each map icon's condition strings.
  - MapIconManager | 0x2ee7700 | .debug | Field | known only | Global singleton (`0x120` bytes, made in `0x5ee590`) that `0x6d11f0` and about 20 `.ecode` functions read; presumably `fldMapIcon`.

Spike for #652. **Found, as data the mod can read offline with no hook.** `base.cpk` holds one file per field map and
variant, `COMMON/field/source/layout/map_icon_point/mip_<a>_<b>_<c>.dat` (133 of them, listed in `exist_mipoint.dat`).
Each holds the map's icon points: a kind, a 2D position and an index. The 102 named `Mission_FLD_MISSION<NN>_<MM>`
(kind 4) have an index that fits the row of `Mission_Field_Data.bin`, the table the mod's mission line already reads
(#665, #743). So the current objective's row picks out its marker on the current map. In the royal capital, a chapter 1
objective with a marker has one on each district map, at the objective or on the way to it. **Not settled:** how the 2D position converts to world
`x, z`. A one-session live check settles that ([checklist](#live-session-checklist)).

| Question (brief order) | Answer | Confidence |
|---|---|---|
| 1. A marker the minimap or field map draws | `mip_*.dat` kind-4 points, named per field mission | High for the data; medium that it's what the minimap draws |
| 2. Quest or objective records with a place | None: `Mission_Main_Data` (`0x14`-byte rows), `Mission_Field_Data` (`0x24`) and `Mission_Dungeon_Data` (`0x28`) hold label MD5s and flags only | High |
| 3. Story-path exits | Not as a flag on `MapLink_Data.bin`; the town markers sit on district exits instead (below) | Medium |
| 4. Something the HUD's text ties to a place | The same field row: its flag gives the HUD text (#743) and its index gives the marker | Medium |
| The game's loader | The `mip_%04d_%03d_%03d.dat` and `exist_mipoint.dat` strings have no reference in analysed code; `fldExistMapIconPoint`'s vtable is used only from `.xcode` (`0x14c317cc`) | High that it's out of reach; nothing needs it |

## Evidence

### The file

`mip_0001_001_000.dat`, 800 bytes: a `0x14`-byte header (`u32` 1, `u32` 1, `u32` 0, `u32` count 15, `u32` `0x14`, the
first entry's offset), then 15 entries of `0x34` bytes. All 133 files match `0x14 + count × 0x34` exactly (357
entries).

| Entry offset | Type | What it seems to be |
|---|---|---|
| `+0x00` | `u32` | 1 in all 357 |
| `+0x04`, `+0x06` | `u16`, `u16` | 2D position, 94 to 1,957 across all files, so not world units [NEEDS VERIFICATION] |
| `+0x08` | `u32` | 0 in 353; a float in 4 |
| `+0x0c` | `u32` | kind: 2 (39), 3 (4), 4 (137), 5 (138), 6 (24), 7 (15) |
| `+0x10` | `u32` | index: a field mission row for kind 4 (below) |
| `+0x14` | `char[0x20]` | name: `Mission_FLD_MISSION<NN>_<MM>` (102), `DNG_MISSION_D<n>_<m>[...]` (5), `MapIcon_...` (24), or empty |

`exist_mipoint.dat` (810 bytes) is a header (`u32` 1, `u32` 133, `u32` `0xc`) and 133 `u16` triples `a, b, c`, one per
`mip` file. The file name's `a, b` are the field's `f_<a>_<b>`, the name `MapData.bin` gives a map's navmesh
([note](field-navmesh-file-from-map-data.md)): map 1 is `f_0001_001`, map 2 `f_0001_002`, map 3 `f_0001_003`, and 110
of the 606 map ids have a field with a `mip` file. `c` is 0 except for `f_0004_001`, `f_0005_00x` and `f_0030_001`,
which also have a `_001` [NEEDS VERIFICATION: what picks the variant].

### The index is the field mission row, not the name's number

The name's `NN` and the index differ by 2 from `NN = 7` on (`MISSION07` has index 9, `MISSION18` has 20). The index
is the one that fits, read against `Mission_Field_Data.bin`'s rows in file order (labels by MD5 as in the
[tables note](field-map-and-objective-tables-static.md#evidence)):

- One point on map 1, `(1478, 1025)`, has index 9 (row 9, flag `0x1174`, "Head to the Hushed Honeybee Inn") and index
  92 (row 92, "Rest at the inn after speaking with people"). By name these are rows 7 and 90, "Rest up for tomorrow"
  and "Build strength for the decisive battle".
- On `f_0030_001_001`, the gauntlet runner by its use [NEEDS VERIFICATION], `MISSION55` has index 57, "Head to the
  deck". On `f_0004_001`, `MISSION58` has index 60, "Prepare and enter the Dragon Temple".

So the row the mod already finds for the mission line, the top set flag's row, is also the marker's index. That the
game picks the marker the same way is [NEEDS VERIFICATION], like the objective text
([mission box note](mission-box-text-source-static.md)).

### Chapter 1 in the royal capital

Kind-4 points by row, `(x, y)` per field:

| Row | Flag | Objective | `f_0001_001` (map 1) | `f_0001_002` (map 2) | `f_0001_003` (map 3) | Other |
|---|---|---|---|---|---|---|
| 2 | `0x1168` | Head to the Recruitment Centre | (1045, 295) | | (836, 784) | |
| 3 | `0x116a` | Queue up for provisions | | | (731, 638) | |
| 9 | `0x1174` | Head to the Hushed Honeybee Inn | (1478, 1025) | (1537, 1359) | | |
| 11 | `0x1177` | Sneak into the royal funeral | (1045, 295) | (815, 496) | (1203, 710) | |
| 13 | `0x117b` | Head to the Regalith Grand Cathedral | (1045, 297) | (815, 496) | (1203, 540) | |
| 14 | `0x117c` | Head to the Mausoleum | (1045, 295) | (815, 496) | (569, 630) | `f_0153_002` (1024, 850) |
| 20 | `0x1186` | Find Maria | (1045, 295) | (814, 497), (971, 816) | (1204, 541) | |
| 22 | `0x1188` | Register for the Tournament for the Throne | | | (731, 640) | |
| 23 | `0x1189` | Accept the kidnapper's bounty | | | (731, 639) | |
| 26 | `0x118c` | Explore the gauntlet runner | | | | `f_0030_001_000` (1097, 1198) |

One point serves many objectives on a map: `(1045, 295)` (or `297`) on map 1 for eight rows (2, 11, 13, 14, 20, 81, 94, 95), `(815, 496)` on map 2 for four.
They fit the way out toward the district that holds the objective, so a beacon pointed at the marker leads
district by district [NEEDS VERIFICATION]. Rows with no point in chapter 1: 1 ("Walk to the Royal Capital"), 4 and 6
("Choose a destination", the travel map), 5, 7, 12, 15, 18, 21, 24, 28 (resting), 8, 10, 16, 17, 19, 25, 27.

### Where the positions live: not in the mission tables or the link table

- `Mission_Main_Data.bin` (`MSN_M`, 12 rows of `0x14`: MD5, `u16` flag) and `Mission_Dungeon_Data.bin` (`MSN_D`, 141
  rows of `0x28`: two MD5s, a `u16` from `0x3c0c` up, six `0xff` bytes) hold no coordinates. The `DNG_MISSION` points'
  indexes (65, 70, 75) may be `Mission_Dungeon_Data` rows [NEEDS VERIFICATION].
- `COMMON/field/source/layout/mission/f_0001_00{1,2,3}.mps` are 16-byte headers (`MPS`, 14, a date-like `0x20200715`)
  with no entries.
- `MapLink_Data.bin` (`MAPLIK`, 999 rows of `0x24` bytes of `u16`s) has no field that marks a story path in the rows
  read; it wasn't decoded further.
- The PG script functions `PG_GetDestinationZoneIDFromQuestID` and `PG_GetDestinationPointDFromQuestID`, called by name
  from `0x4b9090`, return a zone and a point for a quest id. That is the travel map's destination, not a field position.

### The 2D-to-world conversion is open

Since settled from the data: [`minimap-data-converts-map-points-to-world.md`](minimap-data-converts-map-points-to-world.md)
(`x = Ox − s × px`, `z = Oz − s × py`, the record picked by `MiniMap_SetData.bin`). What follows is the earlier reading.

`COMMON/ui/map/MiniMap_Data.bin` is a `0x40`-byte header and 1,500 records of 14 bytes (`u16` id equal to the index,
then three floats), 262 of them not `(0, 0, 1)`: record 1 is `(18807, 21899, 18)`, record 6 `(988, 31786, 18)`,
record 11 `(-5347, 51380, 18)`. An origin plus a scale per 2D map fits, but which record a field uses and the formula
are [NEEDS VERIFICATION]. The three royal capital navmeshes lie side by side in one world frame (tile bounds `x`
−8,185 to 9,735 for `f_0001_001`, −26,743 to −8,183 for `_002`, −36,850 to −9,970 for `_003`), and the 2D positions
overlap across them, so each district likely has its own 2D frame. `MiniMapLayout` (vtable `0x21b0b80`) hands the
drawing to the UI system (`0x2f04ab8`) and shows no conversion in its own slots.

## Live-session checklist

What settles the conversion and the row join in one session. Use the 2026-11-23 Debug build or later: its log
already has `field beacon route ... map= file= player=(x,y,z)` and the `field mission` records.

1. **Map 1 (`f_0001_001`)**: with a save on a step in the table above (row 11, 13, 14 or 20), open the field map and
   note where the objective marker is. Walk onto it and press L. Do the same at the Hushed Honeybee Inn's entrance
   (row 9's point) if the step allows.
2. **Maps 2 and 3**: the same, one marker each, plus the Recruitment Centre or the provisions queue on map 3 (row 2 or
   3) if a save has it.
3. **Log, per stop**: the player's `x, y, z` and map id (the L record or a beacon request), the top set main-mission
   flags (the L record lists them), and a screenshot of the field map with the marker. Two stops on one map give the
   scale and offset if there's no rotation; a third checks it.
4. **Check offline afterwards**: fit `world = origin + scale × (x, y)` per district and compare the origin and scale
   with `MiniMap_Data.bin`'s records. If one record per district fits, the conversion needs no more live data.

Pass: the marker on screen matches the row the top set flag gives, and the fitted conversion puts every chapter 1
kind-4 point of that district on its navmesh within a few metres.

## Build plan once the checklist passes

1. **Data, no hook.** A `MapIconPoints` reader in `Metaphor.Features.Field`, beside `FieldTables`: read
   `exist_mipoint.dat` and the `mip_*.dat` files from `base.cpk`, keep kind-4 points by `(field, row)`. Unit tests on
   made-up files.
2. **The goal.** On the "guide me to the objective" key (#652), take the current row from `MissionLine`'s flag scan,
   the current map's field from `MapData`, its points for that row, convert them to world `x, z` with the fitted
   conversion, and give the nearest to the beacon (`NavRoutePlanner`, #690) as its target. When the map has no point
   for the row, say so; the rows without one (above) need hand-written waypoints, our own data (ADR 0016), with the
   same key `(field, row)`.
3. **Variants.** Until what picks `_001` over `_000` is known, take `_000` and log when `_001` exists for the map.

Steps 1 to 3 are built ahead of the checklist (#652, testing `objectiveGuidance`): the conversion is per-field values
in `data/objectives/map-to-world.json`, empty, so every field says it isn't ready. A Debug build logs an
`objective place` or `objective key` record on `L` and `O` with what the checklist needs
([fit the objective markers](../contributors/how-to/fit-objective-markers.md)).

## Steps

1. `Find-MetaphorGame` (`scripts/player/MetaphorInstall.psm1`) for the install; the exe's SHA-256 matches the build
   above.
2. Strings: `grep -aoE` on the exe for class and path names with `MiniMap`, `MapIcon`, `Mission_`, `Destination`, then
   for `/%s.%s` layout folders; the table that lists `/mission/%s.%s` next to `mps` follows
   `COMMON/field/source/layout` and `f_%04d_%03d`. String file offsets turned into RVAs with the PE section table.
3. The `base.cpk` listing: `CpkArchive`'s TOC table (`src/Metaphor.Core/CpkArchive.cs`) read row by row from PowerShell
   (96,883 files), counted by folder under `COMMON/field/source/layout/`. Files read with `CpkArchive.Lookup` into a
   scratch folder outside the repository; all stored uncompressed.
4. A throwaway PowerShell parse of all 133 `mip` files and the four mission tables. Field rows' labels and texts by MD5
   against the `@MissionText_` labels compiled in the maintainer's local 2026-11-23 session log (grep only, not
   committed).
5. Navmesh tile bounds with `NavMeshFile.FromArchive` on `COMMON/model/field/navi/f_0001_00{1,2,3}.NAV`. The same log's
   `field beacon route` record names map 1 as `f_0001_001` and gives a player position of `(-2269.4, 24, 8609.8)`.
6. Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project on the build above, 2026-11-23, six
   `scripts/dev/ghidra.ps1 batch` runs, 56 queries: `global` on the path strings (no references for the
   `map_icon_point`, `MiniMap_*` and `MapEventIcon` paths; `0x4b9090` for the PG names); `vtable` on `fldMapIcon`
   (`0x180bbb8`), `fldExistMapIconPoint` (`0x17f99a0`), `fldMapEventIconDefine`, `fldMapLink`, `MiniMapLayout`,
   `FieldMapLayout` and the camp quest map's `Goal` and `Map`; `decompile 0x6d11f0`, `0x4b9090`, `0x5ee590` and
   `MiniMapLayout`'s own slots; `global 0x2ee7700`; `callers` on the `fldMapIcon` vtable's functions.
