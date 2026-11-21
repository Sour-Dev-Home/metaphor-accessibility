# The travel map panel is handed (compile id, position) message references; the field map strip's names stay in `.text1`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`world-map-destination-panel-static.md`](world-map-destination-panel-static.md),
  [`field-map-region-area-name-static.md`](field-map-region-area-name-static.md),
  [`message-serials-from-file-positions.md`](message-serials-from-file-positions.md),
  [`game-text-hooks-community-2026-11-15.md`](../raw-sources/game-text-hooks-community-2026-11-15.md)

For #545 and #586. The `0x1c`-byte text that `0xcccf50` copies into the travel map's panel is a message reference:
two `u32`s set from a label's lookup. In play they are the id the compile hook saw the file compiled under, and the
message's position in that file. So the mod finds the text in the source it kept for that id (`MessageFilesById`), with
no serials. The field map strip's three serials and its composer are all `.text1` stubs, so the mod can only probe the
composed result for text, in Debug builds; that reading is `[NEEDS VERIFICATION]`.

## Evidence

- **The reference's shape.** `0xa67a40` constructs it: `[+0x0]` = -1 (eight bytes), and bytes `+0xc`, `+0x14`,
  `+0x18` = 0. `0xa67db0 (ref, const u32* a, const u32* b)` stores `a` at `+0x0` and `b` at `+0x4` and clears `+0xc`
  and `+0x14`. Its destructor `0xa67a90` is empty. `title::menu::ExitBtn::vf18` (`0xd3bde0`) shows the whole route:
  `0x9e81e0` resolves a label to the pair, `0xa67db0` stores it, and the panel's consumer (`0xa67aa0`, a stub into
  `.text1`) turns the reference into the pane's text, as `0xcccf50` does. What `+0x8` to `+0x18` hold is not known.
- **The destinations' references come from labels.** `0x4de360` writes the `0xd0`-byte destination entry's `+0xac`
  (name) and `+0x58` (detail), the offsets `0xcd05f0` hands `0xcccf50`. Each time it reads a label string from a data
  table and calls `0x9e81e0`, then `0xa67db0`. A two-character placeholder label is replaced by serial `0x507`, through
  a `.text1` stub.
- **The pair is (compile id, position), seen in play.** `0x9e81e0` looks the label's 16-byte key up in a map and
  returns the value's low and high `u32` as `a` and `b`. In the maintainer's by-ear run of #633 (2026-11-21, Debug
  build, the `TravelMapPanelShow` hook's `map panel` records), the name references were `a` = 4 with `b` = 382, 383
  and 396, and the detail references `a` = 124 with `b` the destination's index (0, 1, 15). In the same run's log,
  `compile id=4` is `EN/message/MapName.msg` and `compile id=124` is `EN/message/system/Map_Grand.msg`. Counting the
  logged sources' messages, positions 382, 383 and 396 of `MapName.msg` are labels `Map_Grand_SPOT_NAME_00`, `_01`
  and `_15`, and positions 0, 1 and 15 of `Map_Grand.msg` are `Map_Grand_SPOT_TEXT_00`, `_01` and `_15`. So `a` is
  the compile id, not the message-file table entry: entry 4 of `0x27b0cd0` is `message/ClassName.msg`, and 124 is past
  the table's end.
- **Compile ids are slots, reused.** The five region files `Map_Martira`, `Map_Altavely`, `Map_Brilhaven`,
  `Map_Grand` and `Map_Virga` compiled as ids 121–125 at 847 842 ms and again as 132–136 at 944 392 ms, `Map_Grand`
  as 124, then 135. So a file compiled under an id replaces what that id held, and a reference is read against the
  latest file under its id.
- **The strip's serials are out of reach.** `0xefe180` gets its first and third serial from `.text1` stubs given the
  id, and composes with a `.text1` stub. `0xc94130` copies the id to the stack before the call, and its other branch
  calls `0xefd050`, not `0xefe180`. The arrival banner's lambda (`0xfc7030`) hands its list to another `.text1` stub
  and returns no text. `0x4c2590` reads `MapNameTable` only when a map's `+0x1e8` is positive; otherwise it uses the
  same `.text1` stubs. Whether a hook on such a stub counts as a `.text1` hook under ADR 0010 is the maintainer's
  call, so the mod probes `0xefe180`'s result for the first string behind it instead, in Debug builds only. That the
  result holds the composed name as a string is `[NEEDS VERIFICATION]`.

## Steps

Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1`), 2026-11-21: `decompile` on `0xcccf50`, `0xa67a90`,
`0xefe180`, `0xefe270`, `0x9e3d30`, `0x9e4140`, `0xcd05f0`, `0xf67210`, `0xa67db0`, `0xd3bde0`, `0x9e81e0`,
`0x9e8dd0`, `0x9f4520`, `0x9e60f0`, `0x9e3b70`, `0x9f3e10`, `0xd0e870`, `0x4de360`, `0x4c2590`, `0x725ab0`; `disasm` on
`0xa67a40`, `0xa67a60`, `0xce8c40`, `0xc94130`; `callers` on `0xefe270`, `0xa67a40`, `0xa67db0`, `0x9e8dd0`,
`0x9e81e0`; `callees 0xcccf50`; `offset 0x45a090`. Table entries: `tools/Metaphor.SignatureCheck --bytes <exe>
0x27b1010 0xd0` (entry 4) and `0x27b7190` (124), then the name each entry's first pointer names.

The run's log, read locally: `grep -a " compile id=" message-log.txt`, then count the `{` body lines after the
`compile id=4` and `compile id=124` records to the wanted positions.

What would settle the rest, with a Debug build (`map` records in `message-log.txt`):

1. Travel map: each move should say the destination's name, then its detail line.
2. Open the field map in a dungeon: `map compose id=… at=+0x… text=…` then `map strip id=… said=…`. `text` should be
   the region and area as the screen shows them.
