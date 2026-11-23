# The quest list fetches its rows' names from `Quest_1.msg`, and an opened quest its text from `Quest_2.msg`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do (the
  message log of an earlier play session, read only)
- **Sources:** [`menus-by-ear-session-message-log-2026-11-22.md`](../raw-sources/menus-by-ear-session-message-log-2026-11-22.md),
  none else, see Steps

For #194. The exe's message-file table gives `message/quest/Quest_1.msg` serials 9508–9643 (136 messages) and
`message/quest/Quest_2.msg` 9644–10100 (457). The quest list's 95xx and 96xx serials, unlabelled until now, are quest
names from `Quest_1.msg`, and the serial an opened quest fetches with flags 7 is one of its texts in `Quest_2.msg`.

## Evidence

- **The table** (layout in [the equipment note](equipment-character-archetype-serials-static.md)): entry 32
  (`0x27b26c8`) is `message/quest/Quest_1.msg` with range test `0x9f4b80`, `8D 81 DC DA FF FF` then `3D 87 00 00 00`
  (`lea eax,[rcx-0x2524]; cmp eax,0x87`): first 9508, count 136. Entry 33 (`0x27b2798`) is `message/quest/Quest_2.msg`
  with test `0x9f4b90`, `8D 81 54 DA FF FF` then `3D C8 01 00 00`: first 9644, count 457. Entry 31,
  `Item_TypeName.msg` (`0x9f4b70`), ends at 9507, so the files are contiguous.
- **Compiled counts.** In the Debug `message-log.txt` of the by-ear session of 2026-11-22 (mod `618cf84`), the
  `compile` records `id=33` and `id=34` (entry index + 1) hold 136 distinct labels (`QuestName_None`, then
  `QuestName_000` to `_134`) and 457 (`QuestInfo_<quest>_<n>`). So `QuestName_<n>` is serial 9509 + n.
- **The list.** `CampQuestChoice`'s opening and each move fetched the view's row names with `flags=9`: 9606, 9607, 9608
  and 9567, then the settled cursor `index=0 count=4`. Those are `QuestName_097` to `_099` and `_058`, and their text,
  read locally from the log, matches the four in-progress quests the maintainer read out, in that order. Two system
  labels, `System_QUEST_20` and `_12`, come with `flags=9` in the same batch; which row each belongs to, and the
  `System_QUEST_14` and `System_Quest_list_15` fetched once per view, are `[NEEDS VERIFICATION]`: state and deadline
  per row aren't known.
- **An opened quest.** Before `CampQuestDetailChoice`'s opening cursor (2 243 038 ms), the game fetched 9622
  (`QuestName_113`) with `flags=10` and 10031 (`QuestInfo_113_002`) with `flags=7`. In session c the first row's quest
  opened with 9979, `QuestInfo_097_000`. Which `QuestInfo_` line the game picks (by the quest's progress, probably) is
  `[NEEDS VERIFICATION]`; the mod says the one it fetched.

## Steps

`tools/Metaphor.SignatureCheck` (this repository at the commit that adds this note), on the build above, 2026-11-23:

1. `--bytes <exe> 0x27b26c8 0x28` and `0x27b2798`: path pointer at `+0x8`, length at `+0x10`, test at `+0x20`; read
   the path with `--bytes <exe> <pointer − 0x140000000> <length>`.
2. `--bytes <exe> 0x9f4b80 12` and `0x9f4b90`.
3. In a Debug `message-log.txt`, count the distinct `@` labels after the `compile id=33` and `id=34` records (the game
   compiles both files twice, interleaved), then read the `resolve` records around each `CampQuestChoice` and
   `CampQuestDetailChoice` cursor record. No game text is quoted here beyond labels.
