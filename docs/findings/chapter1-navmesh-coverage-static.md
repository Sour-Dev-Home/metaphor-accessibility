# Every chapter 1 field map but the Castle Gates has a navmesh, so wall tones and route pings need no raycast for v0.1.0

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own data archives (CPK) offline with our read-only tool (`tools/Metaphor.CpkExtract`),
  output kept in git-ignored `extracted/`; the live check is our own Reloaded-II mod's message log
- **Sources:** none, see Steps; builds on [`field-navmesh-file-from-map-data.md`](field-navmesh-file-from-map-data.md),
  [`field-navmesh-reader-decodes-every-file.md`](field-navmesh-reader-decodes-every-file.md),
  [`field-map-and-objective-tables-static.md`](field-map-and-objective-tables-static.md) and
  [`field-wall-probe-raycast-static.md`](field-wall-probe-raycast-static.md)

For #546 and the navigation kit (#823). Of the 32 `MapData.bin` rows in the regions chapter 1 plays in (Grand Trad,
the study, the desert, the fort, the Nord mine), 31 name a `.NAV` file the archive has. The one without is map 4,
Castle Gates. Every `.NAV` file decodes whole ([reader note](field-navmesh-reader-decodes-every-file.md)), so on
those 31 maps `WallTones` and `RoutePings` work from `WalkGraph` as built. **Recommendation for v0.1.0: ship the
navmesh path only.** On a map with no navmesh, keep what the code already does: say "No wall tones on this map" once,
and fall back to straight-line pings. Leave the raycast `0x13e2530` until after v0.1.0, unless the by-ear run (#627)
finds a playable chapter 1 stretch on map 4, or navmesh edges that mislead at doors and people.

## Evidence

Map rows are decoded as in the [map table note](field-map-and-objective-tables-static.md): area at `u16 +0x6`, file
`f_<+0x8>_<+0xa>.NAV`, map name by the label MD5 at `+0x1c`, matched against `EN/message/MapName.msg`. "Logged"
means a `map strip id=` or `field place map=` record in the maintainer's sessions of 2026-11-21 to 2026-11-23
(12 sessions).

| Map ids | Name (`MapName.msg`) | Area | `.NAV` | Logged |
|---|---|---|---|---|
| 389 | Tradia Desert Entrance | 0 | `f_0195_001` yes | no |
| 137, 151 | Sandstorm Barrens, Main Road | 8 | `f_0150_001`, `f_0150_004` yes | both |
| 138, 150 | Royal Army Camp, Watchtower Roof | 9 | `f_0150_002`, `f_0150_005` yes | 138 |
| 374 | Mountain Forest Camp | 9 | `f_0150_006` yes | no |
| 1, 2, 3 | Sunlumeo Street, Sunshade Row, Regalith Grand Cathedral | 0 | `f_0001_001` to `_003` yes | all |
| **4** | **Castle Gates** | 0 | **`f_0001_004` not in the archive** | no |
| 20, 21 | Hushed Honeybee Inn, Naŭ Filono Tavern | 0 | `f_0011_001`, `_002` yes | no |
| 22 to 28 | Spado Smithy to Lycaon Magic Association (7 shops and halls) | 0 | `f_0011_003` to `_010` yes | 22, 26 |
| 58 | Novelist's Study | 6 | `f_0016_001` yes | yes |
| 139 to 149 | Nord Forest Trail to Southern Entrance (the mine, 11 rows) | 10 | `f_0151_001` to `_011` yes | no |
| 499 | Rest Area (`MapName_Dungeon02_11`) | 10 | `f_3020_001` yes | no |

- **Loaded live.** The session logs show the mod's own reader loading chapter 1 files whole: `f_0150_001.NAV` (the
  desert, the largest file at 3,607 tiles) in 26 ms, `f_0001_001` (2,523 tiles) in 14 ms, `f_0001_003` (2,392) in
  12 ms, `f_0016_001` (169) in 5 ms, all `0 skipped`. Earlier sessions logged `unreadable` for maps 1 and 138 only
  because of the old `f_0000_<id>` guess, which #741 fixed.
- **Fallbacks already in the code.** `WallTones.NoNavMeshText` ("No wall tones on this map") is said once per map with
  no navmesh (`src/Metaphor.Features.Field/WallTones.cs`). `RoutePings` treats "no route planned" as straight there,
  and `Routes` returns `RouteOutcome.NoNavMesh` for `file=none` (`src/Metaphor.Core/Navigation/Routes.cs`).
- **Scope is approximate.** The rows come from the regions in the
  [pilot inventory](../raw-sources/chapter1-interaction-inventory-2026-11-20.md) (desert, capital, study, fort, mine).
  Whether chapter 1 lets the player walk on 4, 21, 23 to 25, 27, 28, 374 and 499 is `[NEEDS VERIFICATION]` by the
  #627 run. Area 7 (the Gauntlet Runner's decks and the camps, 71 rows, 47 of them without a navmesh) comes after
  chapter 1 by the walkthrough order in [chapter structure](../raw-sources/metaphor-chapter-structure-2026-11-20.md)
  `[NEEDS VERIFICATION]`.
- **Unnamed rows.** 79 rows (59 to 113 and 500 to 523) hold a label MD5 that matches no label in any English message
  file. All but 3 have no navmesh. What they are (cutscene or event stages?) is `[NEEDS VERIFICATION]`. None was logged
  in a session.
- Overall: 354 of 606 rows name a file the archive has and 252 don't (row 0 included), as the
  [file note](field-navmesh-file-from-map-data.md) counted.

## Steps

.NET SDK 9.0.319, Python 3.12.10, 2026-11-24. `METAPHOR_GAME_EXE` from `Find-MetaphorGame`
(`scripts/player/MetaphorInstall.psm1`), its SHA-256 matching the header.

1. `dotnet run --project tools/Metaphor.CpkExtract -c Release -v q -- list base.cpk navi`: 374 `.NAV` files, plus
   `COMMON/field/source/bin/Navi_Data.bin`.
2. `... -- extract base.cpk COMMON/init/data/MapData.bin EN/message/MapName.msg --out extracted/audit-ch1`.
3. A throwaway Python script outside the repository decoded every row (area, file, label MD5), hashed every
   `MapName.msg` label with MD5, and checked each file name against the listing.
4. `grep -E "map strip id=|field place map=|navmesh: "` over each session's `message-log.txt` for the logged maps and
   load times.
