# Item and Equipment menus, from the session of 2026-11-22 (#216, #217)

- **Source:** the maintainer's session c of 2026-11-22, Debug session build `f66f83a` (dirty tree, `hooks 35/35`) with
  the opt-ins `ui-text-panes`, `create-msg-from-string`, `tutorial-window` and `inspector`. Its report log and
  `message-log.txt` share one millisecond clock and were re-read offline with grep and awk; the game wasn't run for this
  note. The logs hold game text, so they stay local; only labels, serials, flags, indexes, counts and times are kept.
- **Saved:** 2026-11-22.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## What it shows

Cursor records in the report log: `ItemMenuChoice` 68, `ItemCharaSelChoice` 11, `EquipCharaSelChoice` 10,
`EquipPartsChoice` 32, `EquipMenuItemList` 22, `EquipSwapCharaSelChoice` 0. No `silent` record falls between 428000 and
720000 ms. In that window the message log has 206 690 lines: `pane` records (every frame, and every `text=` read is
pointer bytes, not text), 1635 `resolve via=GetMessageBySerial`, 146 `cursor`, 96 `cue`, 11 `lookup id=55`.

Item menu, 430246 to 562612 ms:

- Each `ItemMenuChoice` record with `cmd=0x1 index=0` (a tab's list opening) follows, in the same or the previous
  millisecond: `ItemName_<group>_<n>` (`flags=9`) then its `ItemText_` (`flags=7`), up to eight pairs; then serials 2, 4,
  7, 8, 11, 16, 21 (`flags=9`, `label=(unknown)`); then `Item_Tab_01`, `_03`, `_04`, `_11`, `_06`, `_07`, `_08`, `_09`
  (serials 1369, 1371, 1372, 1379, 1374, 1375, 1376, 1377, `flags=2`), the same order every time.
- The `count=` on those records went 136, 7, 54, 36, 103, 33, 45, 59, then 136 again. The name groups at each were
  `Consumable`; `CASH` (7 names); `Insects`, `Scenario`, `Follower`; `Info`; then four lists of unlabelled serials
  (2xxx with `flags=9`, each followed by one or two `flags=7` serials) for 103, 33, 45 and 59.
- Moving down the `count=136` list from index 8 to 12, each `cmd=0x14` move resolved exactly one new `ItemName_` (and
  its `ItemText_`) just before its cursor record: `Consumable_13`, `_17`, `_21`, `_19`, `_23`.
- No record carries a quantity.
- `ItemCharaSelChoice` (`count=8`, 547842 to 551598 ms): no `resolve` names a party member on a move; the resolves
  between moves are item rows again.

Equipment menu, 565342 to 717448 ms:

- Entering `EquipCharaSelChoice` (`count=8`) resolves 1084, 1108, 2, 1087, 4, 1113, 7, 1117, 8, 1083, 11, 1101, 16, 21
  (all `label=(unknown)`). Each move then resolves a block: index 0 `9441 …`; index 1 `2 9447 …`; index 2 `4 9442 …`;
  index 3 `7 9448 …`; index 4 `8 9449 …`.
- The block for index 4, repeated on `EquipPartsChoice` (`count=5`): `8` (`flags=9`), `9449` (`flags=8`), `4992`
  (`flags=6`), `System_General_10`, `_01`, `_02`, `System_General_30` (1511, `flags=21`), `2697` (`flags=8`), `3903`
  (`flags=7`), `_03`, `_04`, `System_General_31` (1512), `2944`, `4150`, `5091` (`flags=6`), `System_General_32`
  (1513), `3257`, `4463`, `System_General_33` (1514), `3504`, `4710`. Each `flags=7` serial is its `flags=8` one plus
  1206.
- `EquipMenuItemList` (`count=7`) opened from `EquipPartsChoice index=1` at 678901 ms. Its new resolves:
  `MainMenu_Equip_03`, then 2688, 2687, 2684, 2693, 2694 (`flags=8`), each followed by its +1206 serial, one with a
  `flags=6` serial (4855) after it.
- A confirm at list index 4 (690692 ms) returned to `EquipPartsChoice` with 2693 after `System_General_30` in place of
  2697. Reopened at 696292 ms, the list resolved 2697, 2688, 2687, 2684, 2694. After the list's index 2 (698957 ms) the
  next block (717448 ms, character index 4) shows 2687 after `System_General_30`.

## Steps

1. Count the cursor records per screen class in the report log (grep `screen=<class>`).
2. Cut the message log to 428000–720000 ms with awk on the first field.
3. For each `cursor` record, print the `resolve` serials and labels since the previous one, keeping the order.
