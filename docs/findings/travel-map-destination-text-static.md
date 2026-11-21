# The travel map panel is handed (file, position) message references; the field map strip's names stay in `.text1`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`world-map-destination-panel-static.md`](world-map-destination-panel-static.md),
  [`field-map-region-area-name-static.md`](field-map-region-area-name-static.md),
  [`message-serials-from-file-positions.md`](message-serials-from-file-positions.md),
  [`game-text-hooks-community-2026-11-15.md`](../raw-sources/game-text-hooks-community-2026-11-15.md)

For #545 and #586. The `0x1c`-byte text that `0xcccf50` copies into the travel map's panel is a message reference:
two `u32`s set from a label's lookup, which read as the file's entry in the message-file table and the message's
position in it. A serial is that file's first serial plus the position, so `MessageSerials` gives the text once it
knows the place-name files' entries. The field map strip's three serials and its composer are all `.text1` stubs, so
the mod can only probe the composed result for text. Both readings are `[NEEDS VERIFICATION]` in play.

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
- **The pair is (table entry, position), by analogy.** `0x9e81e0` looks the label's 16-byte key up in a map and
  returns the value's low and high `u32` as `a` and `b`. `GetMessageByLabelIndex` (`0x9e3b70`, MF.Toolkit's
  `(int* msgId, int labelIdx, ...)`) and `GetMessageBySerial` both reach `0x9e4140` with a message id converted
  through `0x9f3e10`, then a label index. `0x9f3e10` and `MessageSerialOfFile`'s `0x9f4520` both index by that id.
  `0x9f4520` indexes the message-file table `0x27b0cd0` (`0xd0`-byte entries) with it, so the id is the table entry.
  That `a` is that id and `b` the position is the natural reading, not shown `[NEEDS VERIFICATION]`. The compile hook's
  `id` is another number: `System_Message.msg` is table entry 5 but compiles as id 6.
- **The place-name files' entries.** Each table entry holds the file name at `+0x0` and three `std::function`s at
  `+0x18`, `+0x58` and `+0x98`; `+0xc8` points at the third, which `0x9f4520` calls for the first serial.

  | Entry | File | Range test | First serial | Serials |
  |---|---|---|---|---|
  | 34 | `message/AccessPointName.msg` | `0x9f4c20`: `lea eax,[rcx-0x2a79]; cmp eax,0x197` | `0x9f4720`: `mov eax,0x2a79` | 10873–11280 (408) |
  | 40 | `message/system/Big_Mapname.msg` | `0x9f4bb0`: `lea eax,[rcx-0x2776]; cmp eax,0x16` | `0x9f46b0`: `mov eax,0x2776` | 10102–10124 (23) |
  | 66 | `message/system/WorldMap_Message.msg` | `0x9f4c10`: `lea eax,[rcx-0x2a6c]; cmp eax,0xc` | `0x9f4710`: `mov eax,0x2a6c` | 10860–10872 (13) |

  Whether these files compile whole, and with these counts, isn't seen yet `[NEEDS VERIFICATION]`.
- **The strip's serials are out of reach.** `0xefe180` gets its first and third serial from `.text1` stubs given the
  id, and composes with a `.text1` stub. `0xc94130` copies the id to the stack before the call, and its other branch
  calls `0xefd050`, not `0xefe180`. The arrival banner's lambda (`0xfc7030`) hands its list to another `.text1` stub
  and returns no text. `0x4c2590` reads `MapNameTable` only when a map's `+0x1e8` is positive; otherwise it uses the
  same `.text1` stubs. Whether a hook on such a stub counts as a `.text1` hook under ADR 0010 is the maintainer's call,
  so the mod probes `0xefe180`'s result for the first string behind it instead. That the result holds the composed name as a string is
  `[NEEDS VERIFICATION]`.

## Steps

Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1`), 2026-11-21: `decompile` on `0xcccf50`, `0xa67a90`,
`0xefe180`, `0xefe270`, `0x9e3d30`, `0x9e4140`, `0xcd05f0`, `0xf67210`, `0xa67db0`, `0xd3bde0`, `0x9e81e0`,
`0x9e8dd0`, `0x9f4520`, `0x9e60f0`, `0x9e3b70`, `0x9f3e10`, `0xd0e870`, `0x4de360`, `0x4c2590`, `0x725ab0`; `disasm` on
`0xa67a40`, `0xa67a60`, `0xce8c40`, `0xc94130`; `callers` on `0xefe270`, `0xa67a40`, `0xa67db0`, `0x9e8dd0`,
`0x9e81e0`; `callees 0xcccf50`; `offset 0x45a090`. Table entries and code bytes:
`tools/Metaphor.SignatureCheck --bytes <exe> 0x27b2870 0xd0` (entry 34; 40 at `0x27b2d50`, 66 at `0x27b4270`, 5, 6
and 48 for their names), then 16 bytes at each function above.

What would settle it, with a Debug build (`map` records in `message-log.txt`):

1. Open the travel map and move between destinations. Each move logs `map panel kind=… index=… name=<hex> detail=<hex>
   said=…`. The name's first eight bytes should read as `22000000` (entry 34) and a position under 408.
2. Open the field map in a dungeon: `map compose id=… at=+0x… text=…` then `map strip id=… said=…`. `text` should be
   the region and area as the screen shows them.
