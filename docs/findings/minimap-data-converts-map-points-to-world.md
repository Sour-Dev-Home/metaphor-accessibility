# A map's minimap record converts its icon points to world `x, z`: `x = Ox − s·px`, `z = Oz − s·py`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own data archives (CPK) offline with our read-only tool
  (`tools/Metaphor.CpkExtract`), output kept in git-ignored `extracted/`; opening the shipped executable in a
  disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; follows [`objective-map-icon-points-static.md`](objective-map-icon-points-static.md)
  and [`field-navmesh-file-from-map-data.md`](field-navmesh-file-from-map-data.md)
- **Functions:**
  - FieldLayoutLoad | 0x835fa0 | .ecode | Field | known only | Loads a field's optional layout files by kind (`pos`, `enemy`, `tbox`, `minimap_bb`, `mission`, ...) from `f_%04d_%03d`.

For #652. **The conversion is data, not a fit.** `MiniMap_SetData.bin`'s row whose id is the map id lists that map's
`MiniMap_Data.bin` records, one per floor; a record holds `(Ox, Oz, s)`, and an icon point `(px, py)` from the map's
`mip` file is at world `x = Ox − s × px`, `z = Oz − s × py`. In the royal capital 27 of 35 objective markers land on a
walkable navmesh cell or within two 20-unit cells of it; the misses sit at building icons. The `.bbx` files are not
the conversion.
Confidence: high for the formula and the map-1 to map-3 join, medium for the join on other maps (few points each).

| Map | Field | Record | `Ox`, `Oz`, `s` | Kind-4 points on the mesh (≤ 40 units) |
|---|---|---|---|---|
| 1 | `f_0001_001` | 1 | 18807, 21899, 18 | 10 of 12; rows 52 and 92's second point 1,143 and 638 off |
| 2 | `f_0001_002` | 6 | 988, 31786, 18 | 8 of 11; the three at the inn (row 9's), 1,330 off |
| 3 | `f_0001_003` | 11 | −5347, 51380, 18 | 9 of 12; the three at `(731, 640)` (rows 3, 22, 23), 2,745 off |

In `data/objectives/map-to-world.json`'s terms (`world = origin + scale × point`), map 1 is `originX` 18807, `originZ`
21899, `scaleX` and `scaleZ` −18.

## Evidence

### The tables

Both files have the header the game's other `ui` tables use: `u64` version 2, `u64` row count, `u64` field count,
`u64` row size, then one `u64` offset per field, the rows after it. Sizes match exactly.

- **`MiniMap_Data.bin`** (21,064 bytes): 1,500 rows of 14 bytes: `u16` id (equal to the row), `f32` `Ox`, `f32` `Oz`,
  `f32` `s`. 262 rows are not `(0, 0, 1)`. Scales are whole numbers (20 in 105 rows, 17 in 34, 18 in 31, up to 50).
- **`MiniMap_SetData.bin`** (18,800 bytes): 300 rows of 62 bytes: `u16` map id, ten `u16` record ids, ten `f32`
  heights (`-1` when unused). Map 1 is `1, 1, 0…, −1030, −1…`; map 12 (`f_0004_001`) lists records 56 and 57 with
  heights −3500 and −600. 258 rows have one record, 42 have two to nine; 289 rows' first record is `5 × row + 1`, so
  each map has five record slots. 147 rows point only at `(0, 0, 1)` records: maps with no minimap.
- **Floors.** In only 3 rows (maps 14, 15, 114) do a map's records differ; elsewhere every floor has the same `Ox, Oz,
  s`. That a height picks the floor by the player's `y` is `[NEEDS VERIFICATION]`.

### The fit

The world axes are `x` and `z`, `y` up, as the navmesh note and the logged player positions have them. The mod's
`NavMeshFile` and `MapIconPoints` readers on the extracted files; a point counts as on the mesh when the nearest
walkable cell (area not 0) is within 40 units, two cells.

- **Royal capital** (table above): the shared exit marker `(1045, 295)` on map 1 lands at `(−3, 16589)`, on a cell;
  the Hushed Honeybee Inn's `(1478, 1025)` at `(−7797, 3449)`, 2 units off. The misses are icons on buildings (the inn
  on map 2; the provisions queue, tournament registration and bounty spot at `(731, 640)` on map 3) and most kind-2
  points, which sit off the walkable mesh as a building's footprint would `[NEEDS VERIFICATION]`. With `+s` instead
  of `−s` on either axis, maps 2 and 3's points fall outside their walkable bounds.
- **Every map with both files**: 96 fields have a `mip` file and a `.NAV`; 93 have a record. Over their 267 points
  the median distance is 0 and 183 are within 40 units (kind 4: 86 of 118). 57 of the 93 fields have half their
  points or more on a walkable cell. Most fields hold one to four points, and on a large mesh a wrong record also
  lands that few on cells by chance, so the join on those fields is weaker evidence than maps 1 to 3.
- **The logged player.** The 2026-11-23 Debug log's map-1 position `(−2269.4, 24, 8609.8)` converts back to point
  `(1170.9, 738.3)`, inside the range map 1's icon points span (758 to 1490, 295 to 1120); the session's four other
  map-1 positions land within 60 points of it. That only says the frame is plausible: no marker was stood on.

### The `.bbx` files are volumes, not the conversion

`COMMON/field/source/layout/minimap_bb/<field>.bbx`, 7 files: a 16-byte header (`BBX\0`, `u32` 10, `u32`
`0x20191024`, `u32` last index), then `0xa0`-byte entries: `u32` index, a 64-byte name, `f32` translate `x, y, z`,
rotate `x, y, z`, scale `x, y, z`, size `x, y, z`, then a `u32` value and a `u32`, the rest zero.

- `f_0001_001`, `_002` and `_003` each hold one identical cube, `pCube2` at `(−362.8, 0, −393.8)`, size 200, value
  10: the same bytes as the last of `f_2000_001`'s five cubes. That spot is outside maps 2 and 3's walkable bounds
  (`x` −26,653 to −8,193 and −36,760 to −10,280), so in the capital the files are a copied placeholder.
- `f_0173_032`, `f_0175_012` and `f_0182_032` hold one `bb_001` box at the origin, sizes 1000 to 1400, values 1, 1, 2.
- The exe names the format only in the field layout loader `0x835fa0`'s list of layout files (`bbx` beside
  `/minimap_bb/%s.%s`, between `/action/` and `/mission/`); that loader takes every kind as optional, which fits only 7
  fields having one. What the value does (a minimap floor or area for the volume) is `[NEEDS VERIFICATION]`; nothing
  the conversion needs.

### Where the game reads them

The `COMMON/ui/map/MiniMap_Data.bin` and `MiniMap_SetData.bin` path strings (`0x17f9af0`, `0x17f9b10`) and the bare
table names (`0x2136bb0`, `0x2136bc0`) have no reference in analysed code, so the reader is out of reach; the data
alone settles the formula.

## Build plan

For the objective guidance key (#652), replacing the hand fit in
[fit the objective markers](../contributors/how-to/fit-objective-markers.md):

1. **Reader.** A `MiniMapFrames` reader in `Metaphor.Features.Field` beside `MapIconPoints`: read both tables from
   `base.cpk` with `CpkArchive` (bounded sizes: 21,064 and 18,800 bytes; any other header is "no conversion", logged),
   and map a map id to its first non-default record's `(Ox, Oz, s)`. Unit tests on made-up tables.
2. **Conversion.** An `IMapToWorld` that gives `(Ox − s × px, Oz − s × py)` for the current map id. Keep
   `map-to-world.json` as an override for a field that turns out wrong; no field needs it on present evidence.
3. **Floors.** Use the first record until the floor rule is checked; the three maps whose floors differ say so in the
   Debug log.
4. **Live check.** One stop on a capital marker (row 11 on map 1, `(1045, 295)` → `(−3, 16589)`) with `L`; pass when
   the player is within a few metres of it.

## Steps

1. `Find-MetaphorGame` (`scripts/player/MetaphorInstall.psm1`) for `METAPHOR_GAME_EXE`; the exe's SHA-256 matches the
   build above. .NET SDK 9.0.319, `tools/Metaphor.CpkExtract` at `a020a205`, 2026-11-24:
   `dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk`, then `extract base.cpk
   COMMON/ui/map/MiniMap_Data.bin COMMON/ui/map/MiniMap_SetData.bin COMMON/init/data/MapData.bin`, the 7
   `minimap_bb/*.bbx`, the 134 `map_icon_point/*` files and the 111 `navi/f_<a>_<b>.NAV` files named like them,
   `--out extracted/minimap`.
2. Python 3.12.10, throwaway scripts outside the repository's tracked files: the table header and rows of both
   `MiniMap_*` files; the `.bbx` entries.
3. A throwaway console program on the branch's `Metaphor.Core` and `Metaphor.Features.Field`: for each `MapData` row
   (`+0x8`, `+0xa` give the field), its `mip_<a>_<b>_000.dat` points through `MapIconPoints.Parse`, its `.NAV` through
   `NavMeshFile.Parse`, each record of the map's `SetData` row applied, and the distance from each converted point to
   the nearest walkable cell. Printed counts and distances only.
4. The player positions: `grep` of the `field beacon route` lines in the maintainer's local session logs (not
   committed).
5. Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project on the build above, one
   `scripts/dev/ghidra.ps1 batch` of 9 `global` queries on the path strings (found by a byte search of the exe, turned
   into RVAs with its section table), and `callers 0x835fa0` in a second.
