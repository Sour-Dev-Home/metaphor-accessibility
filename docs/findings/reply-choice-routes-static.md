# A reply choice can open by four routes, and the choice hook sees only the scripted by-name one

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`msg-select-functions-2026-11-16.md`](../raw-sources/msg-select-functions-2026-11-16.md),
  [`msg-window-show-line-2026-11-20.md`](../raw-sources/msg-window-show-line-2026-11-20.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`select-window-reply-choice-2026-11-20.md`](../raw-sources/select-window-reply-choice-2026-11-20.md)

Static prep for #541, read in Ghidra only. Every reply choice reaches the window through `0x9eb890`, which four
functions call. The choice hook (`MsgRequestSelectByName`, `0x9f8d70`) sits in front of only one of them, and only for
script calls. A choice opened any other way sets no options for `ChoiceFocus`, so neither the choice nor its focus is
spoken. Gallica's first choice is a native call of `0x9ee5e0`: a 2026-11-20 run logged its select window, kind 0, and
no scripted choice (the run's record is in the last source above). Which route her later choices take is `[NEEDS VERIFICATION]`; the session check below settles it.

## Evidence

### The four routes

`0x9eb890` takes the window and a select block. The block holds the prompt's message-file id at `+0`, the kind at `+4`,
the prompt's line index at `+8`, the choice's message-file id at `+0xc` and its line index at `+0x10`, and the
prompt's label hash at `+0xa2`. It shows the prompt by calling `MsgWindowShowLine` (`0x9ed360`) with a line block whose
`+0x98` hash is copied from that `+0xa2`. Its callers:

| Builder | Takes | Reached from | Hooked |
|---|---|---|---|
| `0x9ee5e0` | kind, prompt label, choice label (C strings), a 16-bit value, options | `MsgRequestSelectByName` (`0x9f8d70`), and 39 native call sites: field code (`0x4d8d80`, `0x4e2ef0`, `0x4f5f90`, `0x4ffe60`, `0x50baa0`, `0x517600`), `fclSpotAcademiaMenu::vf8`, `UUIC_Common_DifficultySelect::vf13`, battle and others | only through `0x9f8d70` |
| `0x9ee1c0` | message-file id and line indexes (32-bit numbers), a 16-bit value, options | `0x9f8b30`, from the script functions `MsgRequestSelectID`, `MsgRequestSelectID_Ex`, `MsgRequestSystemMessageSelectID` and `…SelectID_Ex` | no |
| `0x9ee3f0` | kind, a pointer to the prompt's 16-byte label hash, a pointer to the choice's, a 16-bit value, options | no caller in analysed code | no |
| `0x9ee9f0` | `[NEEDS VERIFICATION]` | `0x9f8fe0` (from `0x5b9910`, `0x5d2c50`), `0x4abe40`, `0x72f920` | no |

- `MsgRequestSelect`, `MsgRequestSelect_Ex`, `_CancelExit`, `_Shop`, `MsgRequestSelectAccessSpot(_Ex)` and
  `MsgRequestSystemMessageSelect(_Ex)` all call `0x9f8d70`, so every scripted by-name choice is heard. The by-id script
  functions are not.
- `0x9ee3f0` looks both hashes up in the window (`0x9e60f0`, as the by-hash line routes `0x9ec100` and `0x9ed120` do).
  It is the select counterpart of those line routes, which also have no analysed caller.
- `0x9ee1c0` fills the block's prompt hash with the MD5 of the empty string (`d41d8cd9…ecf8427e`), as `0x9ec320` does
  before it overwrites it. So a by-id choice shows its prompt with that hash.

### What the shown-line record says about the route

`MsgWindowShowLine`'s `request` record at a choice carries the prompt's hash. In the 2026-11-20 run, the Scheming Man's
choice logged `select … first="MES_e00_002_001_004"` and, in the same millisecond, `request via=MsgWindowShowLine
kind=0 hash=802AF9B8…`, which is the MD5 of `MES_e00_002_001_004`. So:

- hash `D41D8CD98F00B204E9800998ECF8427E` (empty string): the by-id route, `0x9ee1c0`;
- the MD5 of a label in a compiled file, with no `select` record: a native by-name call of `0x9ee5e0`, or `0x9ee3f0`
  by hash (the record doesn't tell these two apart).

### `ChoiceFocus` and the menus' open state

`ChoiceFocus` observes the cursor hook directly (`HookServices`), not through `MenuScreens`, so the camp-menu handle's
open state (#571) doesn't gate it. It speaks only after a `ChoiceRequested` fills its options. Without one it stays
silent whatever the cursor does.

### Mapping a by-id choice to its text

The compile hook's `id` (`compile id=60 path=…/e01_002_001.msg`) may be the message-file id the window's lookup
(`0x9e7460`) takes. If so, a by-id choice is the compiled file with that id and the entry at that index.
`[NEEDS VERIFICATION]`.

## Session check

Debug build, `dialogue-window` on, new game to the scene with Gallica after the opening cutscene, then her
conversations in the first walkable area. At each silent choice, before pressing anything:

1. In `message-log.txt`, take the `request via=MsgWindowShowLine` record logged when the choice appeared, and note any
   `select` and `cursor` records from then until the choice closes. Check the latest `compile` record before it: its id
   and file.
2. Hash `D41D8CD9…8427E`: by id. Otherwise MD5 the labels of that file (`@MES_…` lines in its `compile` block) to find
   the prompt, and see whether the next label is a `SEL`.
3. `cursor` records: note the `screen=` (expected `CommonTalkChoice@ope@ui@app`) and the manager. Records naming another
   screen, or none, mean the choice moves by another cursor.
4. Move the cursor once each way and confirm a `cursor` record per settle.

Telling `0x9ee5e0` from `0x9ee3f0` takes a development listener on each builder, as on `MsgSelectedId`.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project from `pwsh scripts/dev/ghidra.ps1 build` on the
build above, Windows 11 x64, 2026-11-20. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `callers 0x9eb890`, then `decompile 0x9eb890`: the four builders and the block fields.
2. `callers` of `0x9ee5e0`, `0x9ee1c0`, `0x9ee3f0`, `0x9ee9f0`, `0x9f8d70`, `0x9f8b30`, `0x9f8fe0`; script names from
   the squirrel list by address.
3. `decompile 0x9ee3f0` and `disasm 0x9ee3f0` (the first `0x9e60f0` call takes `rdx` unchanged); `decompile 0x9ee1c0`,
   `0x9ee5e0`; `disasm 0x90c6c0` (`MsgRequestSelectID` calls `0x9f8b30` with kind 0).
4. The MD5 of `MES_e00_002_001_004` from `md5sum`, against the hash in the run's `message-log.txt` (Reloaded-II's mod
   folder; not committed).
