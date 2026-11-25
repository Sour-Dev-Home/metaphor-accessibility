# The save and load slot lists are named by the prompt each opening fetches by serial

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do; opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps
- **Functions:**
  - SaveLoadLayoutBase vtable | 0x21b6da0 | .udata | Menus | known only | The save and load slot lists' base layout class. | finding; docs/findings/battle-target-cursor-index-static.md

For #622. The slot lists' cursors come with no screen class (`screen=?`): no choice hands out their manager. Each
opening fetches its prompt by serial about 10 ms before its new manager's first cursor: serial 1634 (`System_Option_12`,
"Read the records so far?") for the load list, 17 items; serial 1299 (`System_Save_10`, "Record your actions thus
far?") for the save list, 16 items. `SlotScreens` (`src/Metaphor.Core/Menus/SlotScreens.cs`) names the list from that.

## Evidence

- **Two Debug sessions (2026-11-21 and 2026-11-22, local, not committed).** Every `class=0x22045e8 screen=?` manager
  with 17 items (8 and 4 openings) followed a 1634 resolve, and every one with 16 items (3 openings) a 1299 resolve.
  Those two serials resolved nowhere else. The only other `screen=?` lists of that class held 1 and 29 items.
- **Which row is which.** The load list has one row more than the save list, and `System_Save_11` ("Autosave")
  resolves as the load list opens, so its first row is taken for the autosave `[NEEDS VERIFICATION by ear]`. The save
  list opened once at index 3.
- **What isn't readable.** Moving resolves nothing per slot. The list fetches `System_General.msg` serials (9270–9308,
  [field-map-region-area-name-static.md](field-map-region-area-name-static.md)) as rows scroll into view: by position
  in that session's compiled file, 9285–9289 are `System_General_Week_01`–`_05` (weekdays) and 9297–9303 are
  `System_General_Time_01`–`_07` (times of day), so these are each save's in-game date. The Debug log drops repeats, so which row owns which
  is not pinned. No place-name lookup, playtime or empty-slot record shows; pane text on these screens is unreadable.
- **Classes in the exe.** RTTI names `SaveLoadLayoutBase` (vtable `0x21b6da0`), `LoadSlotSelectLayout`,
  `SaveSlotSelectLayout` and their `Camp` and `CampInstant` variants. Which owns the list's manager is
  `[NEEDS VERIFICATION]`; a hook on its update, as for the difficulty list, would name it without the prompt.

## Steps

1. Debug build with the `GetMessageBySerial` and menu cursor hooks; open Load from the title screen and from the quick
   menu, and Save from the main menu's system list.
2. In `message-log.txt`, pair each `screen=? count=16|17` manager's first `cursor` with the `resolve ... flags=7` just
   before it, and count serials 1634 and 1299 over the whole log.
3. Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1 vtable app::ui::SaveLoadLayoutBase`); `grep -ao` over the
   exe for the RTTI names.
