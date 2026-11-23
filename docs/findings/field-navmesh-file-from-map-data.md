# A map's `.NAV` file is named by its row in `MapData.bin`, not by splitting the map id

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's data files from `base.cpk` offline, as
  [`field-navmesh-files-tile-cache-static.md`](field-navmesh-files-tile-cache-static.md) (#639) does (no ADR 0002 row
  covers data files, which no protection guards); nothing extracted or kept
- **Sources:** none, see Steps; the table read is
  [`field-position-facing-map-static.md`](field-position-facing-map-static.md)'s `MapData` (`+0x10 + id × 0x2c`)

Issue #741. In the 2026-11-22 session no route was ever planned because `NavMapFiles.Guess` (`f_<id/1000>_<id%1000>`)
named files the archive doesn't have: `f_0000_001`, `f_0000_138`, `f_0000_371` and `f_0000_372` are not among the 374
`.NAV` files in `COMMON/model/field/navi/`. The files were missing, not unparsable. **The map's file is in its row of
`COMMON/init/data/MapData.bin`: row `id`'s `u16`s at `+0x8` and `+0xa` are `<a>` and `<b>` of `f_<a>_<b>.NAV`.**
High confidence from the files; that the game's own map uses that navmesh is `[NEEDS VERIFICATION]` by ear.

| Map id | Row `+0x8`, `+0xa` | File | Read by `NavMeshFile` |
|---|---|---|---|
| 1 | 1, 1 | `f_0001_001.NAV` | 2,523 tiles, 0 skipped |
| 2 | 1, 2 | `f_0001_002.NAV` | 749 tiles |
| 12 | 4, 1 | `f_0004_001.NAV` | 6,879 tiles |
| 138 | 150, 2 | `f_0150_002.NAV` | 1,189 tiles |
| 285 | 180, 2 | `f_0180_002.NAV` | 2,709 tiles |
| 371 | 178, 41 | `f_0178_041.NAV` | 1,569 tiles |
| 372 | 178, 42 | `f_0178_042.NAV` | 3,617 tiles |

## Evidence

- **The table.** `MapData.bin` is 26,680 bytes, stored. Its first 16 bytes are `MAPD`, `0`, `0x25e` (606) and `0`;
  16 + 606 × 44 = 26,680, so it is 606 rows of `0x2c` bytes from `+0x10`, the layout `FldTravelGetZoneIDByMapID`
  reads by map id in the note above. Row 0 names no file (`0xffff`). Rows 371, 372 and 373 share their zone (`+0x6` = 39) and differ in `+0xa`
  (41, 42, 43), as neighbouring rooms would.
- **The names match.** Of the 605 rows that name a pair, 354 name a file the archive has, each a different file:
  354 of the 374 files. The other 251 name none (the `f_0017_*` run, for one), maps with no navmesh. The 20 files no
  row names include `f_0000_014`, `f_0000_017` and other `f_0000_*`: the only ones the old guess could ever reach.
- **Session ids.** Every map id the sessions logged (1, 2, 12, 138, 285, 371, 372) names a file that exists and
  decodes whole (table above).
- **A logged position lands on the mesh.** The session's mark-18 door on map 372, `(972, 5800, 346.5)`, has a walkable
  cell in `f_0178_042.NAV` (tile 3239, cell 5,4) within 12 cells and 200 units up or down. With the second and third
  coordinates swapped it has none.
- What the 20 unnamed files are for (cutscenes, unused maps) is `[NEEDS VERIFICATION]`.

## Steps

.NET SDK 9.0.319, 2026-11-22, game folder found by `Find-MetaphorGame` (`scripts/player/MetaphorInstall.psm1`), the
executable's SHA-256 checked against the header. A throwaway console program outside the repository, referencing the
branch's `Metaphor.Core.dll`:

1. Read `base.cpk`'s table of contents with `UtfTable` and listed the rows under `COMMON/model/field/navi/` (374) and
   those named `MapData` (`COMMON/init/data/MapData.bin`, `BgMapData.bin`).
2. Read `MapData.bin` with `CpkArchive.ReadFile`, printed the header and the `u16`s of rows 0 to 3, 12, 138, 285 and
   371 to 373, and for every row formatted `f_<+0x8:D4>_<+0xa:D3>.NAV` and counted matches against the listing.
3. Ran the fix's `NavMapFiles.FromArchive` and `NavMeshSource.Load` for the session ids, and `WalkGraph.NearestCell`
   for the mark-18 door. It printed counts, names and cells only.
