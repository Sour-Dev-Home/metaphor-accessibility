# The grid cursor's fields: static reading of `MultiLineCursorManager` (#563, #589)

- **Source:** our own reading of the game executable on disk, as shipped (ADR 0002): the local Ghidra project
  (`scripts/dev/ghidra.ps1`, Ghidra 12.2, `vtable 0x22046a8`, then `decompile` on `0xe9c7d0`, `0xe9c250` and
  `0xe9c490`), this repository at the commit that adds this file. Nothing was patched, unpacked or run.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Adds to [`ui-focus-candidates-2026-11-16.md`](ui-focus-candidates-2026-11-16.md), whose table gives this class's slot 1
and slot 4, and to [`settings-pages-resolves-by-ear-2026-11-20.md`](settings-pages-resolves-by-ear-2026-11-20.md).
Addresses are RVAs; field offsets are from the manager. Names in quotes are ours.

## The vtable

`MultiLineCursorManager` (`0x22046a8`) has 13 slots. Slot 4 is the shared `CursorManagerInput` (`0xe98770`). Slots 1
and 5–10 are its own (`0xe9c7d0`, `0xe9c250`, `0xe9c560`, `0xe9c490`, `0xe9c710`, `0xe9c800`, `0xe9c820`), all in
`.ecode`, so unlike the shared cursor's slots 5–8 they decompile.

## Slot 1, the index getter (`0xe9c7d0`)

It returns `(+0xf0 + +0x64) * +0xe0 + +0xf4 * +0x54 + +0xec`, all 32-bit. It doesn't read `+0x60`. The shared cursor's
getter (`0xe99660`) returns `+0x60 + +0x64`, which is what the mod's cursor hook logs as `index`.

## Slot 5, command `0x14` (`0xe9c250`)

It adds 1 to `+0xf0`. When that reaches `+0xe8` it adds 1 to `+0x64` instead, while `+0x64 * +0xe0 + +0x54` is below
`+0x58`. A cell index (row times `+0xe0`, plus `+0xf4 * +0x54`, plus `+0xec`) at or past `+0x58` sets `+0xec` to 0 or
puts the move back; with bit 1 of `+0xac` set it wraps to the top. It ends by storing
`+0xe0 * +0xf0 + +0xf4 * +0x54 + +0xec` at `+0x60` and returns `0x14` for a move. A byte at `+0xf8` selects a
branch that wraps `+0xf0` modulo `+0xe4`.

## Slot 7, command `0x16` (`0xe9c490`)

It returns at once when `+0xe0` is below 2. Otherwise it adds 1 to `+0xec`, bounded by `+0xe0` (or by `+0x58` modulo
`+0xe0` on a last, short row), wrapping to 0 when bit 1 of `+0xac` is set. It stores `+0x60` as slot 5 does and returns
`0x16` for a move.

## Reading

- `+0xe0` is the cells to a row ("columns"), `+0xec` the column, `+0xf0` the row within the view ("line"), `+0x64` the
  rows scrolled ("scroll"), `+0xe8` the rows in view, `+0x58` the cell count, and `+0xf4` times `+0x54` a page's offset.
  Command `0x14` moves down a row and `0x16` right a column.
- `+0x60` is the cell within the view, so the logged `top` is that and the logged `row` is the scroll; their sum is the
  index only while nothing has scrolled.
- The 2026-11-20 by-ear run's step of 2 per press of `0x0040` (`cmd=0x14`) fits `+0xe0` being 2 on Key Configuration.
  That the run-time value is 2 there, and that the two columns are the two binding columns, is `[NEEDS VERIFICATION]`.
- `count=23` on Key Configuration is odd for two cells to a row; slot 7's short-row bound allows it, but what the 23
  cells are is `[NEEDS VERIFICATION]`.
