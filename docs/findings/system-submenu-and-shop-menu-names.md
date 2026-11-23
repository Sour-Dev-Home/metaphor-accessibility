# The System submenu's rows are System_Option labels 52, 53, 50, 49, 51, 54; the shop's top menu is four fetched labels

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`shop-buy-sell-resolves-session-c-2026-11-22.md`](../raw-sources/shop-buy-sell-resolves-session-c-2026-11-22.md);
  the maintainer's by-ear report on #733 and the session's message log (grep, serials and labels of short UI words only)

For #733, #736 and #197. Both menus fetch text by serial through `GetMessageBySerial`, which the mod logs as `resolve`.

## Evidence

- **System submenu** (`SystemMenuChoice`, 6 rows, session c, 194306 to 194321 ms). Opening it fetched six help lines
  (`flags=8`): `System_Option_58` ("Resume from saved data"), `_59` ("Record your progress"), `_56` ("Review
  knowledge"), `_55` ("The story so far..."), `_57` ("Change the game"), `_60` ("Rest well"), in that order. The
  maintainer heard the rows Load, Save, Tutorials, Story, Settings, Return to Title. Each help line fits its row, and
  each row's name label is its help line's label minus 6: `System_Option_52` ("Load"), `_53` ("Save"), `_50`
  ("Tutorial"), `_49` ("Story"), `_51` ("Config"), `_54` ("Return to Title"), as compiled in `System_Message.msg`. With
  `System_Option_55` at serial 1679, those are serials 1676, 1677, 1674, 1673, 1675, 1678. The rows' own names were not
  fetched in the log, so that the rows draw those labels is `[NEEDS VERIFICATION]`, and so is why the file says
  "Tutorial" and "Config" where the maintainer heard "Tutorials" and "Settings".
- **Shop top menu** (`ItemShopMenuChoice`, 4 rows): 1491, 1492, 1493 (`System_General_12` to `_14`) and 1362
  (`Shop_Menu_03`) resolve in cursor order: Buy, Sell, Appraise, Listen (the raw source, "Top menu").
- **Buy and Sell rows** (`ItemShopChoice`): each view resolves its eight row names with `flags=8` in row order just
  before the first cursor, so row n is the n-th name (the raw source). Item names are in `Item_Message_1.msg`; weapon
  and armour names (2447 to 3405) have no known file, so those rows stay by position. The Sell list's first tab had 6
  rows, and `Shop_Other_41` ("Sell All", 1412, `flags=8`) came just before its five item names, so "Sell All" is taken as
  row 0 `[NEEDS VERIFICATION]`. Price, owned count, ATK, HIT and quantity are not in any log.
- **Quest detail** (`CampQuestDetailChoice`, `count=9`, session c, 392385 ms, #194): opening a quest fetched serial 9979
  (`flags=7`) with no label, so its text (the description) is in no file the mod knows. The count is not the quest's.

## Steps

1. `grep -a` the session c message log for `screen=SystemMenuChoice` and read the `resolve` lines in the 30 ms before the
   first cursor; the labels come from the `System_Message.msg` compile dump in the same log.
2. The shop records are in the raw source linked above.
