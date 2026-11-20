# The field keeps a Detour navmesh behind a static pointer chain, and the game queries it every free-roam frame

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`recast-detour-navmesh-layout-2026-11-19.md`](../raw-sources/recast-detour-navmesh-layout-2026-11-19.md),
  [`field-actors-2026-11-16.md`](../raw-sources/field-actors-2026-11-16.md),
  [`field-navigation-accessibility-mods-2026-11-16.md`](../raw-sources/field-navigation-accessibility-mods-2026-11-16.md),
  [`chapter1-interaction-inventory-2026-11-20.md`](../raw-sources/chapter1-interaction-inventory-2026-11-20.md)

Static prep (#495) for #179, feeding guidance (#183) and auto-walk (#184) in game session 4 (#499). **Verdict: usable,
high confidence that the data exists, medium that we can use it as planned.** The exe links Recast/Detour, each map
has its own navmesh file, and readable `.ecode` wrappers run `findNearestPoly`, `findPath`, `findStraightPath` and a
navmesh `raycast` on it for enemies and party followers. Every free-roam frame, `fldScene_Idle` snaps the player's
position to the navmesh. The navmesh is reachable from `fldWorld` without calling anything. Not seen in a running game
yet: every runtime value below is `[NEEDS VERIFICATION]` until session 4 reads it. The pilot inventory names the dark
mine and the open desert as the hardest places for a blind player; this is the data that would guide there.

| Question | Answer | Confidence |
|---|---|---|
| Is there a navmesh? | Yes: Detour, one file per map (`COMMON/model/field/navi/f_%04d_%03d.NAV`) | High (strings, RTTI, code) |
| Where is it at run time? | nav object `N = [fldBg+0x48]`, `fldBg = [[[base+0x2ee7510]+0x38]+0x8]`; `N+0x130` `dtNavMesh*`, `N+0x138` `dtNavMeshQuery*` | High in code |
| Path query? | `0x13f19b0`: nearest poly ×2, `findPath`, `findStraightPath`, into a caller's path buffer | High for the shape, medium for each Detour name |
| Walk-line test (raycast)? | `0x13eeac0`: nearest poly ×2, `raycast`, hit point, `getPolyHeight` | Medium-high |
| Nearest walkable point? | `0x13f2930`, called each idle frame on the player | High |
| Safe to call from a hook? | Only on the game thread (shared node pool); a new kind of hook, not tonight | Medium |
| Safe to read? | Yes: the tiles are plain memory, readable through `ModuleMemoryReader` | High in code, untested |
| Wall collision? | PhysX (the `PhysX_64.dll` the game ships, `fldRayCast`, `PxRaycastHit`/`PxSweepHit`) and `.COL` files; not followed | Low (not traced) |

## Evidence

### 1. Recast/Detour is linked, and each map has a navmesh file

- RTTI (the exe's `.?AV` names, found by a byte search of the exe): `dtFindNearestPolyQuery`, `dtCollectPolysQuery`,
  `dtPolyQuery`, `dtTileCacheMeshProcess`, `dtTileCacheAlloc`, `dtTileCacheCompressor`; `NavigationManager@ai@fw`,
  `MeshProcess@ai@fw`, `UnitCpntNavi@unit@app`, `UnitCpntCollision@unit@app`, `fldRayCast@fld@app`,
  `resrcBgData_collis` (and `_D`, `_BTL`, `_GAYA`). Debug tools `NaviMain@takeuchi`, `RaytraceMain@takeuchi` and
  `NaviTest@ksk@app` are named too.
- Strings: `COMMON/model/field/navi/f_%04d_%03d.NAV` (rva `0x225c040`, its only pointer at `0x27b7e78` in `.debug`,
  with no reference in analysed code, so the loader is in `.text1`), `COMMON/model/field/collision/o_%04d_%03d_%02d.COL`
  (rva `0x18290a8`, loaded at `0x827155`, `0x8271b5`, `0x894ef2`), the source path
  `...program/framework/ai/navigation/NavigationManager.cpp`, and Recast's own build messages
  (`buildTiledNavigation: Could not init Detour navmesh query` at `0x2361de0`, used by `0x13ee010`).
- The map files share the `f_%04d_%03d` pattern of the field layout files, so a map's navmesh is named like its map
  `[NEEDS VERIFICATION]`.

### 2. The Detour functions, by their wrappers

- `0x1543a30` is `findNearestPoly`: it is the one function that builds a `dtFindNearestPolyQuery` (vtable `0x236ff28`)
  on its stack. `0x15489e0` builds the `dtCollectPolysQuery` (`0x236ff40`).
- `0x13f19b0` (called by `fldCharaCtrl_Enemy::vf7`, `0x6566f0`, and five other field functions): calls `0x1543a30`
  on a start and an end point, checks bit 30 of both results (Detour's `DT_SUCCESS`), then `0x1543b10` with the two
  poly refs and a 256-entry poly array, then `0x1541b30` when the last poly isn't the end poly, then `0x1545d40`
  writing into the caller's buffer (count at buffer `+0x1500`, limit at `+0x1504`). That is Detour's documented
  find-path sequence, so `0x1543b10` reads as `findPath`, `0x1541b30` as `closestPointOnPoly` and `0x1545d40` as
  `findStraightPath` `[NEEDS VERIFICATION]` per name. Poly refs are 32-bit `int`s, so the game uses the default
  `dtPolyRef`.
- `0x13eeac0` (called by `0x5d6050`, `0x5d6150`, `0xfe8950` beside `UnitCpntNavi::vf0`, `0x64f240` and `0x651ad0`): two
  `findNearestPoly`, then `0x1549b30` with a hit fraction `t`; when `t <= 1` it returns the point `start + t × (end −
  start)` and calls `0x1546cf0` on the last poly. That is `raycast` then `getPolyHeight` `[NEEDS VERIFICATION]` per
  name: "can I walk straight from A to B, and where would I stop".
- `0x13f2930`: one `findNearestPoly` with half-extents at `[N+0x140]+0x374`, returning the nearest navmesh point and
  a success flag.

The wrapper's first argument is the nav object `N`: each wrapper reads the navmesh at `N+0x130` and the query at
`N+0x138`, and `0x13f19b0` copies its query filter from `N+0x148`.

### 3. The static path to the nav object

`0x576410`, called from `fldScene_Idle::vf3` (`0x5f0891`), the free-roam update: it loads `fldWorld`
(`[0x2ee7510]`), takes `[world+0x38]` (Resonant's `fldBgManager`, `field-actors-2026-11-16.md`), calls `0x576840`
(`mov rax,[rcx+8]; ret`), and calls the result's vtable slot 7. `fldBg`, `fldBg_Fix` and `fldBg_Move` (vtables
`0x17fd370`, `0x17fd478`, `0x17fd5a0`) all have slot 7 = `fldBg::vf7` (`0x577f70`), `mov rax,[rcx+48h]; ret`. If the
result is non-null it reads the player's position at `[[world+0x20]]+0x80` and calls `0x13f2930` with it, then works
out the distance from the player to the snapped point. `0x5d5fd0` (used by `fldMember::vf7`, the followers) and
`fldCharaCtrl_Enemy::vf7` take the same chain. So:

- `fldBg = [[[base+0x2ee7510]+0x38]+0x8]`, nav `N = [fldBg+0x48]` (null means no navmesh on this map);
- `dtNavMesh` `= [N+0x130]`, `dtNavMeshQuery` `= [N+0x138]`;
- by the upstream layout ([raw source](../raw-sources/recast-detour-navmesh-layout-2026-11-19.md)), the query's first
  qword equals the navmesh pointer, the navmesh's tile array is `[mesh+0x50]` (`0x68` bytes per tile), and each used
  tile's header (`[tile+0x8]`) starts with `56 41 4E 44` (`'DNAV'`) and version 7. That the game's Detour is unmodified
  is `[NEEDS VERIFICATION]`; the tile magic settles it.

### 4. What it would take to use it

- **Read only (fits the current rules).** On each map change, copy the tiles' polygons, vertices and links through
  `ModuleMemoryReader` and run our own A* and string-pulling in plain C#. Nothing is called, so no new kind of hook;
  only the polygon and link layouts (in `DetourNavMesh.h`, not yet saved) need adding to the raw source. The navmesh
  "knows the walls and nothing of who stands on the floor" (Arcana, in the navigation raw source); people, closed doors
  and gimmicks need the unit lists on top.
- **Calling the game's query (new hook kind).** `findPath` and `findStraightPath` write the query's node pool and open
  list, so a call from our thread could corrupt an enemy's path search. A call would have to run on the game thread,
  for example from a hook on `0x576410`, after the game's own use in that frame. It changes no game state, so it isn't
  a behaviour-changing hook under ADR 0007, but no rule covers calling a game function from a hook yet; it needs its own
  design and security review. Not tonight.
- **Auto-walk** still needs input injection (navigation raw source, section 5), which swallows or adds input and so
  is behaviour-changing under ADR 0007.
- Whether towns have a navmesh as dungeons do, and whether it covers the player's whole walkable area or only where
  enemies roam, are the open questions tonight's reads answer.

## Tonight

Stops 1 and 2 of #499. Run each batch with
`pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-4/<file> -WaitKey -SaveLog <out>`, `<out>` a folder
outside the repository. The inspector can't chain a `follow` into a `read`, so each batch prints the addresses and the
stop lists the reads to type next as `pwsh scripts/dev/session.ps1 inspect 'read <addr> <count>'`.

| Stop | Batch | What confirms | What rejects |
|---|---|---|---|
| 1, town | `stop-1-town.txt`, section `[town-nav]` | `follow ... 0x48 0x0` gives a non-zero `N`; `rtti <fldBg>` says `fldBg_Fix` or `fldBg_Move`; `read <query> 8` equals the navmesh pointer; `read <header> 0x64` starts `56 41 4E 44 07 00 00 00` | `err unreadable` at the `0x48` step, or a zero `N`, in town (no town navmesh: guidance there needs another source); any other magic (a modified Detour) |
| 2, dungeon first room | `stop-2-dungeon.txt`, `[dungeon-nav]` | The same, and `N` differs from stop 1's. `read <mesh> 0x58`: `orig` x and z (floats `+0x0`, `+0x8`) within a few tile widths of the player's `x`, `z`; `tileWidth` = `tileHeight` > 0; `maxTiles` and `maxPolys` small powers of two or round numbers. The header's `bmin`/`bmax` (`+0x48`, `+0x54`) bracket the player when the map has one tile; `walkableRadius` (`+0x40`) is tens of units, a body's radius in the position unit | `orig` thousands of tiles away from the player, or `tileWidth` zero or huge: the layout differs from upstream |
| 2, after walking into a wall | `[after-wall]` | The player's position stays inside the header's bounds; the navmesh pointers don't change while walking | `N` changes or goes null in the same room |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says
otherwise.

1. Names: a byte search of the exe for `.?AV`/`.?AU` type names and for printable strings matching `navi`, `navmesh`,
   `detour`, `raycast`, `collis` (grep over the exe on disk, nothing written); file offsets turned into RVAs from the
   section table. `global 0x225c040`, `global 0x27b7e78` (none found), `global 0x18290a8`, `global 0x2361de0`;
   `callers 0x13ee010`.
2. `vtable fw::ai::NavigationManager`, `vtable app::fld::fldRayCast`, `vtable app::unit::UnitCpntNavi` (their
   destructors jump into `.text1`; not followed); `vtable dtFindNearestPolyQuery`, `vtable dtCollectPolysQuery`;
   `global 0x236ff28`, `global 0x236ff40`; `callers 0x1543a30`.
3. `decompile 0x13f2930`, `decompile 0x13f19b0` (and `callees`), `decompile 0x13eeac0` (and `callees`); `callers` of
   `0x13f2930`, `0x13eeac0`, `0x13f19b0`, `0x13f22a0`, `0x13f2a80`, `0x13f6550`, `0x13f0f70`.
4. `disasm 0x6566f0`, `disasm 0x576840`, `decompile 0x576410`, `callers 0x576410`, `vtable app::fld::fldBg`,
   `vtable app::fld::fldBg_Fix`, `vtable app::fld::fldBg_Move`, `disasm 0x577f70`, `decompile 0x5d5fd0`,
   `decompile 0x5d6050`, `callers 0x5d5fd0`.

Nothing in `.text1` was decompiled: `NavigationManager`'s, `fldRayCast`'s and the `.NAV` loader's code was noted as a
jump and not followed. No decompiled output is quoted; names and file paths are the exe's own.
