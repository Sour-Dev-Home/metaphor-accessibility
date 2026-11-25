# Battle's Party screen is a classed list of character IDs, and Formation is one unit flag bit the mod can read

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do (the
  maintainer's local Debug logs, grep only)
- **Sources:** none, see Steps; builds on [`battle-order-menu-rows-static.md`](battle-order-menu-rows-static.md),
  [`battle-party-units-static.md`](battle-party-units-static.md),
  [`menu-values-party-unit-static.md`](menu-values-party-unit-static.md) and
  [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md) (rows 34 to 36, 82, 83)
- **Functions:**
  - FormationSet | 0xf03f20 | .ecode | Battle | known only | Sets or clears a character's back-row bit (unit `+0x0` bit 22) by character ID, for IDs 1 to 19. | finding
  - FormationGet | 0xf32ed0 | .ecode | Battle | known only | Returns a character's unit `+0x0` bit 22 by character ID. | finding
  - ChoiceFocusedValue | 0x4ad2c0 | .ecode | Menus | known only | Returns the value of a choice's focused row; on the Party screens a character ID. | finding
  - BTL_OrderPartyReserve formation lambda | 0xd8c8b0 | .ecode | Battle | known only | Button 10 on the screen: flips the focused member's row bit, then redraws the rows. | finding

For #258 and #259. The Order menu's Party row opens `BTL_OrderPartyBattleMember@ope@ui@app` (vtable `0x21cec28`),
whose cursor already reaches the mod with that class and a count of 4: registering it as a `Screen` is enough to get
its focus events. Its rows are character IDs; each member's name comes as a `CharaName_*` message fetched by serial
when the cursor first lands on it, and HP, MP and the Front or Back row come from the unit record the mod can already
find. Formation is unit `+0x0` bit 22: `FormationSet` (`0xf03f20`) writes it, `FormationGet` (`0xf32ed0`) reads it,
so a read-only watch of the four party slots' bits names every change. The swap's own commit was not traced.

## Evidence

- **The screens.** The exe's RTTI names `BTL_OrderPartyBattleMember` (`0x21cec28`), `BTL_OrderPartyReserve`
  (`0x21cf530`) and `BTL_OrderPartyChgSynthesis` (`0x21d0a70`), each a `SingleChoiceBase_WithLayout` keeping the shared
  `ChoiceGetCursor` (`0xd4ab90`, slot 11); and the layouts `CommandBattlePartyMainMemberSelectLayout` (`0x216e3d0`),
  `CommandBattlePartySubMemberSelectLayout` (`0x216e1e0`), `CommandBattlePartySubMemberSelectLineLayout` (`0x216e090`)
  and `CommandBattlePartySynthesisiLayout` (`0x216dea0`). Which layout belongs to which choice is by name only
  `[NEEDS VERIFICATION]`.
- **Classed in play.** The maintainer's 2026-11-23 evening session (Debug, Steam 18330018; local log, grep only), just
  after the transcript says "Orders, Party, 1 of 3": `cursor` records for one manager with
  `screen=BTL_OrderPartyBattleMember@ope@ui@app count=4` from 1 320 456 to 1 322 269 ms, moving over indexes 1, 2, 1,
  0, 1, 2. Nothing was spoken for them: no `Screen` names the class (`grep -rn OrderParty src` finds nothing).
  `BTL_OrderPartyReserve` never showed a cursor in any saved log `[NEEDS VERIFICATION]`.
- **Names by serial.** As the screen opened at index 1, the game fetched `CharaName_PC05_Junus` (serial 8, flags 9,
  "Junah"), and on the first landing on index 2 `CharaName_PC08_Basilio` (serial 16, flags 9). Index 0 fetched no
  `CharaName_*`, which fits the protagonist, whose name the player types `[NEEDS VERIFICATION]`. Each landing also
  fetched skill names and descriptions (flags 9 and 7), the focused member's Synthesis list on the right of the screen
  (inventory row 34). A second landing on a member fetched nothing new in the log; whether the game skips the fetch or
  the Debug log drops the repeat is `[NEEDS VERIFICATION]`.
- **Rows are character IDs.** The formation lambda `0xd8c8b0` (bound in `BTL_OrderPartyReserve::vf17`, `0xd93a70`, with
  button code 10) passes `ChoiceFocusedValue(screen)` (`0x4ad2c0`) to `FormationGet` and `FormationSet` as a character
  ID, then redraws through `0xd85230` and the layout at `+0x3d8` (`0xb15b40`). The unit record of character `n` is
  `G+0x228 + n × 0x16A8`, HP at `+0xc`, MP at `+0x10`, and the battle party's four IDs at `G+0x1c748`
  ([menu-values note](menu-values-party-unit-static.md)).
- **The Formation bit.** `0xf03f20(id*, bool)` returns at once unless `id − 1 < 0x13`, then calls `0x3e8a50` on the
  unit, which sets `0x400000` for true and clears it for false. `0xf32ed0` returns `0x3e8230(unit)`, the unit's bit 22.
  `0xf03f20`'s other callers: `0xf0daf0` and `0xf0fea0`, near the command panel (`UUIC_Battle_Command`), which set all
  19 characters' bits from a byte array at their object's `+0x430` (a restore, perhaps for Retry
  `[NEEDS VERIFICATION]`), and two camp-menu lambdas (`0xe17c70`, `0xe291b0`). The command wheel's D-pad Formation
  (inventory row 47) was not traced to a caller; that it ends in the same bit is `[NEEDS VERIFICATION]`. Which value
  means Back is a lead: `SetBattlePartyFormation` sets the bit for a script argument of 0.
- **The swap.** `BTL_OrderPartyReserve::vf17` binds three more handlers (lambdas `7be5f9da…`, `58d51594…`, `1bb403a7…`,
  with button codes for confirm, 4 and 5); none was followed, so where a swap writes the party is not known. Whether
  `G+0x1c748` changes during battle (it is copied back by `btl::phase::Finalize`, `0x229420`) is
  `[NEEDS VERIFICATION]`. The swap shows as an action-name banner (inventory row 36).

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23.

1. `vtable` on `app::ui::ope::BTL_OrderPartyBattleMember`, `BTL_OrderPartyReserve`, `BTL_OrderPartyChgSynthesis` and
   the four `app::ui::CommandBattleParty*` layouts; class names from `grep -ao ".?AV[...]"` over the exe.
2. `decompile` `0xd92280`, `0xd935f0`, `0xd8fe30`, `0xd93a70`; `callers 0x3e8a50`; `decompile 0x3e8a50`, `0xf03f20`,
   `0xf32ed0`, `0x3e8230`, `0x4ad2c0`, `0xd8c8b0`, `0xf0daf0`, `0xf0fea0`; `callers 0xf03f20`, `0xf0daf0`.
3. Local log: `grep -a "BTL_OrderPartyBattleMember" message-log.txt`, then the `resolve` lines between the first and
   last of those records; the transcript at the same times.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled; the thunks the screen's handlers call were not followed.

## Build plan

1. **Party screen (#258), no new hook.** `PartyScreenFocus` beside `BattleOrderFocus`: a
   `Screen("Party", "BTL_OrderPartyBattleMember@ope@ui@app", OutsideMenus: true)` in `KnownScreens.All`. A row says
   the member's name, HP, MP and row: "Junah, HP 120, MP 40, Back, 2 of 4". The name is the last `CharaName_*` fetched
   (flags 9) for that index, kept per index as `BattleListFocus` keeps rows; a row with none (index 0, the
   protagonist) is said by position until his typed name has a source. HP, MP and the bit come from a
   `PartyUnits` reader in the hook layer: slot `i`'s ID at `G+0x1c748 + 4 × i`, its unit at `G+0x228 + id × 0x16A8`,
   guarded by `1 ≤ id ≤ 19`; that the screen's index is the slot is checked in step 4.
2. **Formation (#259), no new hook.** On each cursor input on a `BTL_` screen and on each `LayoutAnimationPlay`
   record in battle (the command panel's `Command@*@panel@command@layout@ui@app` show 183 records each over the saved
   logs; whether the wheel's D-pad toggle plays one is `[NEEDS VERIFICATION]`), read the four party units' bit 22 and
   speak a change once: "Junah, Back row". A Testing
   setting until step 4 says which value is Back.
3. **Swap (#258).** Read the four party IDs as the Party screens close; a changed slot says "Hulkenberg in for
   Junah". If step 4 shows `G+0x1c748` doesn't change mid-battle, follow lambda `7be5f9da…` next.
4. **By ear, Debug.** Open Order, Party; move over all four members and say each one's name, HP and row; toggle one
   member's row (and once with the command wheel's D-pad); swap one member with the reserve. Compare each spoken line
   with the screen; open `BTL_OrderPartyReserve` and note its `cursor` records' count.
5. **Tests.** Fakes for the fetched names and the unit reader: a focus move, an unnamed protagonist row, a bit flip,
   a slot change, an ID out of range.
