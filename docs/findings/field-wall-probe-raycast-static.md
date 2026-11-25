# A wall probe needs no new hook on maps with a navmesh; elsewhere the field's own raycast `0x13e2530` is callable

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-navmesh-and-path-queries-static.md`](field-navmesh-and-path-queries-static.md),
  [`field-navmesh-file-from-map-data.md`](field-navmesh-file-from-map-data.md),
  [`field-player-frame-hook-static.md`](field-player-frame-hook-static.md)
- **Functions:**
  - FieldRaycastMask30 | 0x13e2530 | .ecode | Field | known only | Casts a ray from a start to an end point with filter word 0x30 and returns hit or miss, the hit point and normal. | finding
  - FieldRaycast | 0x13e21e0 | .ecode | Field | known only | The raycast every masked wrapper calls: locks the scene, casts into a raycast hit buffer, unlocks. | finding
  - FieldSweep | 0x13e2b80 | .ecode | Field | known only | The sweep counterpart, into a sweep hit buffer; used by the follower and character control code. | finding
  - FieldPhysicsWorld | 0x2ee72d8 | .debug | Field | known only | Global pointer to the physics singleton every field raycast passes as its first argument. | finding

Issue #829, for wall tones (#830). **Two Release-safe probes, in order.** (1) On a map with a `.NAV` file, the mod's
own `WalkGraph.DistanceToEdge` (`src/Metaphor.Core/Navigation/WalkGraph.cs`) already answers "how far can I walk
in this direction" from the navmesh it loads for the beacon. It needs no hook and no call, and its unit tests pass.
(2) For real geometry, and for maps with no navmesh, the field's own raycast wrapper `0x13e2530` is a whole `.ecode` function, not a stub, and its first 27 bytes match once.
Calling it needs a new kind of hook: a per-frame hook that calls a game function, as
`SquirrelRegistrationListener` calls `sq_*`. High confidence in the code shapes; nothing here has been run in game.

| Question | Answer | Confidence |
|---|---|---|
| Navmesh edge distance without the game? | `WalkGraph.DistanceToEdge(position, heading, max)`: a grid walk to the first unlinked cell side | High (unit tests); by ear untested |
| Where does it fail? | Maps whose `MapData` row names no `.NAV` (251 of 605 rows); doors, people and gimmicks; edges sit about one agent radius inside the wall | High for the count; the rest by design |
| Game raycast? | `0x13e2530(world, start*, end*, hitPos*, hitNormal*)`, `world = [base+0x2ee72d8]`, returns a byte, 1 on a hit | High for the shape |
| Who uses the same mask (`0x30`)? | `fldUnit::vf38`, the jump and dash-jump actions, `fldCharaCtrl_Enemy::vf4`, `fldActionHit::vf7` (through `0x5d4c30`) | High |
| Release-safe? | `.ecode`, full prologue, unique 27-byte pattern with no wildcards; the global from a `mov rbx,[rip+disp32]` at `0x13e21e0+0x68`, that pattern unique too | High on disk |
| Thread? | Game thread only: the wrapper takes and drops a lock on `[world+0x58]` around the cast, but nothing says the scene is never simulating then | Medium |

## Evidence

### 1. The navmesh edge walk already exists

`WalkGraph.DistanceToEdge` (added with the navmesh reader, #179) steps cell by cell (Amanatides and Woo) from the
position along a flat heading and stops at the first cell side with no walk link. It returns null off the mesh and
caps at `MaxEdgeDistance`. `WalkGraphTests` covers open ground, a blocked cell, a diagonal, the cap and off-mesh. The
field feature already loads the map's graph through `NavMeshSource` for the beacon's routes (#690), and the map's
file comes from its `MapData.bin` row (#741, [note](field-navmesh-file-from-map-data.md)). Its own comment flags that
cells are shrunk by the agent's radius `[NEEDS VERIFICATION]`. Navmesh edges also mark ledges and drops, not only walls.
For a blind player both mean "can't walk on". That the built mesh matches the game's runtime mesh is the open
question in the [navmesh note](field-navmesh-and-path-queries-static.md) `[NEEDS VERIFICATION]`.

### 2. The field raycasts go through one wrapper and one global

- **The hit buffers.** The exe has RTTI for `PxHitBuffer<PxRaycastHit>` (vtable `0x2120cd0`) and
  `PxHitBuffer<PxSweepHit>` (`0x23619b8`). The game ships PhysX 4.1.0.0 (`PhysX_64.dll`'s file version). The raycast
  buffer's vtable is stored in three functions: `0x9bfd20` (a `takeuchi::Enemy` debug test), `0x13ac940` (its own
  object's scene at `+0x220`, not followed) and `0x13e21e0`.
- **The wrapper `0x13e21e0`.** It reads `[0x2ee72d8]+0x58`, calls its vtable `+0x318` before the cast and `+0x320`
  after, normalises `end − start`, scales the start by a constant, then calls `[[world+0x58]]+0x2b0` with the hit
  buffer on its stack. On a hit it copies the hit's position (scaled back), normal, actor and shape out. That `+0x58` is
  the `PxScene`, `+0x318`/`+0x320` are `lockRead`/`unlockRead` and `+0x2b0` is `raycast` are `[NEEDS VERIFICATION]`:
  the evidence is the hit buffer passed through, and the sweep wrapper `0x13e2b80` calling slot `+0x2b8` with a sweep
  buffer.
- **The masked entry points.** Ten small functions `0x13e2530`–`0x13e28c0` set a filter word and flags 3, then call
  `0x13e21e0`. The words: `0x30` (`0x13e2530`, and `0x13e2750`), `0x31`, `0x200`, `0x1200`, `0x800`, `0x2000`, `0x4000`
  and `0x1`. What each bit selects is `[NEEDS VERIFICATION]`.
- **Who calls `0x13e2530`.** `0x5d4c30` (and `0x485fd0`, `0xa04b90`, `0xfe1f60` and others) load
  `[0x2ee72d8]` into `rcx` and call it. `0x5d4c30` is called by `fldUnit::vf38`, `fldCharaAct_DashJump::vf1`,
  `fldCharaAct_JumpUp::vf1`, `fldCharaCtrl_Enemy::vf4`, `fldActionHit::vf7` and others, so mask `0x30` is what the
  field's units cast against: the level's collision `[NEEDS VERIFICATION]` that walls are in it. The camera's
  `fldTypeCamera_Follow::vf11` uses `0x5d4d10`, mask `0x1200`.
- **`fldRayCast`.** Its vtable `0x18017e8` has one slot, a `jmp` into `.text1`, so the class itself is off limits.
  The `0x5d4c30` family sits right after it, but each one's prologue matches 4 times (they differ only in the call),
  so `0x13e2530` is the target, not them.
- **The global.** `0x2ee72d8` is a `SingletonBase` instance. `0x9bcbe0` creates it (malloc `0xe0`, a constructor in
  `.text1`); 121 reads in `.ecode`, from the field, the camera, `fw::Destruction` and `fw::ColDynamic`. That it is
  set in normal play is `[NEEDS VERIFICATION]`: the one writer found is under `0x9c2ad0`, and `.text1` isn't analysed.

### 3. Signatures (on disk, `.ecode` only)

| Target | Pattern | Matches |
|---|---|---|
| `0x13e2530` | `4C 8B DC 48 83 EC 68 45 33 D2 49 C7 43 E0 30 00 00 00 4D 89 53 D0 B8 03 00 00 00` | 1 |
| `0x13e21e0`, load of `0x2ee72d8` at `+0x68` | its first `0x68` bytes, then `48 8B 1D ?? ?? ?? ??` | 1 (its first 38 bytes alone: 3) |
| `0x5d4c30` | first 33 bytes, rip operand wildcarded | 4 |

## Build plan

1. **#830 on the navmesh (no new hook).** Four `DistanceToEdge` probes per tick, from the player's position (`P+0x80`)
   along the camera's flat forward, back, left and right. Run them on the beacon's loaded `WalkGraph`; null or no graph
   means silence. Add the agent radius before mapping distance to volume, once a session checks it. Tested in
   `Metaphor.Features.Field` with fake graphs.
2. **A game-raycast spike, only if (1) fails by ear** (towns without a `.NAV`, open edges that aren't walls). Add
   `FieldRaycastMask30` and the `0x2ee72d8` load as signatures, and a hook on the free-roam frame (`fldScene_Idle::vf3`,
   [frame note](field-player-frame-hook-static.md)) that lists the raycast in `Calls`. In `Observe`, it casts four
   rays from `stackalloc`ed 16-byte vectors, chest height above `P+0x80`. That needs: the hook type added to
   architecture rule 10's list of types that call through function pointers, a `security-reviewer` stamp, and a
   session that checks that the global is non-null in free roam, that a ray into a known wall returns 1 with a hit
   point at a sensible distance, and that the position unit matches `P+0x80`.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-23. Six `pwsh scripts/dev/ghidra.ps1 batch <file>` runs, 64 queries:

1. A byte search of the exe on disk for printable names matching `RayCast`, `Sweep`, `Raytrace`, `Collis`
   (nothing written); `PhysX_64.dll`'s version resource.
2. `vtable` of `app::fld::fldRayCast`, `app::unit::UnitCpntCollision`, `physx::PxHitBuffer<physx::PxRaycastHit>`,
   `physx::PxHitBuffer<physx::PxSweepHit>`; `disasm 0x5d4c00`.
3. `global` of `0x2120cd0`, `0x23619b8`, `0x18017e8`, `0x2ee72d8`; `decompile` of `0x13e21e0`, `0x13e2b80`, `0x9bfd20`,
   `0x13ac940`, `0x13e2530`, `0x5d4c30`, `0x5d5320`, `0x9bcbe0`, `0x669740`.
4. `callers` of `0x13e21e0`, `0x13e2b80`, `0x9bfd20`, `0x13ac940`, each of the ten masked wrappers, `0x5d4c30`,
   `0x5d4d10`, `0x9bcbe0`, `0x665ea0`, `0x668080`; `disasm` of the ten wrappers for their filter words.
5. Section table and signature counts: a throwaway PowerShell snippet read the exe's section headers and `.ecode`'s raw
   bytes, and counted matches of each pattern above (wildcards as `??`).

Nothing in `.text1` was decompiled. No decompiled output is quoted; the names are the exe's RTTI and our addresses.
