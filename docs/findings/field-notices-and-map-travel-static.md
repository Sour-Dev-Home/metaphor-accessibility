# The loot banner, autosave label, new-entry notice and travel map have static leads, none yet confirmed in play

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`chapter1-interaction-inventory-2026-11-20.md`](../raw-sources/chapter1-interaction-inventory-2026-11-20.md),
  [`field-inventory-2026-11-17.md`](../raw-sources/field-inventory-2026-11-17.md),
  [`title-names-voice-tutorials-static.md`](title-names-voice-tutorials-static.md)
- **Functions:**
  - UUIC_Common_EventItemGet vtable | 0x2251f10 | .udata | Field | known only | The loot banner component; its list type is `ItemGetNotice`. | finding
  - UUIC_Common_Saving vtable | 0x2255948 | .udata | Field | known only | The autosave label component. | finding
  - ItemGetWindowEventOpen | 0x90b4b0 | .ecode | Field | known only | Script function that packs up to three item id and count pairs for the loot banner. | finding; docs/findings/battle-target-cursor-index-static.md
  - ItemGetWindowIsEnd | 0x90b660 | .ecode | Field | known only | Script function that waits for the loot banner to end. | finding; docs/findings/battle-target-cursor-index-static.md

Static prep for #542 to #545. Only the travel map has a route a release hook already sees: Gallica's "That's …" line
is a `MsgRequestByName` request. The other three go through UI components no release hook watches. The labels that
name them are fetched by serial, which only the Debug `GetMessageBySerial` listener logs. Every behaviour below is
`[NEEDS VERIFICATION]` until a session's log agrees; the checks that settle each are under [Steps](#steps).

| Notice | UI component (RTTI) | Text source | Seen by an existing hook |
|---|---|---|---|
| #542 loot banner | `UUIC_Common_EventItemGet@ui@app` (vtable `0x2251f10`), list type `ItemGetNotice@common@layout@ui@app` | Item IDs and counts; names likely from `Item_Message_1.msg` / `_2.msg` by serial | Debug only: `GetMessageBySerial`, if names go by serial |
| #543 autosave label | `UUIC_Common_Saving@ui@app` (vtable `0x2255948`); layouts `AutoSavingLayout`, `SavingLayout`, `SystemSavingLayout` | `System_Save_11` "Autosave" (serial 1300), or a picture | Debug only, if drawn from text |
| #544 new-entry notice | Unknown; `ItemGetNoticeLayout@ui@app` is a candidate | `System_Memorandum_GET` "Memorandum" (serial 1825); entry titles in `Memorandum_Title.msg` | Debug only, if drawn from text |
| #545 travel map | `UUIC_Common_SortieTownMap@ui@app`, `SortieTownMapLayout@ui@app`, `fclSortieTownMap@sortie@fcl@app` | Gallica's line `MES_Location_CurrentPoint` (`ui/Townmap.msg`); place names maybe `Big_Mapname.msg` | Release: `MsgRequestByName` for the line |

## Evidence

### Serials in `System_Message.msg` are the label's index plus `0x476`

The message-file table (`0x27b0cd0`, `0xd0`-byte entries, [static note](title-names-voice-tutorials-static.md)) names
`message/system/System_Message.msg` in entry 5, whose serial test at `+0x18` is `0x9f4c90`: it accepts `0x476` to
`0x78b` (1142 to 1931), 790 serials. The file as compiled holds exactly 790 labels. Numbering them from 1142 in file
order fits today's log: at difficulty select the `resolve` serials were 1888 to 1892, which number `System_mode_01` to
`_05`, and the difficulty list has five rows ([#540's note](difficulty-select-own-input-static.md)); at name entry
they were 1569 and 1574 (`System_NameEntry_02` and `_07`). So the labels below have these serials:

| Label | Serial | Text |
|---|---|---|
| `System_Save_04` / `_05` | 1293 / 1294 | "Saving..." / "Save complete." |
| `System_Save_11` | 1300 | "Autosave" |
| `System_Map_Travel_01` | 1750 | "Days" |
| `System_Result_04` | 1824 | "Items Acquired" |
| `System_Memorandum_GET` | 1825 | "Memorandum" |

Other tests in the same table: `Item_Message_1.msg` (entry 14, `0x9f4a90`) takes `0x1417`–`0x171b`, `Item_Message_2.msg`
(entry 15, `0x9f4aa0`) `0x171c`–`0x1a5c`, `Big_Mapname.msg` (entry 40, `0x9f4bb0`) `0x2776`–`0x278c`.
`Memorandum_Title.msg` (39), `Memorandum_Text.msg` (81), `ui/Townmap.msg` (51) and `ui/Narration_Happening.msg` (52) have
no serial test, so they aren't fetched by serial.

### #542: the loot banner is `UUIC_Common_EventItemGet`

The script function `ItemGetWindowEventOpen` (`0x90b4b0`) takes up to three item ID and count pairs, skips IDs 0 and
`0xffff`, packs them into `StaticArray<UUIC_Common_EventItemGet::Arg::GetItem,3>`, and passes them to `0xf560d0`. That
copies each pair into a `StaticArray<ItemGetNotice::ListInfoItem,5>`. `ItemGetWindowIsEnd` (`0x90b660`) is its wait.
`ItemGet` (`0x90b300`) and `ItemGetTreasure` (`0x90b3c0`) are thunks into `.text1` and weren't followed (ADR 0010).
Whether a field pick-up uses this window or the side notice `ItemGetNoticeLayout` is `[NEEDS VERIFICATION]`. So is
whether the name is looked up by serial. The "OK" is likely a button icon, not text.

### #543: the autosave label is `UUIC_Common_Saving`

`UUIC_Common_Saving`'s constructor is `0xf8a390`; its only analysed caller is its `vf0`, and a data reference at
`0x14cb2574` (outside analysed code) is the likely factory. `vf5` (`0xf8a6f0`) and `vf13` (`0xf8a840`) only start and
stop a layout animation at `+0x30`; no text call is in them. The plain string `Saving` (`0x2324f70`) has no analysed
reference. So the label may be a picture; if it is text, it is `System_Save_11` (serial 1300). The script functions
`FldInitStopAutoSaveOnFadeIn` (`0x8dfc10`) and `FldAutoSaveSetPosByPosID` (`0x8d78b0`) are field hooks around it.

### #544: the new-entry notice names `System_Memorandum_GET`

The string `System_Memorandum_GET` (`0x224e150`) sits in a run of `System_*` label names in `.udata`, followed by a
dword table; no analysed code or pointer refers to it, so the code that uses it is likely in `.text1`. The entry's own
title would come from `Memorandum_Title.msg` (273 labels `Memorandum_Message_Title_<n>`, compiled at boot). Its UI
component wasn't found; `ItemGetNoticeLayout` is the nearest name, as the inventory puts both notices at the left
(rows 5, 40 and 45).

### #545: the travel map asks for Gallica's line by name

`SortieMapUI_CurrentPointMessage` (`0x910e40`) is a thunk to `0x4e8ea0`, which calls
`MsgRequestByName(0, "MES_Location_CurrentPoint", …)`. In `ui/Townmap.msg` that label is Gallica's voiced line
"That's `<V 0>`.", the place name in a variable. `SortieMapUI_Open_SelectDestinationPoint` (`0x911e00`) passes its
arguments to `0xf36ae0`, which builds the map's arguments and hands them on (`0xfd8210`). Travel questions in the same
file (`MES_*_choice`) are likely `MsgRequestSelectByName` choices, which the mod already speaks
`[NEEDS VERIFICATION]`. Where the pin's place name and one-line description come from is `[NEEDS VERIFICATION]`;
`Big_Mapname.msg` (23 serials) is a lead. The large map and travel logged no `screen` record
(`field-inventory-2026-11-17.md`), and `SortieMenuChoice` there is the prep screen, not this map.

## Steps

Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1`): `vtable`, `decompile`, `callers` and `global` on the
addresses above. RTTI and string names: `grep -ao` over the exe; file offsets turned into RVAs from its section table
(Python 3.12, read only). Message text, serials and labels: the local `message-log.txt` of 2026-11-20 (title to the
opening scene), labels counted in file order (awk), checked against its `resolve` records.

What would settle each, with `GetMessageBySerial`, `ui-text-panes` and `create-msg-from-string` logging on (Debug
build), noting log times:

1. **#542:** pick up the first map item (inventory row 12). Look for a `resolve` serial in `0x1417`–`0x1a5c`, or 1824,
   and a `pane` record with the item's name while the banner shows; say whether the banner waits for OK.
2. **#543:** let an autosave happen (entering the base or a new area). Look for `resolve` serial 1300 (or 1293/1294)
   and a `pane` with the label; none means the label is a picture.
3. **#544:** trigger the first desert Memorandum notice (own capture row 21). Look for serial 1825, a `pane` with the
   entry's title, and any `compile` of `Memorandum_*.msg`; note how long it shows.
4. **#545:** open the travel map at the mines (inventory row 38) and move the pin. Check that `MES_Location_CurrentPoint`
   is a `request` and the reader says the place name; look for `Big_Mapname` serials (10102–10124) or `pane` text for
   the description; confirm that the travel question is a `select` record.
