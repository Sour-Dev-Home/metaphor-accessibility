# The battle Order menu's three rows are COMMAND_ORDER_IDs 0 to 2, named by message serials 2068 to 2070

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; the screen class and its count of 3 are from #771's by-ear session (2026-11-22, "c")
- **Functions:**
  - BTL_OrderTop constructor | 0x140d75820 | .ecode | Battle | known only | Sets `BTL_OrderTop`'s vtables and empties its three arrays: row items, order IDs and flags, each of capacity 3. | finding
  - BTL_OrderTop::vf14 | 0x140d92550 | .ecode | Battle | known only | Rebuilds the row items from the order-ID array, one row per ID, in array order. | finding
  - OrderRowName | 0x140b15af0 | .ecode | Battle | known only | Fetches a row's name by serial: order ID 0, 1, 2 to serial 0x814, 0x815, 0x816. | finding
  - OrderRowDraw | 0x140b1bea0 | .ecode | Battle | known only | Sets a drawn row's text from the same serials, and its icon (0x1412, 0x1413, 0x1411). | finding

For #771. `BTL_OrderTop@ope@ui@app` (vtable `0x21ccb30`) is a `SingleChoiceBase_WithLayout<CommandBattleOrderMenuLayout>`
whose rows are `COMMAND_ORDER_ID`s. Its layout names each row with `GetMessageBySerial` at serial `0x814 + id`
(2068, 2069, 2070), so a focus class can say a row by its ID with `MessageSerials.TextOf`, as `SystemMenuFocus` does.
The three words those serials hold are not known statically `[NEEDS VERIFICATION]`.

## Evidence

- **The object.** `0xd524e0` allocates `0x450` bytes and calls the constructor `0xd75820`, which writes
  `BTL_OrderTop::vftable` at `+0x0` and `+0x8`, then three fixed arrays (vtable, count, data): row items
  `StaticArray<CommandBattleOrderMenuLayout::ListInfoItem,3>` at `+0x3e8` (count `+0x3f0`, items of 8 bytes from
  `+0x3f8`), `StaticArray<COMMAND_ORDER_ID,3>` at `+0x418` (count `+0x420`, `u32` IDs from `+0x428`) and
  `StaticArray<char,3>` at `+0x438`.
- **Rows from IDs.** `vf14` (`0xd92550`) empties the row items, then for each order ID in `+0x428` up to the count at
  `+0x420` appends `{ id, 1 }`. So row `i` is the ID at `+0x428 + 4 × i` `[NEEDS VERIFICATION: that the cursor index
  is the row's place in that array]`. Who fills the ID array wasn't found in analysed code.
- **Names by serial.** Both callers of `GetMessageBySerial` in the layout's code pick the serial the same way: 0x814
  for ID 0 (and any ID other than 1 or 2), 0x815 for 1, 0x816 for 2. `0xb1bea0` also sets an icon per ID.
- **The cursor.** `BTL_OrderTop` keeps the shared `ChoiceGetCursor` (`0xd4ab90`, slot 11), which hands out the manager
  held at choice `+0x230`, so the cursor records already seen for it (count 3) come through the usual path.
- **Sub-screens.** The exe's RTTI names `BTL_OrderMove`, `BTL_OrderTactics`, `BTL_OrderPartyBattleMember`,
  `BTL_OrderPartyReserve` and `BTL_OrderPartyChgSynthesis` beside it. Which row opens which is not traced.

## Live check

In a Debug session, open the Order menu in a battle and move over all three rows. The message log should hold
`resolve via=GetMessageBySerial` records for serials 2068, 2069 and 2070 as the menu opens, with their labels; the
rows' words are what the maintainer hears drawn. No inspector line is needed: the choice has no static path.

## What a feature needs

`OrderMenuFocus` beside `SystemMenuFocus`: a `Screen("Order", "BTL_OrderTop@ope@ui@app")` in `KnownScreens.All`, the
row's name as `serials.TextOf(2068 + id)`, then "n of 3". The ID comes from the owner (`CursorOwners.OwnerOf`) at
`+0x428 + 4 × index`, guarded by the count at `+0x420` being at most 3; if unreadable, `2068 + index` is the fallback.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23.

1. `vtable app::ui::ope::BTL_OrderTop`, `app::ui::CommandBattleOrderMenuLayout`.
2. `decompile` `0xd974a0`, `0xd7f1d0`, `0xd92550`, `0xd90090`, `0xd93f70`, `0xd4ab90`; `global 0x21ccb30`;
   `decompile 0xd75820`, `callers 0xd75820`, `decompile 0xd524e0`.
3. `callers GetMessageBySerial`: the two in `0xb15af0` and `0xb1bea0`; `decompile` both.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled. The layout's `vf17` (`0xb168f0`) reads values from `.text1`
(obfuscated constants) and was not followed.
