# Player position, facing and map id read through four follows; the map's name sits behind a table index not yet known

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-position-camera-session6-2026-11-17.md`](../raw-sources/field-position-camera-session6-2026-11-17.md),
  [`field-state-map-session6-2026-11-17.md`](../raw-sources/field-state-map-session6-2026-11-17.md),
  [`field-state-and-map-2026-11-16.md`](../raw-sources/field-state-and-map-2026-11-16.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - fldWorld | 0x2ee7510 | .debug | Field | read | The global holding the field world, read for the player's position and facing.
  - fldScene_Root | 0x2ee7348 | .debug | Field | read | The global holding the field's scene root.
  - fldDatabase | 0x2ee7368 | .debug | Field | read | The global holding the field database, with the map id at `+0x48`.
  - MapData.bin | 0x2eebc48 | .debug | Field | read | The map table, rows of `0x2c` bytes; the zone is the `u16` at row `+0x6`.
  - resrcInit loader | 0x8994a0 | .ecode | Field | known only | Loads the init tables by file name into globals.

Static prep (#495) for #181, restating #177's and #180's session-6 results as inspector commands so game session 4
(#499) reads them beside the units, and adding the map-name tables. Position, facing, camera, scene and map id were
confirmed in session 6 by the `capture-kit-2` reader; their inspector form, and everything about the name tables, is
`[NEEDS VERIFICATION]` until the session's reads agree.

| Value | Inspector path | Confidence |
|---|---|---|
| Position | `follow base+0x2ee7510 0x20 0x0 0x80`, then 12 bytes (x, y, z floats) | High (session 6) |
| Facing | the same address `+0x10`: a quaternion (x, y, z, w floats) | High (session 6) |
| Camera eye, look-at | `follow base+0x2ee7510 0x10 0x0 0x60`, then `0x20` bytes (`+0x60` eye, `+0x70` look-at) | High for the values, medium for this chain |
| Scene | `follow base+0x2ee7348 0x10 0x10 0x0`, then `rtti`; `fldScene_Idle` is free roam | High (session 6) |
| Map id | `follow base+0x2ee7368 0x48`, then a `u32` | High (session 6) |
| Map name | `MapNameTable.bin` at `[0x2eebda8]+0x40`, `0x20`-byte entries of two keys; its index is unknown | Low |
| Zone | `MapData.bin` (`0x2eebc48`), rows of `0x2c`, zone `u16` at row `+0x6` | Medium |

## Evidence

### 1. Position, facing, camera, scene, map id

Session 6's reader used these chains: the player `[[fldWorld]+0x20]` and its `+0x80` and `+0x90`; the camera object
`[[world+0x10]+0x0]` and its `+0x60`/`+0x70` (the reader's `WorldCamera = 0x10`, `CameraObject = 0x00`); the scene
`[[[fldScene_Root]+0x10]+0x10]`; the map id `fldDatabase+0x48`. The globals are `fldWorld` `0x2ee7510`,
`fldScene_Root` `0x2ee7348` and `fldDatabase` `0x2ee7368` ([field state raw source](../raw-sources/field-state-and-map-2026-11-16.md)).
The quaternion order (x, y, z, w) and the facing formula (heading of `(2(xz + wy), 1 − 2(x² + y²))`, counter-clockwise
from above, left is +90°) are the reader's `FieldGeometry.YawDegrees` on `capture-kit-2`, which session 6 matched to
the stick directions ([position raw source](../raw-sources/field-position-camera-session6-2026-11-17.md)). The free-roam
frame (`0x576410`, from `fldScene_Idle::vf3`) reads the same `[[world+0x20]]+0x80` for the player
([navmesh note](field-navmesh-and-path-queries-static.md)).

### 2. The map name and zone tables

`resrcInit`'s loader (`0x8994a0`) loads the init tables by file name into globals beside `CharaID` and `NpcID`:
`MapData.bin` into `0x2eebc48`, `AreaName.bin` into `0x2eebc58`, and `MapNameTable.bin` into `0x2eebda8` (by name
string, `0x210c6d0`, `0x210c6f0`, `0x210ca90`). `AreaNameLogo_*.bin` and `BgMapData.bin` load too; not followed.

- **`MapNameTable`:** `0x898fc0` returns `[0x2eebda8]+0x40` (the shared tail `0x1486fc0`, as `NpcID`'s getter). Its
  only analysed caller, `0x725ab0`, takes entry `n` at `+0x10 + n × 0x20`, turns the 16-byte keys at entry `+0x0` and
  `+0x10` into message ids with `0x9e85e0`, and stores them in two message variables, the shape of a "name and sub-name"
  pair. What `n` is (the map id, or an index from `MapData`) and who calls `0x725ab0` (`0x4c2590`, not read) are
  `[NEEDS VERIFICATION]`. `MsgSetVariableMapName` (`0x90cc60`) still jumps into `.text1`.
- **`MapData`:** `FldTravelGetZoneIDByMapID` (`0x9072b0`) and `FldGetCurrentMapZoneID` take a table from `0x898fa0`
  (a jump into `.text1`) and read the `u16` at `+0x10 + id × 0x2c + 0x6`. No analysed code reads `0x2eebc48`, so that
  `0x898fa0` returns `MapData` is a guess from its neighbours `[NEEDS VERIFICATION]`. If it holds, a map row may also
  carry its name index.
- The location banner (`UUIC_Field_LocationName`, `fldLocationName`, `FldLocationNameUIOpen` `0x8e0060`) jumps into
  `.text1`. With the `ui-text-panes` opt-in, the banner's text may show as a drawn text pane on an area change, which
  would give the name without any table `[NEEDS VERIFICATION]`.

## Tonight

Stops 1 and 2 of #499 (the `[where]` section of both batches). Run with
`pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-4/<file> -WaitKey -SaveLog <out>`, `<out>` a folder
outside the repository, then type the reads the batch's comments list. Adding `ui-text-panes` to the run's `-OptIn`
tests the banner idea at no cost.

| Stop | What confirms | What rejects |
|---|---|---|
| 1, town, standing still then walking | `read <pos> 0x20`: x and z change while walking and hold while still; y steps on stairs; the facing quaternion's heading turns with the stick. `rtti <scene>` says `fldScene_Idle`. The map id is a small number that stays put in town | `follow` fails in free roam; the position never changes |
| 1 and 2, after each area change | The map id changes (session 6 saw 2, 12, 285, 371, 372). `read <T> + id × 0x20` for `MapNameTable` (`T` from `follow base+0x2eebda8 0x40 0x10`) gives two non-zero 16-byte keys for this map, different from the last map's; `read <MapData> + id × 0x2c` has a small `u16` at `+0x6` | Keys all zero or equal across maps: the index isn't the map id |
| 2, dungeon | `fldScene_Idle`, a new map id; the banner text, if it appears as a text pane, matches the screen | |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says
otherwise.

1. A byte search of the exe for `COMMON/init/data/*.bin` names; their RVAs from the section table. `global 0x210c6b0`,
   `global 0x210c6d0`, `global 0x210c6f0`, `global 0x210ca90`; `disasm 0x8994a0` (the stores after each load);
   `global 0x2eebc48`, `global 0x2eebc58`, `global 0x2eebda8`.
2. `disasm 0x898fc0`, `disasm 0x1486fc0`, `decompile 0x725ab0`, `callers 0x725ab0`, `callers 0x898fc0`;
   `disasm 0x898fa0`, `disasm 0x9072b0`; `vtable 0x1801438`, `disasm 0x8e0060`.
3. `FieldWatch.cs` and `FieldGeometry.cs` on `capture-kit-2` (`git grep`), for the reader's offsets and formula.

Nothing in `.text1` was decompiled. No decompiled output is quoted; names and file paths are the exe's own.
