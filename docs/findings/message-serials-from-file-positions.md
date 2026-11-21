# A message serial is its file's first serial plus its position, so the compiled source gives its text

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`quick-menu-serials-run-2026-11-20.md`](../raw-sources/quick-menu-serials-run-2026-11-20.md),
  [`name-lookup-candidates-2026-11-16.md`](../raw-sources/name-lookup-candidates-2026-11-16.md),
  [`settings-category-labels-session1-2026-11-20.md`](../raw-sources/settings-category-labels-session1-2026-11-20.md);
  the range tests are in [`settings-screens-inventory-static.md`](settings-screens-inventory-static.md)

For #584. `GetMessageBySerial` (`0x9e3d30`) returns a message it builds for the flags it was asked with, so the mod
doesn't read it. Each message file owns a run of serials that starts at a number the exe fixes, one per message in file
order. The game compiles its system files whole at start-up and the mod already keeps that source, so a serial's label
and text come from the source by position. `MessageSerials` (`src/Metaphor.Core/Messages/MessageSerials.cs`) does this
for the two system files. The quick menu is its first user: its items' cursor order comes from the order the game
fetches their help lines in. Three of its names aren't messages at all.

## Evidence

- **What the lookup returns.** `GetMessageBySerial` reads the manager at the global `0x2f04960` and asks the object at
  its `+0x208` (code in `.text1`, not decoded) for the file and the position that own the serial. It then calls
  `0x9e4140` with the file, the position and the flags. That function finds the file's compiled data in a map and
  builds the result through `0x153c96ee0` with the flags' first dword. With no manager or an unknown serial it formats
  `MESSAGE: Data not found (SERIAL ...)` instead. The result is made per call and per flags, and its layout isn't known.
  Whether it is the object the dialogue window's pointer holds is `[NEEDS VERIFICATION]`, and nothing here relies on
  it.
- **Each file's first serial is in the exe.** The message-file table at `0x27b0cd0` gives each file a range test. For
  `System_Message.msg` it is `0x9f4c90`: `lea eax,[rcx-0x476]; cmp eax,0x315; setbe al; ret`, so serials 1142–1931, 790
  messages. For `System_Message_PC.msg` it is `0x9f4c60`, with `-0x2cbd` and `0x161`, so serials 11453–11806, 354
  messages. `MessageSerialOfFile` (`0x9e8dd0`) adds a label index to a file's first serial.
- **The compiled source holds them in that order.** Both files compile at start-up, with 790 and 354 messages. By
  position from 1142 and 11453, the settings categories land on the serials the Settings list resolved (11469–11473),
  and the difficulty help lines on the ones the difficulty list resolved (1888–1890, in cursor order). A message counts
  when its `@label` line comes before `{`; `System_Dummy`'s body line `@System_Dummy` doesn't count.
- **The flags pick a style, not a message.** The Settings list resolves its names with `flags=9`, the quick menu its
  help lines with `flags=8`, and #542's run resolved an item name's serial with `flags=9` in battle and `flags=8` on
  pickup. So the text for a serial doesn't depend on the flags, as far as these show.
- **The quick menu's order.** As it opens, the game resolves `System_EasyMenu_08`, `_12`, `_13`, `_10`, `_09`, `_16`,
  `_11`, `_14` in one frame. Those are the help lines of `_01`, `_05`, `_06`, `_03`, `_02`, `_15`, `_04`, `_07`, which
  are the item names. Cursor index 3 opened the tutorials list and index 5 the glossary list. These match the 4th
  (`_10`, for `_03`) and 6th (`_16`, for `_15`) help lines. So the resolve order is the cursor order, top first. The
  maintainer heard all eight positions right on 2026-11-21 (#591).
- **Three rows draw words no message holds.** In that check, rows 4, 6 and 7 were spoken from `_03`, `_15` and `_04`,
  but the screen shows "Tutorials", "Memorandum" and "Settings". No message in the compiled files of that run's Debug
  log is exactly "Tutorials". "Memorandum" and "Settings" are only in button prompts, key names and a settings row. So
  the menu draws those names itself, perhaps as images `[NEEDS VERIFICATION]`. The maintainer chose on 2026-11-21 that
  the mod says its own English words for those three rows, and the system file's labels for the other five.
- **What's open.** The other files' first serials aren't in the table yet. One table entry's range test is
  `0x1417`+`0x304` (serials 5143–5915, 773 messages), which fits `Item_Message_1.msg`'s compiled count; which entry it
  is wasn't read `[NEEDS VERIFICATION]`. If so, `Item_Message_2.msg` (833 messages) would start at 5916, the
  description serial battle's item list resolved `[NEEDS VERIFICATION]`. The ninth quick-menu item later in the game is
  probably `_17`, and its place in the order isn't known.

## Steps

1. Ghidra 12.2 project of the exe (`scripts/dev/ghidra.ps1 decompile 0x9e3d30`, then `decompile 0x9e4140`).
2. `tools/Metaphor.SignatureCheck --bytes <exe> 0x9f4c90 16` and `0x9f4c60 16` (this repository at the commit that adds
   this note) for the range tests, and `0x27b0cd0 0x1a0` for the table's first entries.
3. The run logs, as in [`quick-menu-serials-run-2026-11-20.md`](../raw-sources/quick-menu-serials-run-2026-11-20.md).

What to check in game with this change (Debug build): each `resolve via=GetMessageBySerial` record now ends with
`label=<label> text="<text>"`, or `label=(unknown)` outside the two system files. Opening the quick menu should log the
eight `System_EasyMenu_` help lines, and the menu should say the name of the row the cursor is on, then "n of 8".
