# Recast/Detour: the navmesh and query structures a reader needs (#179)

- **Source:** `recastnavigation/recastnavigation` (Zlib licence) at `9f4ce64458dfae86e1239c525ddc219c4e9e06f1`
  (2026-02-27), read through the GitHub API (`gh api repos/.../contents/<file>?ref=<sha>`):
  `Detour/Include/DetourNavMesh.h` and `Detour/Include/DetourNavMeshQuery.h`. Nothing was downloaded into the
  repository, built or run.
- **Saved:** 2026-11-19.
- **Game build:** none. This is the upstream library; that the game links this version unchanged is
  `[NEEDS VERIFICATION]` (see [`field-navmesh-and-path-queries-static.md`](../findings/field-navmesh-and-path-queries-static.md)).

Feeds #179. Field names are the library's own; offsets are worked out for x64 with the default build options (32-bit
`dtPolyRef`, since `DT_POLYREF64` is commented out at line 28 of `DetourNavMesh.h`; non-virtual `dtQueryFilter`, since
`DT_VIRTUAL_QUERYFILTER` is commented out at line 31 of `DetourNavMeshQuery.h`) and natural alignment.

## `DetourNavMesh.h`

- `DT_NAVMESH_MAGIC = 'D'<<24 | 'N'<<16 | 'A'<<8 | 'V'` (line 70), so `0x444E4156`, stored little-endian as the bytes
  `56 41 4E 44`. `DT_NAVMESH_VERSION = 7` (line 73).
- `dtMeshHeader` (lines 249-277): `int magic` `+0x0`, `int version` `+0x4`, `x`, `y`, `layer` `+0x8`..`+0x10`,
  `userId` `+0x14`, `polyCount` `+0x18`, `vertCount` `+0x1c`, `maxLinkCount` `+0x20`, `detailMeshCount` `+0x24`,
  `detailVertCount` `+0x28`, `detailTriCount` `+0x2c`, `bvNodeCount` `+0x30`, `offMeshConCount` `+0x34`, `offMeshBase`
  `+0x38`, `walkableHeight`, `walkableRadius`, `walkableClimb` (floats) `+0x3c`..`+0x44`, `bmin[3]` `+0x48`, `bmax[3]`
  `+0x54`, `bvQuantFactor` `+0x60`.
- `dtMeshTile` (lines 281-309): `salt` `+0x0`, `linksFreeList` `+0x4`, `header` `+0x8`, `polys` `+0x10`, `verts` `+0x18`
  (`float[3] × vertCount`), `links` `+0x20`, `detailMeshes` `+0x28`, `detailVerts` `+0x30`, `detailTris` `+0x38`,
  `bvTree` `+0x40`, `offMeshCons` `+0x48`, `data` `+0x50`, `dataSize` `+0x58`, `flags` `+0x5c`, `next` `+0x60`; `0x68`
  bytes.
- `dtNavMeshParams` (lines 324-331): `orig[3]`, `tileWidth`, `tileHeight` (floats), `maxTiles`, `maxPolys` (ints);
  `0x1c` bytes.
- `dtNavMesh` members (lines 656-670): `m_params` `+0x0`, `m_orig[3]` `+0x1c`, `m_tileWidth` `+0x28`, `m_tileHeight`
  `+0x2c`, `m_maxTiles` `+0x30`, `m_tileLutSize` `+0x34`, `m_tileLutMask` `+0x38`, `m_posLookup` `+0x40`, `m_nextFree`
  `+0x48`, `m_tiles` `+0x50` (an array of `m_maxTiles` `dtMeshTile`), then `m_saltBits`, `m_tileBits`, `m_polyBits`.

## `DetourNavMeshQuery.h`

- `class dtNavMeshQuery` (line 165) has no virtual functions; its first data member is `const dtNavMesh* m_nav`
  (line 560), so the query's first qword is its navmesh.
- `m_tinyNodePool`, `m_nodePool`, `m_openList` (lines 575-577): scratch state that `findPath` and the sliced path
  functions write, so one query object can't serve two callers at once.
- The functions the game's wrappers match (declarations): `findPath` (line 190), `findStraightPath` (207),
  `findNearestPoly` (324, 338), `raycast` (406, 421), `closestPointOnPoly` (484), `getPolyHeight` (499).
- `dtPolyQuery` (line 155) is a virtual base for polygon queries. The game's exe carries RTTI for it and for
  `dtFindNearestPolyQuery` and `dtCollectPolysQuery`, which are not in this header (they would be in
  `DetourNavMeshQuery.cpp`, not read) `[NEEDS VERIFICATION]`.
