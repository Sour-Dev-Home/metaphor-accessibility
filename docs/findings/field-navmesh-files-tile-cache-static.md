# Each map's `.NAV` file is a Recast tile-cache set the mod can read from `base.cpk` without calling the game

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's data files from `base.cpk` offline, as
  [`title-menu-item-images-2026-11-20.md`](../raw-sources/title-menu-item-images-2026-11-20.md) and
  [`voice-clip-families.md`](voice-clip-families.md) do (no ADR 0002 row covers data files, which no protection
  guards); and opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`recast-tile-cache-set-and-atl-header-2026-11-21.md`](../raw-sources/recast-tile-cache-set-and-atl-header-2026-11-21.md),
  [`recast-detour-navmesh-layout-2026-11-19.md`](../raw-sources/recast-detour-navmesh-layout-2026-11-19.md),
  [`field-navigation-accessibility-mods-2026-11-16.md`](../raw-sources/field-navigation-accessibility-mods-2026-11-16.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - FldPlayerWalkToPos | 0x8e9120 | .ecode | Field | known only | Script function that walks the player; it reaches `.text1` through a stub, so it is off-limits.
  - FldPlayerWalkToDoor | 0x8e8de0 | .ecode | Field | known only | Script function that walks the player to a door, like `FldPlayerWalkToPos`.

Spike #179, extending [`field-navmesh-and-path-queries-static.md`](field-navmesh-and-path-queries-static.md), whose
runtime reads (game session 4, #499) never ran. **Verdict: the path data exists in the files, high confidence; using
it for guidance and wall tones needs no game call and no new hook kind.** `COMMON/model/field/navi/` in `base.cpk`
holds 374 stored (uncompressed in the archive) `.NAV` files. Each is the Recast sample's tile-cache set, in the Atlus
variant Rirurin's crate names (version 3). Every tile is a 2.5D grid of walkable cells, FastLZ-compressed, that a few
hundred lines of plain C# can decode and search. What the files can't give: obstacles the game adds at run time
(gates, gimmicks), and the player's own collision.

| Question | Answer | Confidence |
|---|---|---|
| Navmesh file format? | Recast tile-cache set, magic `TEST`, version 3 (368 files), 2 (3), 1 (3) | High (headers match upstream and Rirurin) |
| Tile data? | Upstream `dtTileCacheLayerHeader` (`RLTD`, v1), then FastLZ heights, areas, connections | High: every tile of 362 of 367 files decodes to `3 × width × height` bytes |
| Cell size? | `cs` 20 and `ch` 3 world units in 358 files; tiles of 16 × 16 cells | High from the files; the unit (cm?) `[NEEDS VERIFICATION]` |
| Walkable area eroded by? | the agent radius: 50 (166 files) or 100 (191), others 5 to 300 | High that the field is there; that the game erodes by it as upstream does `[NEEDS VERIFICATION]` |
| Which file is the current map? | `f_<a>_<b>.NAV`, `<a>` and `<b>` from the map's row of `MapData.bin` ([`field-navmesh-file-from-map-data.md`](field-navmesh-file-from-map-data.md), #741) | High from the files |
| Run-time obstacles? | Yes: tile-cache obstacles (`maxObstacles` 256), toggled by the script function `FldGmcSetObstacleActive` | Medium (name and file field only) |
| Wall collision the player bumps? | PhysX character controller (`Cct` obstacle context RTTI), `.COL` files; `fldRayCast` is in `.text1` | Low, not followed |
| Game's own "walk the player to" | `FldPlayerWalkToPos` (`0x8e9120`) reaches `.text1` through the stub `0x576c40`: off-limits (#586) | High |

## Evidence

### 1. The files

The archive's table of contents (read as in the title-menu raw source) lists 374 files in `COMMON/model/field/navi/`,
all `.NAV`, none packed (`FileSize` = `ExtractSize`). The map `f_0000_014` also has `layout/collision/*.hit`,
`layout/enemy/*.enm`, `layout/locator/*.GFS` and `layout/pos/*.pos` files under `COMMON/field/source/`, so the
navmesh is named like the map's layout files. `COMMON/model/field/collision/` holds 2,252 `.COL` files with no common
magic; they weren't read further.

### 2. The header

Every `.NAV` starts with the bytes `54 45 53 54`, the upstream `TILECACHESET_MAGIC`. In `f_0000_014.NAV` (136,125
bytes, version 3, 283 tiles):

- `meshParams` at `+0xc`: `orig` (−6939.8, −515.7, −4103.0), tile width and height 240, `maxTiles` 8192, `maxPolys`
  512.
- `cacheParams` at `+0x28`: the same `orig`, `cs` 15, `ch` 20, width and height 16 cells, walkable height 150, radius
  100, climb 70, max simplification error 1.3, `maxTiles` 6384, `maxObstacles` 256. 16 × 15 = 240, the tile width.
- At `+0x5c`, Rirurin's `AgentSettings`: 15, 20, 150, 100, 70, slope 45, 8, 20, 12, 1.3, 6, 6, 1, partition 0, tile
  size 16; then `max_bound` (2077.7, 2027.7, 5842.2) and `min_bound`, which equals `orig`. The header ends at `+0xb0`.
- From `+0xb0` to the first tile (about 32 KB here) is a block Rirurin's crate doesn't describe: a count (1069 here),
  then floats including unit-cube corners (±0.5). Perhaps convex volumes or obstacle shapes `[NEEDS VERIFICATION]`.

### 3. The tiles

After that block come `numTiles` entries of upstream `TileCacheTileHeader` (`tileRef`, `dataSize`) plus `dataSize`
bytes. Each starts with a `dtTileCacheLayerHeader` (`RLTD`, version 1, tile x, y, layer, bounds, height range, 16 × 16).
The rest of the entry (121 bytes for the first tile here) decompresses with FastLZ to exactly 768 bytes, the upstream
heights, areas and connections.

- `f_0000_014.NAV`: all 283 tiles decode (280 in layer 0, 3 in layer 1, a second floor). Areas: 48,862 cells 63
  (`RC_WALKABLE_AREA`), 22,626 cells 0 (`RC_NULL_AREA`), 960 cells 62 (a game-defined area). Connection bits: 15 (all
  four neighbours) on 36,400 cells, 0 on 22,628 (the null cells, plus two), the rest partial: walls and edges.
- After the last of the `numTiles` tiles, 111 of the 367 version-3 files with tiles hold further runs (one to four or
  more) of tiles with the same layout (425 more in `f_0000_014`, after a 32-byte gap, refs in another range). What they are
  (another agent size, or alternative states for obstacles) is `[NEEDS VERIFICATION]`.
- Over all 367: the first run's length equals `numTiles` in every file; FastLZ decodes every tile in 362 files; in 5
  (`f_0001_003`, `f_0155_004`, `f_0183_042`, `f_3040_001`, `f_3040_002`) some tile fails, the throwaway decoder's
  fault or a variant `[NEEDS VERIFICATION]`. One version-3 file has no tiles.

### 4. Obstacles and collision in the exe

Strings and RTTI (a byte search of the exe, as in the earlier note): `CreateObstacle` and `RemoveObstacle`
(`.udata` `0x18056e8`, `0x18056f8`), PhysX's character-controller `ObstacleContext@Cct@physx`, and the script
function name `FldGmcSetObstacleActive` (`0x2113538`), registered in `nAppScript::FieldCommandDomain_Common::vf1`
(`0x8eae90`) for the function at `0x8df630` (script function raw source). So the game turns navmesh or controller
obstacles on and off from field scripts, and a navmesh read from the file alone will miss them.

### 5. The game's own walk

The script functions `FldPlayerWalkToPos` (`0x8e9120`), `FldPlayerWalkToDoor` (`0x8e8de0`) and their kin walk the
player in scenes. `0x8e9120` calls only `0x8da720`, which takes `fldBg` through `0x576840` and then calls `0x576c40`, a
thunk into `0x620e200` in `.text1`. A call into that chain is off-limits under the maintainer's rule of 2026-11-21
(#586). It shows the engine has a walk-to-point for the player, not a way for us to use it.

### 6. The Detour wrappers stay in `.ecode`

From the earlier note, one level down: `0x13f19b0` (path) calls `0x1543a30`, `0x1543b10`, `0x1541b30`, `0x1545d40`
and CRT helpers; `0x1543b10` and `0x1549370` (inside the raycast `0x1549b30`) call only `.ecode` functions and
`sqrtf`. None of them reaches `.text1` at that depth. Each matches once with `tools/Metaphor.SignatureCheck --probes`:

| Function | Pattern |
|---|---|
| `0x13f19b0`, path from A to B | `48 89 5C 24 10 48 89 74 24 18 55 57 41 54 41 56 41 57 48 8D AC 24 00 6F FF FF B8 00 92 00 00 E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33` |
| `0x13eeac0`, navmesh raycast | `48 89 5C 24 10 48 89 74 24 18 55 57 41 56 48 8D AC 24 10 FB FF FF 48 81 EC F0 05 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 E0 04 00 00 41 BB` |
| `0x13f2930`, nearest navmesh point | `4C 8B DC 49 89 5B 10 56 57 41 56 48 81 EC 80 01 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 70 01 00 00 41 C7 01 00 00 00 00 4D 8B F1 48 83` |
| `0x576410`, the free-roam frame's navmesh snap | `48 8B C4 48 89 58 10 57 48 81 EC B0 00 00 00 48 8B D9 C5 F8 29 70 E8 48 8B 0D ?? ?? ?? ?? C5 78 29 40 C8 C5 78 28 C1 48 8B 49 38 E8` |

## What each feature needs

- **Direction and distance (#183).** The player's position and facing and a target's position, which the
  [position](field-position-facing-map-static.md) and [units](field-units-names-exits-static.md) notes already reach.
  No navmesh.
- **"No path" (#183).** Which cell the player and the target stand in, and a flood fill over the cells' connection
  and portal bits across tiles and layers: unreachable means no path. Read from the file, it misses run-time obstacles,
  so a closed gate could read as a path. The live `dtNavMesh` behind `fldBg` (earlier note) has them, but its reading
  is untested.
- **Wall tones (#185).** From the player's cell, step along the facing (and left, right, behind) until a cell isn't
  connected; that distance plus the erosion radius is a wall's distance. The cells mark where an agent's centre can
  stand, so a ledge sounds like a wall, and a wall the player brushes but an enemy can't reach may read early or late.
  Arcana measured the real body instead (capsule sweeps); that needs the PhysX scene, in `.text1`.
- **Auto-walk (#184).** The same cells give a route (A* and string-pulling over cell centres). Moving the player still
  needs input injection, behaviour-changing under ADR 0007; the game's own walk is off-limits (section 5). Calling
  `0x13f19b0` on the game thread would give the game's own path, but no rule covers calling a game function yet.

## By-ear and inspector checks for the next game session

1. The earlier note's stops (`docs/sessions/session-4/stop-1-town.txt`, `stop-2-dungeon.txt`) still stand: `N`
   non-null in town and dungeon, the tile magic `DNAV` version 7 in the live navmesh.
2. Map id to file: in a town room and the first dungeon room, note the map id and position; offline, find the `.NAV`
   whose bounds hold the position and whose walkable cells hold it.
3. Walk into a wall, along a corridor and to a ledge: compare each stopping position with the nearest unconnected
   cell. A gap far beyond the erosion radius means the file's cells don't describe what the player bumps.
4. Open a gate or trigger a gimmick that blocks a path: whether the live navmesh changes (obstacle count) and the file
   doesn't.
5. By ear, once a prototype exists (NVDA on): a "no path" cue for a target behind a closed gate, and wall tones in the
   first dungeon's corridor.

## Steps

Python 3.12.10, throwaway standard-library scripts kept outside the repository, 2026-11-21, reading `base.cpk` in
place (nothing extracted or kept):

1. The table of contents, as in the title-menu raw source: list entries whose directory ends `model/field/navi`, and
   those sharing `f_0000_014`; count `.COL` files and their first four bytes.
2. For each `.NAV`: the header fields above; walk the tile entries from the first `RLTD` (minus 8 bytes) while each
   entry's data starts `RLTD`; then search for further runs. FastLZ-decode each tile's bytes after the `0x38`-byte
   header, written from the FastLZ format (level from the first byte's top three bits); LZ4 was tried too and failed
   on every tile. Count area values and connection bits for `f_0000_014`.
3. The exe: a byte search for `bstacle`, `TileCache`, `Raycast`, `RayCast` with the section table to turn file
   offsets into RVAs.
4. The shared Ghidra project (Ghidra 12.2, JDK 25.0.5+8, built by `pwsh scripts/dev/ghidra.ps1 build` on this build):
   `global 0x2113538`, `callees 0x8e9120`, `callees 0x8da720`, `decompile 0x8da720`, `callers 0x13f19b0`,
   `callees 0x13f19b0`, `callees 0x13eeac0`, `callees 0x1549b30`, `callees 0x1549370`, `callees 0x1543b10`.
5. `tools/Metaphor.SignatureCheck --bytes <exe> <rva> 48` for the four functions, then `--probes <exe> <file>` with
   the patterns above in a scratch probes file: 4 of 4 matched once.

Nothing in `.text1` was decompiled; the thunk at `0x576c40` was noted and not followed. No decompiled output and no
game data is quoted beyond header numbers and counts.
