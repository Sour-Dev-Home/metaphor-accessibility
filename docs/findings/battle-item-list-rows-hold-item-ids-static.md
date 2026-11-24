# Battle's item list rows hold item IDs at +0x23e8; the quantity each row shows is drawn from a bag count read in .text1

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [`battle-item-list-tab-index-static.md`](battle-item-list-tab-index-static.md)
  and [`money-follower-rank-and-shop-static.md`](money-follower-rank-and-shop-static.md)
- **Functions:**
  - BattleItemTabRows | 0x140d837d0 | .ecode | Battle | known only | Fills a `BTL_ItemSelect` tab's row list from a tab code, through a thunk into `.text1`.
  - BattleItemRowsBuild | 0x140d91e50 | .ecode | Battle | known only | `BTL_ItemSelect::vf14`: makes one `UiParts_ItemSelector` per row and hands it the row's item ID as `ItemID`.

For #937. A `BTL_ItemSelect` row is an 8-byte entry in the list at choice `+0x23e8` (count `+0x23f0`) whose first
`int` is the row's item ID. The rows are filled inside `.text1`, and each row's part is given only that ID, so the
number the row shows comes from somewhere the part looks up itself. The bag-count getter sends the battle consumables'
category (IDs from `0x4000`) into `.text1` too, so no analysed code gives the quantity: `[NEEDS VERIFICATION]` live.

## Evidence

- **The rows.** `BTL_ItemSelect::vf17` (`0xd932c0`) calls `0xd837d0(this, this + 0x23e8, tab)` for each tab, then
  walks the list at `+0x23e8` in steps of 8 bytes up to `+0x23f0`, comparing each entry's first `int` with the
  last-used item ID at `+0x4440`. `0xd837d0` turns the tab code (9 to 13 become 0x11 to 0x15, any other −1) into one
  argument of `thunk_FUN_14ebf4b10`, which is in `.text1` and was not followed.
- **The parts.** `vf14` (`0xd91e50`) fills the list the same way, then for each row up to `+0x23f0` allocates a
  `UiParts_ItemSelector` and sets its property named `ItemID` (string at `0x1417f38b0`, type `datItemID`) to the row's
  first `int`. The row's second `int` is not read there; what it holds is `[NEEDS VERIFICATION]`. `UiParts_ItemSelector`
  (vtable `0x2204310`) has four slots of its own, none of which calls the bag-count getter.
- **The count.** `callers 0x3bcb10` (the getter behind `GetBagItem`, which dispatches on `id >> 12`) lists the camp item
  menu (`fclCampItemMenu::vf8`), the battle results, quests, DLC checks and a boss, but no battle item list code. A
  battle consumable's ID of `0x4000` and up is category 4, which the getter sends into `.text1`
  ([bag counts](money-follower-rank-and-shop-static.md)).

So the mod could read a focused row's item ID from the choice, but not its quantity. Next, live: with the battle item
list open, read the choice's `+0x23e8` list in the Debug inspector and compare each entry's second `int` with the count
the row shows. If it matches, the quantity is that `int`; if not, call the count from a hook on the part's draw.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-24:

1. `vtable app::ui::ope::BTL_ItemSelect`; `decompile 0xd932c0`; `decompile 0xd837d0`.
2. `offset 0x23e8`; `offset 0x23f0`; `decompile 0xd84380`; `decompile 0xd91e50`.
3. `callers 0x3bcb10`; `vtable app::ui::ope::UiParts_ItemSelector`; `decompile 0xe92000`; `decompile 0xe92230`.

`.ecode` is `0x1000` to `0x17c0000` in the section table
([target cursor note](battle-target-cursor-index-static.md)). Nothing in `.text1`, `.xcode` or `.xtext` was decompiled;
`0x14ebf4b10` was noted and not followed.
