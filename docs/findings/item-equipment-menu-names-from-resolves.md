# The Item and Equipment menus resolve their row names by serial, in row order, just before each cursor record

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`item-equipment-menus-message-log-2026-11-22.md`](../raw-sources/item-equipment-menus-message-log-2026-11-22.md);
  the serial ranges are in [`message-serials-from-file-positions.md`](message-serials-from-file-positions.md)

For #216 and #217. Neither menu's text panes hold readable text, but both fetch every name they draw through
`GetMessageBySerial`, which the mod already logs as `resolve`. The Item menu fetches the visible rows' names top first,
then all eight tab names; the Equipment menu fetches each slot's label and the worn item's name. So the tab, item, slot
and worn-item names are in hand by serial. Character names and the equipment files' text aren't yet.

## Evidence

Item menu (`ItemMenuChoice`):

- **Tabs.** Every list opening resolves `Item_Tab_01`, `_03`, `_04`, `_11`, `_06`, `_07`, `_08`, `_09` in one fixed
  order, all in `System_Message.msg` (serials 1369–1379), which `MessageSerials` already reads. The lists the player
  stepped through held, in that order, `Consumable`, `CASH`, `Scenario`/`Insects`/`Follower` and `Info` items, then
  four lists of equipment serials. So the fetch order is the tab order and tab `i` is the `i`-th label
  `[NEEDS VERIFICATION]` by ear for the last four. No record says which tab is active. The first `ItemName_` group of
  the opening batch gives the first four tabs. The four equipment tabs can't be told apart by label, but they can by
  their place in the switch sequence and by the `count=` (103, 33, 45, 59 here, which changes with the inventory)
  `[NEEDS VERIFICATION]`.
- **Rows.** The opening batch resolves up to eight `ItemName_` serials (`flags=9`), each followed by its `ItemText_`
  description (`flags=7`), top row first. Each step down past the eighth row resolves one more name just before its
  cursor record. So row `n` is the `n`-th name resolved since the opening. That held for indexes 0–12 of the
  consumables list. Scrolling back up and paging weren't seen `[NEEDS VERIFICATION]`.
- **Item text files.** `ItemName_` and `ItemText_` serials (5143 and up, 5916 and up) are in `Item_Message_1.msg` and
  `_2.msg`, which `MessageSerials` doesn't read on `main` yet (#631's `item-skill-message-serials-static.md`). The
  equipment tabs' 2xxx serials have no label in this build's logs; their file isn't known `[NEEDS VERIFICATION]`.
- **Count.** `count=` matched the names seen only for the 7-row exchange list. Whether 136 on the consumables list is
  its row total is `[NEEDS VERIFICATION]`. No record carries an item's quantity, so the game draws it as a number, not
  a message `[NEEDS VERIFICATION]`.
- **Target list** (`ItemCharaSelChoice`, `count=8`): no move resolves a member's name.

Equipment menu:

- **Characters** (`EquipCharaSelChoice`, `count=8`). Index 0 resolves `9441` with no small serial before it; indexes
  1–4 resolve 2, 4, 7, 8 before their 94xx serial, the same order as the entry batch's 2, 4, 7, 8, 11, 16, 21. So row 0
  is the protagonist and rows 1–7 are characters 2, 4, 7, 8, 11, 16, 21, if those serials are character IDs as in
  `menu-values-party-unit-static.md` `[NEEDS VERIFICATION]`. Their text isn't logged (no label), and the protagonist's
  name isn't a message. The 94xx serials are probably archetype names `[NEEDS VERIFICATION]`.
- **Slots** (`EquipPartsChoice`, `count=5`). Each character's block resolves the 94xx serial, then
  `System_General_30` to `_33` (serials 1511–1514), each followed by the worn item's name (`flags=8`) and its
  description (`flags=7`, the name plus 1206). Index 1 opened the list whose confirm changed the name after
  `System_General_30`. So rows 1–4 are those four labels, and row 0 is the archetype `[NEEDS VERIFICATION]`, matching
  #217's list.
- **Item list** (`EquipMenuItemList`). Opening it resolves `MainMenu_Equip_03` then each listed item's name and
  description, except the worn one's. With 2697 worn the list resolved 2688, 2687, 2684, 2693, 2694, and index 4 equipped
  2693. With 2693 worn it resolved 2697, 2688, 2687, 2684, 2694, and index 2 equipped 2687. Both fit one row order,
  2697, 2688, 2687, 2684, 2693, 2694, where the worn row keeps its place but isn't fetched again. "Worn first" fails
  the second confirm. So the worn row is the one missing from the batch, and its name is the one after the slot label.
  Where it sits isn't in the messages `[NEEDS VERIFICATION]`. `count=7` against six rows is unexplained
  `[NEEDS VERIFICATION]`.

## Steps

1. The run, the counts and the per-cursor resolve listing in the raw source.
2. Serial ranges from `message-serials-from-file-positions.md`: 1369–1379 and 1511–1514 fall in `System_Message.msg`
   (1142–1931).
