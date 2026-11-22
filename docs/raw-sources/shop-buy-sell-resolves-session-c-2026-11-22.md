# Shop Buy and Sell: which serials resolve per view, from game session c (#197, #736)

- **Source:** the mod's `message-log.txt`, its report-log extract and the inspector log from the maintainer's game session
  c on 2026-11-22 (combined Debug dev build, opt-ins `ui-text-panes`, `create-msg-from-string`, `tutorial-window` and
  `inspector`; a weapon shop), re-read with grep and awk. The logs hold game text, so they stay local; only serials,
  flags, labels of short UI words, class names, indices, counts, buttons and times are kept.
- **Saved:** 2026-11-22.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -n ItemShop` in the report log for the `cursor` records.
2. In the message log, the records from 2050000 to 2292000 ms (the two logs' ms share a clock) cut out with awk, then the
   `resolve`, `cursor` and non-idle `input pad0` records printed in order as time, serial, flags and label, grouped by
   millisecond. `pane` records counted by address and their `text=` fields checked for readable strings.
3. The inspector log read whole (38 lines).

## What it shows

Times are milliseconds since the game started. Buttons are as the log names them: `0x4000` confirm, `0x2000` cancel,
`0x0010` up, `0x0040` down, `0x0020` and `0x0080` the two sideways moves, `0x0800` the next-tab shoulder.

- **Top menu** (`ItemShopMenuChoice`, `count=4`). On every move, 1491, 1492, 1493 (`System_General_12` to `_14`,
  flags 10) and 1362 (`Shop_Menu_03`, flags 10) resolve in that order: Buy, Sell, Appraise, Listen. Each index also
  resolves one prompt line, flags 8: index 0 1403 (`Shop_Other_32`), index 1 1404 (`_33`), index 2 1405 (`_34`); index 3
  resolves none. At the shop's open (2055006 ms), before the first cursor, `Shop_Other_08` ("Total", 1384),
  `Shop_Other_46` (1417) and `System_Choice_OK` (1289) resolve.
- **Buy list** (`ItemShopChoice`, `count=37`, 2144621 ms). Just before the first cursor: `Shop_Other_03` ("Have", 1382,
  flags 6), `Shop_Other_17` and `_18` ("ATK", "HIT", flags 8), 15 serials 9441–9458 without 9451, 9454 and 9456 (flags
  7, `label=(unknown)`), then eight rows. Each row is a name serial with flags 8, an optional serial from 4865–5092
  (flags 7) and the name serial + 1206 (flags 7). `System_General_01` and `_02` ("ATK", "HIT", flags 7) follow the first
  name and `System_General_10` ("Effect", flags 7) the second; the logger drops a serial repeated within 2 s
  ([`name-lookup-candidates-2026-11-16.md`](name-lookup-candidates-2026-11-16.md)), so later rows' copies would not show.
  The eight names: 2453, 2454, 2486, 2487, 2488, 2489, 2512, 2513, all `label=(unknown)`. Moves to index 1 (2151898)
  and 3 (2154793) resolve the top menu's four names, its prompt and the same set again in the same order; the move to
  index 2, 1.7 s after the one to index 1, logs none, as the 2 s rule predicts. No move went past index 3, and no tab key
  was pressed in the Buy list.
- **Buy pane.** Confirm at index 3 (2190714 ms) resolves `System_Window_02` ("Buy", 1431), `System_Window_01` ("Cancel",
  1430), `Shop_Other_01` ("Buy", 1380), then 2487 with "ATK", "HIT", "Effect", 4866 and 3693: the fourth name of the
  view, with its optional serial and its + 1206.
- **Quantity.** Up at 2193799 and 2194204 ms, down at 2203004 ms: no `cursor`, no `resolve`. In the 400 ms after each
  press, the only `pane` records are the two addresses logged most often across the whole cut (30637 records each), with
  unreadable `text=`. No `pane` `text=` in the cut holds a price, a count or a
  shop word; the few readable ones are three-letter fragments.
- **Confirm** (`ItemShopConfirmChoice`, `count=2`). Its first cursor is at 2214607 ms, after the second confirm, index
  0. Cancel (2217555), down (2218725) and confirm (2220685) bring it back at index 0. Sideways moves to index 1 and 0
  (2223676, 2225796) each re-resolve the whole Buy view as above, nothing for the confirm's own rows. Confirm at index 0
  (2227485) returns to the Buy list at index 3, `count=37`.
- **Sell list.** Sell from the top menu (2235517) resolves `Shop_Other_40` (1411, flags 6) and `Shop_Other_41` (1412),
  then five name/description pairs from `Item_Message_1` and `_2` (`ItemName_CASH_20`, `_28`, `_30`, `_33`, `_38`, each
  with an `ItemText_CASH_` serial), then the six tabs, flags 2, in this order: `Item_Tab_03`, `_01`, `_06`, `_07`, `_08`,
  `_09` (Exchange, Use, Weapons, Armour, Gear, Accessories). Its first cursor has `count=6`. Each tab key (2239396 to
  2249076) logs a cursor at index 0 with `count` 136, 74, 21, 32, 39, then 6 again. The first five keys resolve eight
  name serials each: 5145–5155 with `ItemName_Consumable_` labels; 2447–2475 with ATK/HIT; 2901–3012 with
  `Shop_Other_19` and `_20` and `System_General_03` and `_04` (DEF, EVA); 3095–3157; 3305–3405 with `System_General_16`
  ("+Skill"). Each name is followed by its + 1206 (or its `ItemText_` serial for consumables), some by a 4865–5092
  serial. The Weapons tab's names are not in serial order (2447, 2456, 2450, 2451, 2458, 2463, 2453, 2475). The sixth key
  (back to the first tab) resolves no row.
- **Sell moves and the sale.** Down to index 1 and 2 (2252072, 2253037) resolve nothing. Confirm (2262432) resolves
  `System_Window_03` ("Sell", 1432), `System_Window_01` and `Shop_Other_02` ("Sell", 1381), no item serial. A second
  confirm (2264567) opens `ItemShopConfirmChoice` at index 0; a third (2271442) returns to the list at index 2 with
  `count=5`.
- **Inspector** (16:39–16:40 local, at the top menu). The chain `base+0x2ee7338 0x48 0x0 0x0 0x8 0x28 0x78 0x18 0x0`
  gave `0x418797d510`, `rtti` `fclSpotItemShopMenu@fcl@app`; a `read` of 0x1000 bytes from it was taken. The first two
  runs failed on the typo `0x80x40` and an unreadable step. No read reached `ItemShopChoice` or
  `ItemShopConfirmChoice`.
