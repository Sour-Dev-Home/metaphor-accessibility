# The difficulty select moves its own cursor, outside the menu cursor hook

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`chapter1-opening-inventory-own-capture-2026-11-20.md`](../raw-sources/chapter1-opening-inventory-own-capture-2026-11-20.md)
  (row 13)

New Game's difficulty list (#540) is the UI component `UUIC_Common_DifficultySelect@ui@app`, which reads the move
commands itself and keeps the choice in its own field. It never calls `CursorManagerInput`, the one function the menu
hook observes, so no cursor record reaches `MenuScreens` and not even the unknown-screen fallback ("n of total")
speaks. This is not a regression: #94's PR (#173) left the difficulty select out ("the logs don't show them"), and no
screen or feature has covered it since.

## Evidence

- **The script opens a component, not a choice list.** `DifficultySelectStart` (`0x904ca0`, from the script function
  table) is a thunk to `0xa0215a0`, which looks up or allocates a `UniversalUIController` component typed
  `app::ui::UUIC_Common_DifficultySelect` (vtable `0x2251798`).
- **Its update handles input itself.** Slot 13 (`0xf50130`), in its state 2, takes a command from the input reader
  (`0xe9acc0`): `0x14` and `0x15` move a list widget held at `[[this+0x18]+0x28]` through its vtable slots 0 and 1;
  `0x18` (pointer) asks the widget's slot 3 for the item under it. The choice is stored as `4 - row` in the `int` at
  `[[this+0x28]+0x60]`. On confirm it opens `System_mode_12`, then a `System_Choice_YESNO` box with `System_mode_13` or
  `System_mode_14`, and in state 3 passes `4 - [[this+0x28]+0x60]` to `0x3b3cb0`.
- **The mod's log agrees.** In the maintainer's 2026-11-20 14:17 run (mod built from #553's branch), every list screen
  logs a `cursor` record with `cmd=0x1` as it opens (`TitleChoice`, `YesNoChoiceSample`, `CommonTalkChoice`,
  `ConfigTopChoicePc`, `InstantMenuChoice`). Between the name-reaction dialogue's last line and the `YesNoChoiceSample`
  box that follows it there is none. The player confirmed at once there, so the run doesn't show a move.

`[NEEDS VERIFICATION]` in game:

1. moving through the list logs no `cursor` record;
2. `[[this+0x28]+0x60]` follows the focus, and which value is Storyteller (top) and which Regicide;
3. where the five names and their descriptions come from (message labels or images), and whether the confirm box is
   `System_mode_13` or `_14` for a given choice.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`, built per
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)):
`decompile 0x904ca0`, `decompile 0xa0215a0`, `vtable app::ui::UUIC_Common_DifficultySelect`, `decompile 0xf50130`,
`callees 0xf50130`. The log check: `cursor` records in `message-log.txt` in the mod's Reloaded-II folder, grouped by
`screen=`, and the records between the `MES_e00_002_001_014` request and the next `YesNoChoiceSample` cursor.
