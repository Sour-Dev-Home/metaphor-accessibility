# The travel map's pin is a free pointer over map points, and a route's days live in the travel map component

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`world-map-destination-panel-static.md`](world-map-destination-panel-static.md),
  [`travel-map-destination-text-static.md`](travel-map-destination-text-static.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - SortieMapPointerStep | 0xcd4670 | .ecode | Menus | known only | Each frame: hit-tests the pin against the map's points and, on a change, shows the new point (`0xcd05f0`) and plays UI sound 8.
  - SortieTownMapState | 0x4dfc80 | .ecode | Menus | known only | Returns the object the UI registry (global `0x2ca3248`) holds for `UUIC_Common_SortieTownMap`, or 0.
  - SortieMapUI_GetPathNeedDay | 0x911480 | .ecode | Menus | known only | Script function: that object's `+0x34`, clamped to 1 to 3.
  - SortieMapUI_GetRouteNum | 0x9117a0 | .ecode | Menus | known only | Script function: that object's byte `+0x3c`, or 0 when there is none.

For #545. The destination panel is already spoken (#633, the `TravelMapPanelShow` hook on `0xcccf50`), and that is
the selection itself: the pin is not a list. `0xcd4670` moves a pointer, finds the map point under it in the point
table at `owner+0x45a090`, and on a change calls `0xcd05f0`, the call that shows the panel, so every destination the
pin reaches already passes the hook; there are no rows and no "n of total". What the panel hook doesn't carry is the
route: the days a trip takes are `+0x34` of the travel map's state object (`SortieMapUI_GetPathNeedDay` clamps it to
1 to 3) and the route's length `+0x3c`, both behind a registry lookup; who writes them wasn't found. The per-region
data files hold the paths, so the days may also be readable offline.

## Evidence

- **The pointer.** `0xcd4670` (the caller of `0xcd05f0` named in the
  [panel note](world-map-destination-panel-static.md#showing-a-destination-fills-those-holders)) updates two timers
  at `+0x45fff8` and `+0x45fffc`, hit-tests through `0xcc8f30` with the pointer state at `+0x45ff10` to `+0x45ff20`,
  looks the hit up in the point table (`0xcc9d00` on `owner+0x45a090`), and compares it with the shown pair at
  `+0x45fefc`/`+0x45ff00`. On a change it calls `0xcd05f0(owner, kind, index)`, stores the new pair, and calls
  `UiSoundPlay` (`0x92e3e0`) with 8. A by-ear run saw no `cursor` record on the map (#545, 2026-11-21), which fits.
- **The state object.** `0x4dfc80` hashes the type descriptor at `0x2bbd890` and walks the bucket list of the
  registry at global `0x2ca3248` (`+0x1a0` buckets, `+0x1b8` mask), returning the matched node's `+0x18`. The
  descriptor's name, read with `tools/Metaphor.SignatureCheck --bytes` at `0x2bbd8a0`, is
  `.?AVUUIC_Common_SortieTownMap@ui@app@@` (vtable `0x2250628`). `0x4dff40` is the same lookup returning whether the
  object exists. Eight functions beside `fclSortieTownMap` (`0x4e07a0` to `0x4ea870`), its `vf2` (`0x4e91b0`) and
  every `SortieMapUI_*` getter call it.
- **Days and route.** `0x911480` returns the object's `+0x34`, raised to the `int` at `0x2114224` and lowered to the
  one at `0x2114228`; those read 1 and 3 (`--bytes 0x2114220 16`). `0x9117a0` returns byte `+0x3c` when `0x4dff40` is
  true. `SortieMapUI_StartRouteSelect` (`0x912050`) and `SortieMapUI_IsEndRouteSelect` (`0x911ac0`) cast the open
  scene to `fclSortieTownMap` and call `0x4e8ef0` and `0x4e6a80`. No instruction with displacement `0x34` is in the
  eleven callers, `fclSortieTownMap::vf2` (`0x4e91b0`) or the component's update (`vf13`, `0xf36dc0`), so the writer
  is elsewhere (the descriptor's other users, `0x4d8f90`, `0x4f6ca0` and `0xf36f40`, weren't read) or behind
  `.text1` `[NEEDS VERIFICATION]`. That the screen shows this number as the trip's days is
  inferred from the name `[NEEDS VERIFICATION]`.
- **The data files.** `app::dat::SortieTownMap::vf0` (`0x3d7370`) formats `%spoint.bin`, `%spath.bin`, `%scoll.bin`
  and `%sabsorpcoll.bin`, and reads `COMMON/ui/worldmap/PathUniquePosDef.bin`. The exe also holds the prefixes
  `COMMON/ui/worldmap/TownmapPathData_<region>_` for Altavely, Brilhaven, Grand, Intercity, Martyra and Virga; that
  they fill the `%s` is `[NEEDS VERIFICATION]`, and the files' formats weren't read.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23.

1. `decompile 0xcd4670`, `0x911480`, `0x9117a0`, `0x912050`, `0x911ac0`, `0x4dfc80`, `0x4dff40`, `0x3d7370`.
2. `global 0x2bbd890` (the lookups), `callers 0x4dfc80`, `vtable 0x2250628`; `offset 0x34` in each `0x4e` caller,
   `0x4e91b0` and `0xf36dc0`, and `offset 0x3c 0x4e91b0` (all empty).
3. `tools/Metaphor.SignatureCheck --bytes <exe> 0x2bbd8a0 64` and `0x2114220 16`; `grep -aoE
   "COMMON/ui/(worldmap|sortie|townmap)[...]"` over the exe; `global 0x17e7c48` (the `PathUniquePosDef.bin` string).

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.

## Live-session checklist

Debug build, `travelMap` on (#633), noting log times; NVDA on.

1. Open the Gauntlet Runner's travel map. Move the pin over three destinations: each should say its name and detail
   (the `map panel` records). Say whether the screen shows a trip length ("n days") and where.
2. Start a route (the route-select step, if the screen has one) and say what the screen asks: a destination list,
   waypoints, or a confirm. Note whether the panel records keep coming while choosing.
3. Confirm and cancel once each: the question should be a `select` record (#545's earlier checklist, step 4).
4. If step 1 shows days, the next spike is either a Debug read of the state object's `+0x34` (registry lookup above)
   at each panel record, or the region's `path.bin` from `base.cpk`, read offline as the skill-cost note reads tables.
