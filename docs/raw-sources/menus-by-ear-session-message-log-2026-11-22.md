# Item, Equipment, shop and quest screens, from the by-ear session of 2026-11-22 (#216, #217, #736, #194)

- **Source:** the maintainer's by-ear session `2026-11-22 22.50.00`, mod `618cf84` (Debug), its `message-log.txt`,
  `report-log.txt` and `transcript.txt` (the mod's own speech, on the same millisecond clock). Re-read offline with grep
  and awk on 2026-11-23; the game wasn't run for this note. The logs hold game text, so they stay local; only labels,
  serials, flags, cursor fields, counts and times are kept here, plus the words of a few menu labels.
- **Saved:** 2026-11-23.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Cursor fields

The `cursor` record prints `top=` and `row=` from `CursorInput.Top` and `CursorInput.Row`, with `index = top + row`.
On the Item list (`count=136`), wrapping up from index 0 logged `cmd=0x15 index=135 top=7 row=128`, and stepping back
logged `index=130 top=2 row=128`, `index=129 top=1 row=128`, `index=128 top=0 row=128`. So `row` is the first row on
view and `top` is the focus's line in the view (eight lines).

## Item menu (`ItemMenuChoice`, 1 789 235 to 1 893 993 ms)

- Every settled input of an opening, a tab switch and a move (`cmd=0x14`/`0x15` then `cmd=0x1`) is preceded, in the same
  millisecond as the move or within 1 ms, by one batch: the rows on view as `ItemName_*` (`flags=9`, each followed by
  its `ItemText_*`, `flags=7`) or, on the equipment tabs, unlabelled serials (`flags=9`); then serials 2, 4, 7, 8, 11,
  16, 21 (`flags=9`, no label); then `Item_Tab_01`, `_03`, `_04`, `_11`, `_06`, `_07`, `_08`, `_09` (`flags=2`).
- At the wrap to index 135 the batch held eight names (`ItemName_BATH_01` … `ItemName_AEXP_UP04`): the rows 128 to 135.
  The mod said "136 of 136" with no name.
- A tab switch is a settled `cmd=0x1` with no move before it, its count changed: 136, 7, 54, 36, 103, 33, 45, 59, then
  136 again; the first six each followed a press of the shoulder button the game masks as `0x0800`
  (`input pad0 buttons=0x0800`). The name groups of
  the first four tabs' batches: `Consumable`, `BATH`, `FISH`, `SEED`, `AEXP` (Use); `CASH` (Exchange); `Scenario`,
  `Insects`, `Follower` (Key Items); `Info` (Information). The equipment tabs' first batches began 2788 … 2697 (103),
  3063 … 2901 (33), 3257 … 3227 (45), 3305 … 3375 (59).
- The switch to the 59-row tab, at index 0 from index 0 (1 863 833 ms), has no transcript line: the screen event of an
  unchanged index isn't told.
- The first entry said "Item, Expensive Medicine, 1 of 136", though the batch's first name was `ItemName_Consumable_100`
  (row 0, said as such at 1 793 654 ms after a move).

## Equipment swap (`EquipSwapCharaSelChoice`)

At 2 062 225 and 2 090 871 ms, `MainMenu_Equip_25` (serial 1478, `flags=9`, "Change whose equipment?") then
`Shop_Other_19` and `_20` (DEF, EVA) resolve just before `cmd=0x1 index=0 count=1`. The mod said "1 of 1".

## Shop (2 413 704 to 2 731 510 ms)

- Sell (top menu index 1): `ItemShopChoice` settled `cmd=0x1 index=0` six times with no move between, counts 6, 136,
  73, 21, 32, 39, each after a batch of `flags=8` names. The batches began with `Shop_Other_41` (serial 1412) and
  `ItemName_CASH_*`; `ItemName_Consumable_*`; 2447 …; 2901 …; 3095 …; 3305 …. All but the 32-row batch also fetched the
  tab names `Item_Tab_03`, `_01`, `_06`, `_07`, `_08`, `_09` (`flags=2`). Only the first was spoken ("Sell All, 1 of 6").
- Buy: moving the top menu to index 0 (2 466 268 ms) fetched the top labels, `Shop_Other_32`, then `Shop_Other_41` and
  five `ItemName_CASH_*` and the tab names, before the top menu's settled input (2 466 274 ms). The Buy list's own
  batch (`Shop_Other_03`, `_17`, `_18`, then serials 2453 …) came at 2 466 358 ms, before its first input at
  2 466 365 ms. The mod said "Sell All, 1 of 37".
- The confirm (`ItemShopConfirmChoice`, `count=2`): at 2 704 283 ms `System_Window_02` (serial 1431, "Buy"),
  `System_Window_01` (serial 1430, "Cancel"), `Shop_Other_01` (serial 1380) and the bought row's serial resolved
  (`flags=8`); no cursor input came between them and the confirm's first, at 2 706 220 ms. Its move at 2 708 179 ms
  refetched the Buy list's batch, with no `System_Window_` label. The mod said "1 of 2", "2 of 2".

## Quest list (`CampQuestChoice`, 2 181 387 ms on)

Opening resolved `System_Quest_list_10`, `_13`, `_14`, `_01`, `_16`, `_09`, `_11`, then `_07`, `System_QUEST_24`,
`System_Quest_list_08`, `_06` (`flags=2`), then 9606, `System_QUEST_20`, 9607, 9608, 9567, `System_QUEST_12`
(`flags=9`) and `System_Quest_list_02` (`flags=8`), before `index=0 count=4`. Each move refetched the same set. The
lists of 68 and 72 rows fetched 95xx and 96xx serials with `System_QUEST_07` and `_11` among them. None of the 9xxx
serials has a label: their file isn't in the mod's serial table.
