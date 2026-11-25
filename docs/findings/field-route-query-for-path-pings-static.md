# Path pings can use the mod's own navmesh routes; the game's Detour path query is a callable fallback

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-navmesh-and-path-queries-static.md`](field-navmesh-and-path-queries-static.md),
  [`field-navmesh-file-from-map-data.md`](field-navmesh-file-from-map-data.md),
  [`field-navmesh-reader-decodes-every-file.md`](field-navmesh-reader-decodes-every-file.md),
  [`field-wall-probe-raycast-static.md`](field-wall-probe-raycast-static.md)
- **Functions:**
  - FieldNavFindPath | 0x13f19b0 | .ecode | Field | known only | Detour nearest-poly, findPath and findStraightPath from a start to an end point into a caller's corner buffer. | finding
  - FieldNavRaycast | 0x13eeac0 | .ecode | Field | known only | Detour walk-line test: how far a straight walk from A to B gets on the navmesh. | finding

Issue #832, for path pings (#823). **Found, and it needs no game call.** The mod already plans a route from the
player to a place over the map's navmesh, in plain C#. The beacon does it today (#690): `NavRoutePlanner`
(`src/Metaphor.Core/Navigation/Routes.cs`) returns a `RoutePlan` whose `Corners` are the points to walk to in turn.
Path pings can ping the next corner instead of the target. The game's own Detour path query (`0x13f19b0`) is a
whole `.ecode` function whose first 24 bytes match once, so a call is possible later, but nothing needs it yet. The
game itself draws no route line in the field: the exe names only enemy patrol routes and the world map's route
picker. High confidence in the code; no route has been heard to work in game yet `[NEEDS VERIFICATION]`.

| Question | Answer | Confidence |
|---|---|---|
| Can the mod get a route? | Yes, offline: `.NAV` file by the map's `MapData` row (#741), `PathFinder.Path`, corners in `RoutePlan` | High (unit tests, all 374 files decode) |
| Is it the game's route? | The same navmesh files the game loads `[NEEDS VERIFICATION]`; our A* over cells, not the game's `findPath` | Medium |
| Maps without a route | 251 `MapData` rows name no `.NAV`; there `RouteOutcome.NoNavMesh`, and straight-line pings (#831) stand in | High for the count |
| Game path query | `0x13f19b0(N, …)`, `N = [fldBg+0x48]`; used by `fldCharaCtrl_Enemy::vf7` and five other field functions | High for the shape |
| Callable? | `.ecode`, unique 24-byte prologue; game thread only (shared node pool), ~37 KB of stack | High on disk, medium for the rest |
| A game route line to reuse? | None in the field found: strings name `AI_Route_`, `FldEnemySetAIRouteID` and `SortieMapUI_*Route*` only | Medium (string search) |

## Evidence

- **The mod's route.** `NavRoutePlanner` takes a map id and two points and returns a `RoutePlan`: `Found` with
  corners, or `Loading`, `NoNavMesh`, `OffMesh` or `NoPath`. Under it, `PathFinder.Path` runs A* over the navmesh's walk cells
  (bounded by `WalkGraph.DefaultMaxVisited`) and cuts the path to corners by line of sight. `NavMapFiles` names the map's file from `COMMON/init/data/MapData.bin`
  ([note](field-navmesh-file-from-map-data.md)), and every tile of every file decodes
  ([note](field-navmesh-reader-decodes-every-file.md)). The beacon asks for a plan off the game thread and follows it
  with `RouteFollower` (`src/Metaphor.Features.Field/Beacon.cs`). The 2026-11-22 session planned no route because of
  #741; no session since has logged a `Found` plan, so that it guides right in game is `[NEEDS VERIFICATION]`.
- **The game's query.** `0x13f19b0` runs nearest-poly on both ends, `findPath` into 256 polys, then `findStraightPath`
  into the caller's buffer (count at `+0x1500`, limit `+0x1504`), as the
  [navmesh note](field-navmesh-and-path-queries-static.md#2-the-detour-functions-by-their-wrappers) shows; the
  Detour names are `[NEEDS VERIFICATION]` per function. Its prologue reserves `0x9200` bytes of stack (`mov eax,0x9200`
  then a stack-probe call). Its first 24 bytes, `48 89 5C 24 10 48 89 74 24 18 55 57 41 54 41 56 41 57 48 8D AC 24 00 6F`,
  match once in `.ecode`. `0x13eeac0` (the walk-line test) matches once on its first 24 bytes too.
- **No game route line.** A byte search of the exe for names with `Route`, `GuideLine`, `NaviLine`, `PathFind` or
  `Waypoint` finds enemy AI routes (`AI_Route_`, `FldEnemySetIsAIRouteLoop`), battle `RoutePath`, and the sortie
  (world) map's route picker (`SortieMapUI_AddRoute`, `SortieMapUI_StartRouteSelect`). None is a field guide to a
  destination.

## Build plan

1. **Path pings on the beacon's routes (no new hook).** The ping source asks the same `IRoutePlanner` as the beacon,
   pings the `RouteFollower`'s next corner while the plan is `Found`, and falls back to the straight-line ping (#831)
   on `NoNavMesh`, `OffMesh` or `NoPath`, saying which once, as the beacon does. Unit tests in
   `Metaphor.Features.Field` with a fake planner. This fits v0.1.0.
2. **By-ear check first.** In a dungeon room with a `.NAV` file, ask the beacon for a target behind a wall and confirm
   the log's `beacon route` line says `Found`. The route should bend round the wall. That one check covers both the
   beacon and path pings.
3. **Game query, only if our routes disagree with the walls.** Add `FieldNavFindPath` as a signature and call it from
   a free-roam frame hook as `field-wall-probe-raycast-static.md` plans for the raycast: `N` from the existing
   `FieldNavmeshSnap` load (`fldWorld` → `fldBg` → `+0x48`), a native buffer of at least `0x1508` bytes, after the game's
   own path searches that frame. It needs the same rule 10 listing, a security review and a session check.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project on the build above, Windows 11 x64,
2026-11-23. The Ghidra queries are shared with the wall-probe spike ([its Steps](field-wall-probe-raycast-static.md#steps));
none was needed beyond them, since the [navmesh note](field-navmesh-and-path-queries-static.md#steps) already traced
`0x13f19b0`. Here:

1. Read `src/Metaphor.Core/Navigation/` (`Routes.cs`, `PathFinder.cs`, `WalkGraph.cs`, `NavMapFiles.cs`, `NavMeshSource.cs`) and
   `src/Metaphor.Features.Field/Beacon.cs` at the commit this note lands on.
2. A throwaway PowerShell snippet read `.ecode`'s raw bytes from the exe on disk and counted matches of the first 24,
   32 and 48 bytes of `0x13f19b0` and `0x13eeac0` (one each).
3. A byte search of the exe for printable names matching `Route`, `GuideLine`, `NaviLine`, `PathFind`, `Waypoint`
   (nothing written).
