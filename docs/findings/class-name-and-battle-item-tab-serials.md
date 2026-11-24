# ClassName.msg is serials 1082 to 1141 and CommandBattle_Message.msg 1932 to 2090; the menus fetch the current archetype and the battle item tabs from them

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do (the Debug message log of a by-ear session, read only)
- **Sources:** none, see Steps; builds on
  [`equipment-character-archetype-serials-static.md`](equipment-character-archetype-serials-static.md) and
  [`follower-and-archetype-tree-screen-classes-static.md`](follower-and-archetype-tree-screen-classes-static.md)

For #857, #220 and #851. `ClassName.msg` holds the specific archetypes (60 messages, three to five per line, labelled
`ClassName_<line>_<rank>`, where `<line>` is the `ATCategoryName_<line>` the archetype belongs to); its range is
1082–1141. `CommandBattle_Message.msg` (159 messages) is 1932–2090, and its `CommandBattle_IT_CATEGORY_01` to `_05` are
2085–2089, the battle item list's five tab names.

## Evidence

- **Counts.** In the Debug `message-log.txt` of the by-ear session of 2026-11-23 evening (mod 4d936b2 Debug, local, not
  committed: it holds the game's text), counting each compiled file's messages as `MessageLibrary.Parse` does gives
  `ClassName` 60, `CommandBattle_Message` 159, `Attribute_Message` 22, `FollowerName_Message` 30 and
  `jyokyo_help_Message` 259. Some `ClassName` bodies are their own label (unused slots); `Parse` counts them too.
- **Contiguity.** `System_Message.msg` starts at 1142 and `PcParam_Message.msg` at 2402 (the range tests in the
  [serials note](equipment-character-archetype-serials-static.md)). 1142 − 60 = 1082 for `ClassName`, and 1142 + 790 =
  1932, then 1932 + 159 + 22 + 30 + 259 = 2402 exactly, for compile ids 7 to 10 in order. `ClassName`'s own range test
  is a `.text1` jump, so 1082 is from the log, not the exe.
- **The Equipment character list** (`EquipCharaSelChoice`, 2215302 ms) fetches seven unlabelled serials with flags 9
  before its first cursor: 1084, 1108, 1087, 1113, 1117, 1083, 1101, in list order (the party names come between them
  with flags 2). As each row is then focused it fetches its `ATCategoryName` line. Taking 1082 as the first serial,
  each of the seven falls in its row's line: row 0 01 (`ClassName_01_03`), row 1 07, row 2 02, row 3 08, row 4 09,
  row 5 01, row 6 05, against `ATCategoryName_01`, `_07`, `_02`, `_08`, `_09`, `_01` and `_05`. Gallica's row 7
  fetches neither. By ear, the protagonist's and row 2's archetypes were the ones the maintainer named for #857.
- **The Archetype Tree** (after `AcademiaClassGetCharaSel`, 2385097 ms on) fetches the focused node's archetype with
  flags 10, then its line's `ATCategoryName` with flags 8, then a `lookup id=55` whose label is the serial − 1082: 1084
  with `_01` and label 2, 1137 with `_17` and label 55, 1120 with `_10` and label 38, each in the line its label names. A
  studied node then fetches `System_Academy_24` ("Studied"); one not studied fetches `System_Academy_02` and a
  prerequisite archetype with flags 9. The `Detail@tree@academia@layout` animation 2090 comes before some moves' fetches,
  not all (1084 at 2387898 had none).
- **The battle item list** (`BTL_ItemSelect`) fetches 2085–2089 (flags 2) just before its cursor settles on opening
  (2067856, 2069907 ms), and after one tab switch (2074497 ms); a switch with RB (2070310, 2071039 ms) fetched nothing,
  only the cursor's count changed (11, 5, 14). So the names don't say which tab is on: see
  [`battle-item-list-tab-index-static.md`](battle-item-list-tab-index-static.md).

## Steps

On the session's `message-log.txt`, 2026-11-23:

1. For each `compile` record, count the lines that open a body (`{` after a label), as `MessageLibrary.Parse` does.
2. List the labels of `ClassName.msg` and `CommandBattle_Message.msg` by position and add 1082 and 1932.
3. `awk` over the `resolve` and `cursor` records at 2215000–2220000 ms (Equipment), 2382000–2390000 ms (the tree) and
   2066000–2075000 ms (battle items).
