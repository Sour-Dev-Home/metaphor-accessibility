# The nearby list's globals load in three short `.ecode` readers; a door's reach value is `fldDatabase+0x58` when set

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-player-frame-hook-static.md`](field-player-frame-hook-static.md),
  [`field-interaction-prompt-static.md`](field-interaction-prompt-static.md),
  [`field-units-names-exits-static.md`](field-units-names-exits-static.md),
  [`field-position-facing-map-static.md`](field-position-facing-map-static.md)

For #181. The mod locates the field's three singletons by a `mov rcx,[rip+disp32]` (7 bytes) in an `.ecode` function
that reads each, as it does its other globals (#300), and the person table by the third `lea` of
`ResourceTablesLoaded`. None of these functions is hooked or called. The door reach test's distance limit comes from
`0x5ab030(fldDatabase)`, which returns the `int` at `fldDatabase+0x58` unless it is `-1`; otherwise it reads a table
through a `.text1` stub, so the mod takes the shorter limit then. Nothing here was read in a running game: every
runtime value is `[NEEDS VERIFICATION]`.

| Global | RVA | Read in | Load at | Signature |
|---|---|---|---|---|
| `fldWorld` | `0x2ee7510` | navmesh snap `0x576410` | `+0x17` | `FieldNavmeshSnap` |
| `fldScene_Root` | `0x2ee7348` | `0x522db0` (its only reader) | `+0xd` | `FieldSceneRootRead` |
| `fldDatabase` | `0x2ee7368` | `0x5767f0` (calls `0x5ab030`) | `+0x9` | `FieldDatabaseRead` |
| `NpcID.bin` table | `0x2eebc38` | `ResourceTablesLoaded` `0x89b3b0` (`lea`) | `+0x2c` | `ResourceTablesLoaded` |

## Evidence

- **The loads.** At each load, the instruction's end plus its displacement gives the global: `0x522dc4 + 0x29c4584 =
  0x2ee7348`, `0x576800 + 0x2970b68 = 0x2ee7368`, `0x57642e + 0x29710e2 = 0x2ee7510`. `global 0x2ee7348` lists one
  write (`0x5220f5`) and one read (`0x522dbd`); `global 0x2eebc38` lists the getter `0x8990c0` that
  [`FldNpcGetCharaID`](field-units-names-exits-static.md) uses and the `lea` at `0x89b3dc`.
- **The door reach value.** `0x5ab030` reads `int [rcx+0x58]` and returns it unless it is `-1`. Otherwise it takes the
  map id at `+0x48` and calls `0x898fa0`, which is a `jmp` into `.text1`, so the row it reads is unknown. The
  [door's reach test](field-interaction-prompt-static.md) uses 150 when the value is 1 or 2 and 300 otherwise.

## What would settle it

A Debug run with the field keys: the log's `field nearby` lines give the player's and each thing's position, its
distance, bearing and reach, and `doorValue`. Walk up to a door and a chest until the prompt shows: the logged
distance at that moment should be just under 150 or 300. Pace a known stretch to measure units per metre.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project of the build above, Windows 11 x64,
2026-11-21. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says otherwise.

1. `global 0x2ee7348`, `disasm 0x522db0`; `global 0x2ee7368`, `disasm 0x5767f0`; `global 0x2eebc38`.
2. `disasm 0x5ab030`, `disasm 0x898fa0` (a `jmp` into `.text1`, not followed).
3. `tools/Metaphor.SignatureCheck --bytes <exe>` at `0x522db0`, `0x5767f0` and `0x576410` for the displacements;
   `bash scripts/check.sh signatures <exe>`: each of the three new signatures matches once.

Nothing in `.text1` was decompiled. No decompiled output is quoted; names are the exe's own or the earlier notes'.
