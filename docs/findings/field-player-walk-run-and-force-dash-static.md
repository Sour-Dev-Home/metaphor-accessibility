# The field player has scripted walk and run moves, and a force-dash bit in its action flags

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-state-and-map-2026-11-16.md`](../raw-sources/field-state-and-map-2026-11-16.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - FldPlayerSetIsForceDash | 0x1408e7fd0 | .ecode | Field | known only | Script function: sets or clears bit 0x800 of the player's action flags at +0x34c8.
  - FldPlayerSetAndWalkToPos | 0x1408e5f40 | .ecode | Field | known only | Script function: a scripted walk of the player to a point, through 0x8d9bb0.
  - FldPlayerSetAndRunToPos | 0x1408e5eb0 | .ecode | Field | known only | Script function: a scripted run of the player to a point, through 0x8d9a00.

For #957. The game has separate walk and run moves for the player, at least for scripted moves: the script list names
`FldPlayerSetAndWalkToPos` and `FldPlayerSetAndRunToPos`, which call two different movers (`0x8d9bb0`, `0x8d9a00`) on
the player. And `FldPlayerSetIsForceDash` sets bit `0x800` of the player's `u32` action flags at `player+0x34c8` (the
player is `[[fldWorld]+0x20]+0x0`, as `FieldReads` reads it), which by its name makes the player dash whatever the
input. How the player's own input picks walk or run (stick tilt, a key) is not found here: `[NEEDS VERIFICATION]`.

## Evidence

- `0x8e7fd0` loads the player from `[[0x2ee7510]+0x20]`, ORs `0x800` into `+0x34c8` when its argument is non-zero and
  clears it otherwise. The same word is the `action34C8` the Debug probe for #702 logs; the 2026-11-24 session read
  values without bit `0x800` (`0x10`, `0x4010`, `0x4020`, `0x24020`), so it was clear in free roam there.
- `0x8e5f40` and `0x8e5eb0` are one-call wrappers on the player with the same arguments, to different functions.

## What #957 can build on this

An "always run" setting could set the force-dash bit while free roam is open, the way the script function does. That
is a write to game memory, so it needs a behaviour-changing hook-layer type under the loader's build check and a
security review, and a by-ear check that the bit runs rather than does something else in towns or dungeons. A "walk
slowly" key needs the input path first: whether the game walks on a light stick tilt, and whether the mod can scale the
stick it hands on.

## Steps

Ghidra 12.2 with JDK 25.0.5.8 (`docs/contributors/how-to/query-the-ghidra-project.md`), the shared project built from
the exe above, on 2026-11-24: one `pwsh scripts/dev/ghidra.ps1 batch` run with `decompile` of `0x8e7fd0`, `0x8e5f40`
and `0x8e5eb0`. The names and addresses are the Debug message log's `register name=... rva=...` lines of the
2026-11-24 session, the same list the squirrel raw source holds.
