# The menus' scene map is one dereference deeper than the first batches walked

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md)
- **Functions:**
  - MenusHandleGetter | 0x51fab0 | .ecode | Menus | known only | Returns `[H+0x48]`, a pointer to the vector of scenes, or 0 when the menus' handle is null.
  - Menus handle | 0x2ee7338 | .debug | Menus | read | The global `H` holding the menus' handle.
  - fclTitle vtable | 0x17f7fb0 | .udata | Menus | known only | The title scene, `0xa8` bytes.

`0x51fab0` returns `[H+0x48]`, a pointer to a vector `{begin, end}`, not the vector itself. The map `M` is
`[[[[base+0x2ee7338]+0x48]]]+0x8`, so the inspector chain is `follow base+0x2ee7338 0x48 0x0 0x0 0x8 …`. The batches
for sessions 1 to 3 had one `0x0` too few; they are corrected.

## Evidence

- **Code.** `0x51fab0` returns 0 when `H` (`0x2ee7338`) is null, else the qword at `H+0x48`. Its callers `0xf9c210`
  (title, key `0x17`) and `0x903140` (tree, key `0x11`) treat the result `p` as a vector: they test
  `(p[1] - p[0]) & ~7`, then pass `[[p[0]]+8]` to `0x4ccde0`, the bucket walk.
- **Session 1.** `[H+0x48]` = `0x40606940` holds `0x5c85d20, 0x5c85d28`: begin and end, one 8-byte element. That
  element, `[0x5c85d20]` = `0x72add670`, is the first element `E`; the old chain read `0x5c85d20+8`, past the end, and
  got a non-pointer. That `[E+8]` is the map is `[NEEDS VERIFICATION]` until the corrected batch runs.
- **The title scene.** `fclTitle@fcl@app` (vtable `0x17f7fb0`) is `0xa8` bytes: its deleting destructor `vf0`
  (`0x51d7d0`) frees `0xa8`. `vf10` (`0x51e0f0`) uses `+0x48`, `+0x80`, `+0x81`, `+0x9a` to `+0x9d` and a state at
  `+0xa0`. A `0x4f8`-byte `TitleChoice` can't be inside it, so the choice is behind a pointer, `+0x48` the first
  candidate `[NEEDS VERIFICATION]`. `vf10` also asks `0x51bf60`, a lookup by type in a map at `[0x2ca3248]+0x1a0`, for
  an object whose type has no RTTI name in the project.
- **The title cursor count.** Every `TitleChoice` cursor record in session 1 says `count=3`, the submenu's too, so the
  count can't tell them apart.

## Next session

At the title: run [`stop-2-title.txt`](../sessions/session-1/stop-2-title.txt) (corrected), then `read <mask
address> 8`, `read <node>+0x10 4` (expect `17 00 00 00`), `rtti <scene>` (expect `fclTitle@fcl@app`), `read <scene>
0xa8`, and `rtti` on each pointer in it, `+0x48` first, then the same one level deeper, for `TitleChoice@ope@ui@app`.
On a hit, `read <choice>+0x230 8` equals the `mgr=` of the title `cursor` records, then `watch <choice>+0x4a8 8` across
the submenu as the [static note](title-names-voice-tutorials-static.md) says.

## Steps

Ghidra 12.2 with JDK 25.0.5+8, the shared project from `pwsh scripts/dev/ghidra.ps1 build`, 2026-11-20:
`decompile 0x51fab0`, `0xf9c210`, `0x4ccde0`, `0x903140`, `0x51e0f0`, `0x51d7d0`, `0x51bf60`, `0x51dee0`;
`vtable app::fcl::fclTitle`; `rtti 0x2bbec70` (none). Live values: the session's `stop-2-offline-snapshot` reads in
the raw source.
