# The `global` query finds the load each global reader's signature points at

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`menus-open-session6-2026-11-17.md`](../raw-sources/menus-open-session6-2026-11-17.md),
  [`dialogue-window-flag-2026-11-16.md`](../raw-sources/dialogue-window-flag-2026-11-16.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - CampStatusIsOpenBody | 0x51fad0 | .ecode | Menus | read | Loads the menus' handle global at its offset 9; the signature's single match. | finding; src/Metaphor.Hooks/MenuHooks.cs
  - MsgIsSelectWindowOpenedBody | 0x9ea970 | .ecode | Dialogue | read | Loads the select window global `W` with its first instruction. | finding; src/Metaphor.Hooks/DialogueWindowPointer.cs
  - Menus handle | 0x2ee7338 | .debug | Menus | read | The global holding the menus' handle, loaded by `CampStatusIsOpenBody`. | finding; src/Metaphor.Hooks/MenuHooks.cs; docs/findings/battle-turn-icons-and-ui-components-static.md; docs/raw-sources/dialogue-window-flag-2026-11-16.md
  - W | 0x2f04960 | .debug | Dialogue | read | The select window global, loaded by `MsgIsSelectWindowOpenedBody`. | finding; docs/raw-sources/dialogue-window-flag-2026-11-16.md; src/Metaphor.Hooks/DialogueWindowPointer.cs

`pwsh scripts/dev/ghidra.ps1 global <rva>` (#304) lists the RIP-relative load in each reader that `hook-signatures.json`
uses to find the menus' handle global (#268) and the message window global (#232).

## Evidence

| Global | Reader signature | Its load | What `global` lists there |
|---|---|---|---|
| `0x2ee7338`, the menus' handle | `CampStatusIsOpenBody` ("load at +9") | `0x51fad9` | `read`, `MOV RCX,qword ptr [0x142ee7338]`, in the function at `0x51fad0` |
| `0x2f04960`, `W` | `MsgIsSelectWindowOpenedBody` (first instruction, rva `0x9ea970`) | `0x9ea970` | `read`, `MOV RCX,qword ptr [0x142f04960]`, no function; label `MsgIsSelectWindowOpenedBody @ 0x9ea970` |

- **Where `CampStatusIsOpenBody` is.** The signature's entry gives no rva. `CampStatus_IsClosed` is `0x903ab0` (the
  script function list); `disasm 0x903ab0` calls `0x51f370`, which is a `JMP` to `0x51fad0`. `disasm 0x51fad0` reads
  `MOVSXD R8,dword ptr [RCX]`, `CMP R8D,-0x1`, `JNZ`, then the load at `0x51fad9` and a second load of the same global
  at `0x51fae5`, the instructions the pattern spells (`4C 63 01 41 83 F8 FF 75 0C 48 8B 0D …`, then `48 8B 05` at
  +0x15). The project has no `CampStatusIsOpenBody` label: it was built on 2026-11-17, before that entry was added
  (`8d3882d`), and `build` reuses a complete project. Since #390, `build` on that project added the label
  (2026-11-19, same game build and Ghidra 12.2): the pattern's single match is `0x51fad0`, and `global 0x2ee7338` now
  names both loads `CampStatusIsOpenBody @ 0x51fad0`.
- **Counts.** `0x2ee7338`: 25 instructions, 24 reads and one write (`0x51f683`). `0x2f04960`: 159 instructions, 156
  reads and three writes (`0x9e62dd`, `0x9eb754`, `0x9eb79f`); 157 of them in `0x9d0000`..`0xa10000`, where the
  dialogue raw source counted 162 reads with `dumpbin`. [NEEDS VERIFICATION: the difference of 5; code auto-analysis
  didn't disassemble is not scanned.]
- An address nothing loads (`global 0x123`) prints `# none found` and exits 0.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on the
build above (`out/ghidra/548ddc955c176867/`, ready 2026-11-17), on Windows 11 x64.

1. `pwsh scripts/dev/ghidra.ps1 global 0x2ee7338` and find `0x51fad9`.
2. `pwsh scripts/dev/ghidra.ps1 disasm 0x903ab0`, then `disasm 0x51f370` and `disasm 0x51fad0`, to tie that load to
   `CampStatus_IsClosed`.
3. `pwsh scripts/dev/ghidra.ps1 global 0x2f04960` and find `0x9ea970`.

The answers are in `out/ghidra/out/`; they hold disassembly, so they stay there (rule 4).
