# Shop rows carry their price and stock; the rank-up notice is a UI part holding the follower and the new rank

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do (the
  message log of earlier play sessions, read only)
- **Sources:** [`shop-follower-inventory-2026-11-17.md`](../raw-sources/shop-follower-inventory-2026-11-17.md),
  [`chapter1-interaction-inventory-2026-11-20.md`](../raw-sources/chapter1-interaction-inventory-2026-11-20.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`shop-buy-sell-resolves-session-c-2026-11-22.md`](../raw-sources/shop-buy-sell-resolves-session-c-2026-11-22.md)

The #196 spike for the decided wording: shop rows `<item>, <price>, you have <n>`, `Buy how many? 1`,
`Total <price>, <money> left`, and the rank-up `<follower>, rank <n>` plus the unlock line. It builds on the
[money note](money-follower-rank-and-shop-static.md) (#405) and the [stock note](follower-availability-and-shop-stock-static.md)
(#494). The table is static; game session c's shop log is in its own section below. No logged session has passed a
rank-up yet.

## Fields per spoken line

| Field | Where | Confidence |
|---|---|---|
| Shop screen, cursor | `ItemShopChoice` (vtable `0x2200938`), focused row by `ChoiceGetCursor` (`0xd4ab90`, slot 11); the existing `screen` and `cursor` records already log it (203 and 200 records in session 5) | High |
| Row list | `ItemShopChoice +0x64d0`, a list of row records (count `+0x64d8`), rebuilt by the buy builder `0xe77370` from the item IDs at `+0x2d560` | High for the build, medium for the layout |
| Row's item | the row's item ID, from `+0x2d560` | High |
| Row's price | computed per row by `0xf31100(item, shop)`: the item's base value (`0x3bd100`) times the shop entry's rate (`0x51b0f0`), at least 1, less a percentage discount (`0x3d0b60`); stored in the row record | High for the route, medium for the record offset |
| Row's stock left | `0xf323c0(shop, item)`; 99 means unlimited and is not stored as a count | Medium |
| Row's owned count | the row takes a value from a thunk at `0xf2d350` that jumps outside analysed code; the readable route is the bag count (`GetBagItem`, `0x3bcb10`; category 0 at `G+0x1c758 + id`) | `[NEEDS VERIFICATION]` that the shown "Have" is this count |
| Row's item name | not fetched by the builder; the list layout draws it. Item names elsewhere resolve through `GetMessageBySerial` with labels `ItemName_<category>_<nn>` (consumables: serial 5142 + `nn` in the 2026-11-21 logs) | `[NEEDS VERIFICATION]` for the shop list |
| Buy-quantity widget | `ItemShopConfirmChoice` (vtable `0x2202ad0`), made by `0xe6e940` from the choice factory (`0xd541a0`). The constructor sets `+0x3f0`, `+0x3f4` and `+0x408` to `0xffff`, the byte `+0x3f8` to 1 and `+0x3fc` to −1 | Class high; which field is the quantity `[NEEDS VERIFICATION]` |
| Total | price × quantity on the confirm screen | `[NEEDS VERIFICATION]`: no field found |
| Money | `G+0x22c58` (`base+0x2cc9348`), `int`; `GetMoney` (`0x90a510`) | High (#405, session 5) |
| Rank-up notice | `app::ui::UUIC_Common_FollowerRankUp` (vtable `0x2252508`), kept in the universal UI controller's map (`[base+0x2ca3248] +0x188`, keyed by its type descriptor `0x2bbaff8`; looked up by `0x47f200`) | High |
| Rank-up follower, rank | its setter `0xf5ba80(part, &follower, rank)` stores the follower ID at `+0x30` and the rank at `+0x38`; `GetFollowerID_OnRankUpScript` (`0x907980`) and `GetFollowerRank_OnRankUpScript` (`0x9079b0`) read them back | High |
| Unlock line | the same setter fills a list of up to 58 archetype nodes and up to 5 item IDs and passes it to the part's layout (`0xd2f930` on `+0x28`); a rank below 1 takes another route (`0x392680`) | Route medium, text `[NEEDS VERIFICATION]` |
| Follower's name | `FollowerName_Message.msg` is compiled at boot in every logged session | `[NEEDS VERIFICATION]` which label the notice uses |

## Evidence

**Rows.** The buy builder `0xe77370` loops over the row count at `+0x2d568`. For each row it reads the `SELECT_STATE`
(`+0x2ed70`) and the item ID (`+0x2d560`), calls the price helper, `0xf2d1d0` (to `0x51b6c0` with the shop and
item), the out-of-code thunk and the stock helper, then pushes one record into `+0x64d0` through `0xe6def0`. The shop is
`+0x64c4` (the stock note). No message lookup happens in the builder, so the name comes from the layout's draw of the
record.

**Rank-up.** `0x47f200` callers are the four `app::evt::evtFollowerRankSequence1` to `4` steps (`vf3` at `0x47f620`,
`0x47fac0`, `0x47ff10`, `0x480370`), the driver `0x482db0` (called from `0x412c80`, itself only referenced from data)
and the two script getters. `0x480d40` creates the part (`0x158` bytes, constructor `0xf5a6c0`) when the map has none.
The driver `0x482db0` calls `0xf5ba80` with the follower and rank. The setter also calls `0x92ddf0(0xbb9)` unless the follower
is `0xf` at rank 1; what that does is `[NEEDS VERIFICATION]`.

**Logs.** The maintainer's four Debug session logs of 2026-11-21 and 2026-11-22 (1.1 million lines in the largest) have
no `ItemShop*` or `UUIC_Common_FollowerRankUp` record, so no shop and no rank-up was passed. They register
`FclShopGetItemID` at `rva=0x906720`, matching the script list for this build, and resolve 185 `ItemName_Consumable_*`
labels by serial.

## Session c, 2026-11-22: the Buy and Sell lists in a log

Same build. Game session c passed a weapon shop with checklist steps 1 to 5 (not 6), in the combined Debug dev build
with `ui-text-panes` and `inspector`. What its logs show is in
[`shop-buy-sell-resolves-session-c-2026-11-22.md`](../raw-sources/shop-buy-sell-resolves-session-c-2026-11-22.md); what
follows from it:

| Line part | What carries it | Confidence |
|---|---|---|
| Top menu names | `System_General_12`, `_13`, `_14` ("Buy", "Sell", "Appraise") and `Shop_Menu_03` ("Listen"), serials 1491–1493 and 1362, resolved in cursor order on every move; the menu's prompt line differs per index (`Shop_Other_32` to `_34`, none at index 3) | High for the labels and the order (four rows, four names, the order heard in #727); that index *n* is the *n*th name is by order only |
| Row name | a `GetMessageBySerial` resolve with flags 8, one per visible row, in row order: eight per view, with the name serial + 1206 (flags 7) after it | High: the Buy pane after confirm at index 3 resolved the view's fourth name |
| Row description | the name serial + 1206 for equipment, the `ItemText_` serial for consumables and valuables (not always the same label number) | High that it resolves per row; that it is the description `[NEEDS VERIFICATION]` (unlabelled in the log) |
| Weapon and armour names | serials 2447–3405, outside every file `MessageSerials` labels, so the log has no text; with descriptions at + 1206 they fit `Equip_Message_1.msg` and `_2.msg`, compiled at boot | `[NEEDS VERIFICATION]` which file and its first serial (the exe's range test isn't read) |
| Optional third serial | 4865–5092, flags 7, on some equipment rows, with the "Effect" label | `[NEEDS VERIFICATION]`: probably the effect line (`Equip_Message_3.msg`?) |
| 15 serials 9441–9458 | flags 7, with every weapon or gear view, after `MainMenu_Equip_02` ("Usable") resolved at the shop's open | `[NEEDS VERIFICATION]`: perhaps the archetype names for "usable by" |
| Price, owned count, ATK, HIT, DEF, EVA | no message: only the column labels resolve (`Shop_Other_03` "Have", `Shop_Other_17` to `_20`, `System_General_01` to `_04`). The numbers are drawn without a lookup, and the logged panes don't hold them | Labels high; values not in any logged record. Read them from the row record (`+0x64d0`) or the shop's helpers above |
| Scroll | eight rows per view; no move went past index 3, so how `top` changes on a scroll isn't seen | `[NEEDS VERIFICATION]` |
| Quantity, total | the Buy pane (after the first confirm) resolves `System_Window_02` and `_01` ("Buy", "Cancel") and `Shop_Other_01`; the quantity presses log no cursor, resolve or readable pane. "Total" (`Shop_Other_08`) resolves only once, as the shop opens | Not in the logs: needs step 3's `read` of the quantity widget |
| `ItemShopConfirmChoice` | a two-row cursor list that first logs *after* the quantity step is confirmed, not during it; its rows resolve nothing of their own | High. So the quantity widget is another object `[NEEDS VERIFICATION]`, which corrects the "Buy-quantity widget" row above |
| Sell tabs | not a cursor list. Sell opens on a tab and the shoulder key cycles six, resolved as `Item_Tab_03`, `_01`, `_06`, `_07`, `_08`, `_09` (Exchange, Use, Weapons, Armour, Gear, Accessories); each tab is an `ItemShopChoice` with its own `count` (6, 136, 74, 21, 32, 39) | High for the cycle; the tab names' order matching the keys is by order. #727's "6 (Sell categories)" was the first tab's row count |
| Sell rows | resolve once per tab, not on moves; names are not in serial order. The first tab had `count=6` but resolved five names | The sixth row's name `[NEEDS VERIFICATION]` |
| Sale | confirm resolves `System_Window_03`, `_01` and `Shop_Other_02`, no item serial; after the sale the tab has `count=5` at the same index, so the sold row left the list | High for the counts; which item was sold isn't logged |

So the mod can say each Buy and Sell row's name from the serials the view resolves, in row order, once it knows the
equipment files' serials. Price, owned count, stats, quantity and total have to come from memory. The inspector reads of
this session stopped at the shop scene, `fclSpotItemShopMenu` (`0x418797d510`, reached by `base+0x2ee7338 0x48 0x0 0x0
0x8 0x28 0x78 0x18 0x0`): of the pointers in its first 0x1000 bytes, the seven checked with `rtti` were not shop
choices.

## Capture checklist

Debug build, `pwsh scripts/dev/session.ps1 run -Seconds 3600 -OptIn ui-text-panes,inspector -SaveLog <outside the repo>`
([run a game session](../contributors/how-to/run-a-game-session.md)). Name each step aloud for the recording.

1. **Shop top menu** (chapter 1's peddler, about 1:27 in the chapter-1 inventory): open it, move Buy, Sell, Listen.
   Expect `ItemShopMenuChoice` `screen` and `cursor` records.
2. **Buy list:** move down three rows slowly, then change category once (LB/RB). The `pane` records name the panes that
   hold each row's name, price and count; note which serial or label the names resolve by. Run
   `docs/sessions/session-3/stop-3-shop.txt`'s `[shop-top]` and `[before-buy]` sections here.
3. **Quantity:** pick an affordable item, press up twice, down once. Panes for the count and the Total should change on
   each press; then `read <confirm>+0x3f0 0x20` before and after a press (the confirm from `rtti` on the shop scene's
   pointers, `ItemShopConfirmChoice@ope@ui@app`).
4. **Buy it**, then run `[after-buy]`: money drops by the total.
5. **Sell list:** open Sell, move two rows, sell one; run `[after-sell]`.
6. **Rank-up:** play to the next bond scene. Note the time the notice shows; the `pane` records then carry the follower
   name, the rank and the unlock line. If the inspector is up, `read` the part's `+0x30` and `+0x38`.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-22. Each item is one `pwsh scripts/dev/ghidra.ps1` query.

1. `vtable app::ui::ope::ItemShopConfirmChoice`; `global 0x2202ad0`; `decompile 0xe6e940`, `0xe7d930`, `0xe7cc20`;
   `callers 0xe6e940`; `callees 0xe78da0`.
2. `callees 0xe77370`; `decompile 0xe77370`, `0xf31100`, `0xf2d1d0`, `0xe745e0`, `0xe6def0`.
3. `decompile 0x907980`, `0x9079b0`, `0x47f200`, `0x4803f0`, `0x480d40`, `0x482db0`, `0x47f620`, `0x47fac0`,
   `0x480370`, `0x481270`, `0xf5ba80`; `callers 0x47f200`, `0x482db0`, `0x412c80`; `disasm 0x47f0b0`, `0x47f0c0`;
   `vtable app::ui::UUIC_Common_FollowerRankUp`.

Logs: `grep -c` for `ItemShop`, `screen=` class names and `resolve` labels over each session's `message-log.txt`,
counting only; no game text kept. Nothing outside analysed code was followed.
