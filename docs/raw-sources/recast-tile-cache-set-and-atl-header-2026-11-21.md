# Recast's tile-cache set file, and Rirurin's reading of the Atlus variant (#179)

- **Source:** two GitHub repositories, read through the GitHub API (`gh api repos/<repo>/contents/<file>?ref=<sha>`);
  nothing was downloaded into the repository, built or run.
  - `recastnavigation/recastnavigation` (Zlib licence) at `9f4ce64458dfae86e1239c525ddc219c4e9e06f1`, the commit
    [`recast-detour-navmesh-layout-2026-11-19.md`](recast-detour-navmesh-layout-2026-11-19.md) read:
    `RecastDemo/Source/Sample_TempObstacles.cpp`, `DetourTileCache/Include/DetourTileCache.h`,
    `DetourTileCache/Include/DetourTileCacheBuilder.h`, `DetourTileCache/Source/DetourTileCacheBuilder.cpp`,
    `Recast/Source/RecastLayers.cpp`, `Recast/Include/Recast.h`.
  - `rirurin/recast-navigation-rs` (GPL-3.0, by Rirurin; its `Cargo.toml` names the repository
    `rirurin/recast-navigation-atl`) at `8a9f8065535ed47c6ba75509c418c6c94f94fc39` (2026-03-19):
    `recast-nav-atl/src/input_geometry.rs`, `recast-nav-atl/src/tile_cache.rs`, `recast-nav-atl-sys/build.rs`.
- **Saved:** 2026-11-21.
- **Game build:** none. Rirurin's code names the Steam prologue demo 1.01; the checks against the full game's files are
  in [`field-navmesh-files-tile-cache-static.md`](../findings/field-navmesh-files-tile-cache-static.md).

Ground rule 3 search for #179, 2026-11-21: a web search for the game's navmesh or collision file formats found nothing
about this game; GitHub repository and code searches (`metaphor refantazio`, `TileCacheSetHeader`, `model/field/navi`)
found only Rirurin's crate. Nothing below is copied into our code; it is the layout in our words with short quotes.

## Upstream: the sample's tile-cache set file

- `Sample_TempObstacles.cpp` line 53: `TILECACHESET_MAGIC = 'T' << 24 | 'S' << 16 | 'E' << 8 | 'T'`, so the int
  `0x54534554`, stored little-endian as the bytes `54 45 53 54` ("TEST" read as text). Line 54: version `1`.
- Lines 1471-1484: the file starts with `TileCacheSetHeader` (`int magic`, `int version`, `int numTiles`,
  `dtNavMeshParams meshParams`, `dtTileCacheParams cacheParams`), then for each tile a `TileCacheTileHeader`
  (`dtCompressedTileRef tileRef`, `int dataSize`) followed by `dataSize` bytes of tile data (`saveAll`, from line 1486).
- `dtNavMeshParams` is `0x1c` bytes (the navmesh raw source). `dtTileCacheParams` (`DetourTileCache.h`): `float
  orig[3]`, `float cs, ch`, `int width, height`, `float walkableHeight, walkableRadius, walkableClimb,
  maxSimplificationError`, `int maxTiles, maxObstacles`: `0x34` bytes. So the upstream header is `0x5c` bytes.
- The sample's compressor is FastLZ: `struct FastLZCompressor : dtTileCacheCompressor` (line 356), calling
  `fastlz_compress` and `fastlz_decompress`.
- Before building layers the sample erodes the walkable area by the agent radius:
  `rcErodeWalkableArea(buildContext, tcfg.walkableRadius, ...)` (line 646), then `rcBuildHeightfieldLayers` (line 671).
- Obstacles: `DetourTileCache.h` declares cylinder, box and oriented-box obstacles (`addObstacle`, `addBoxObstacle`,
  lines 137-140); `update` (line 150) "Updates the tile cache by rebuilding tiles touched by unfinished obstacle
  requests" into the live `dtNavMesh`.

## Upstream: one compressed tile

- `DetourTileCacheBuilder.h` line 25: `DT_TILECACHE_MAGIC = 'D'<<24 | 'T'<<16 | 'L'<<8 | 'R'`, bytes `52 4C 54 44`
  ("RLTD"); line 26: version `1`.
- `dtTileCacheLayerHeader`: `int magic, version`, `int tx, ty, tlayer`, `float bmin[3], bmax[3]`, `unsigned short
  hmin, hmax`, `unsigned char width, height, minx, maxx, miny, maxy`: `0x38` bytes.
- `dtBuildTileCacheLayer` (`DetourTileCacheBuilder.cpp`) writes the header uncompressed (aligned to 4), then
  compresses one buffer of `gridSize*3` bytes, `gridSize = width * height`: the heights, then the areas, then the
  connections, `gridSize` bytes each.
- `RecastLayers.cpp` lines 609-644, per cell: `heights[idx] = s.y - hmin` (in cell heights `ch` above the header's
  `hmin`), `areas[idx]` the area id, and `cons[idx] = (portal << 4) | con`: bit `dir` (0-3) of `con` set when the
  neighbour in that direction is walkable and in the same layer, bit `dir` of `portal` when it is walkable in another
  layer.
- `Recast.h` lines 635 and 640: `RC_NULL_AREA = 0` (not walkable), `RC_WALKABLE_AREA = 63`.

## Rirurin's crate: the Atlus variant

- `input_geometry.rs` lines 17-20: the same magic, `TILECACHESET_VERSION = 1`, `TILECACHESET_INCLUDE_OFFMESH = 2` and
  `TILECACHESET_ATL_VERSION = 3`.
- `TileCacheSetHeaderAtl` (lines 93-97): the upstream header, then `AgentSettings`, then `max_bound` and `min_bound`
  (`Vec3` each). `AgentSettings`: 13 floats (cell size, cell height, agent height, agent radius, agent max climb, agent
  max slope in degrees, region min size, region merge size, edge max length, edge max error, verts per poly, detail
  sample distance, detail sample max error) and two `u32`s (partition type, tile size in voxels): `0x3c` bytes.
- Its `Default` cites "Original function: 0x1413d6a10 (Metaphor: Refantazio, Steam Prologue Demo 1.01)", a demo
  address, not ours.
- `tile_cache.rs` wraps the upstream `dtTileCache` (tiles, obstacles by ref, cylinder, box and oriented-box
  obstacles). It has no file reader past the header and no compressor of its own.

What the crate doesn't say: what follows the Atlus header before the first tile, or anything after the last tile.
