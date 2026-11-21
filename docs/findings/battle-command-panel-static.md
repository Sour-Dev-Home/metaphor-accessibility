# The battle command panel is one state machine, and one function opens each command's screen

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md),
  [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md)

The per-turn command buttons are `UUIC_Battle_Command` (vtable `0x224ed40`), not a list, which is why no shared cursor
moves on them (#247, 2026-11-20). Its update keeps a state at `+0x50`; choosing a command calls `0xf10ba0`
(`BattleCommandOpenSub`, our name) with the command's code, which stores it at `+0x58` and opens that command's
screen. The labels on the buttons were not found: they are the layout's own `[NEEDS VERIFICATION]`. Static only.

## Evidence

- **Update.** Slot 13, `0xf11290` (`BattleCommandUpdate`), switches on `[this+0x50]` - 1 over 0x18 cases. Other
  slots: slot 3 (`0xf10030`) runs a pending command stored at `+0xB0` (with `+0xB4`, `+0xB8`) through `0xf10ba0`;
  slot 5 (`0xf0ce00`) closes the panel (clears the shown byte at `+0x10`) and keeps the last state at `+0xB0`.
- **Command codes.** `0xf10ba0(this, code, a, b)` writes `code` to `+0x58`, sets `+0x50` to 0x1b (waiting) and
  switches on the code. Each case creates a screen through a call into a thunk (`0xe2486f0`, not followed) with a
  screen number and a data object whose class the type information names:

  | Code | Screen number | Data object | Reading |
  |---|---|---|---|
  | 2 | `0x45` | built inline | `[NEEDS VERIFICATION]` |
  | 3, 4, 5 | none | `0xf16170(…, 1/2/3)` | three commands with no screen, such as Guard or Pass `[NEEDS VERIFICATION]` |
  | 6 | `0x46` | `BTLSkillSelect_Data` (`0xd74530`) | Archetype skills, the `BTL_SkillSelect` list |
  | 7 | `0x51` | built inline | Synthesis by elimination `[NEEDS VERIFICATION]` |
  | 9 | `0x49` | `BTLItemSelect_Data` (`0xd73fd0`) | Item, the `BTL_ItemSelect` list |
  | `0xF` | `0x4B` | `BTLOrderTop_Data` (`0xd74460`) | Tactics/Escape, `BTL_OrderTop` |
  | `0x13` (slot 3) | `0x4F` | built inline | `[NEEDS VERIFICATION]` |

- **Acting member.** Codes 6 and `0xF` copy `[this+0x128]` into their data object's second field, and code 6 passes
  it to `0xf2cf20` to pick per-member rows from three tables at `+0x158`, `+0x1B8` and `+0x218`. So `+0x128` is the
  member whose turn it is, a lead `[NEEDS VERIFICATION]`.
- **Signatures.** `BattleCommandOpenSub` (rva `0xf10ba0`) and `BattleCommandUpdate` (rva `0xf11290`, with its
  jump-table address wildcarded) each match once on disk
  (`tools/Metaphor.SignatureCheck/battle-turn-probes.json`).

For #247's turn line: a hook on `BattleCommandUpdate` that lets it run and reads `+0x50` and `+0x128` sees the panel
open for each member (a new `+0x128`, or `+0x50` entering 1, the case that waits for the layout and then shows it
through `0xf11e40`). Package `+0x2F0` (session 6) counts the commands. A hook on `BattleCommandOpenSub` names each
command chosen, by code. Speaking the six buttons needs their labels: the six commands and their buttons in the battle
inventory can be our own strings until the layout's text is found.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-20.

1. `vtable app::ui::UUIC_Battle_Command`; `decompile 0xf11290`, `0xf10030`, `0xf0ce00`, `0xf13210` (case 2's return).
2. `decompile 0xf10ba0` and `callers 0xf10ba0` (slot 3 and ten calls inside the panel's own functions).
3. `decompile 0xd74530`, `0xd73fd0`, `0xd74460`: each stores the named `_Data` vtable.
4. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0xf10ba0 48` and `0xf11290 48`,
   then `--probes <exe> tools/Metaphor.SignatureCheck/battle-turn-probes.json`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled; the screen-creating thunk was not followed.
