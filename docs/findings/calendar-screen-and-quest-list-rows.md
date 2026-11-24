# The Calendar is a 5-column day grid; the quest list is every quest whose status isn't 0 or 6

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`grid-cursor-fields-static-2026-11-21.md`](../raw-sources/grid-cursor-fields-static-2026-11-21.md),
  [`calendar-hud-inventory-2026-11-17.md`](../raw-sources/calendar-hud-inventory-2026-11-17.md),
  [`metaphor-010-templates-shop-follower-quest-2026-11-19.md`](../raw-sources/metaphor-010-templates-shop-follower-quest-2026-11-19.md);
  adds to [`calendar-date-time-and-quests-static.md`](calendar-date-time-and-quests-static.md) and to #665's
  main-mission table note
- **Functions:**
  - app::ui::CalendarLayout vtable | 0x2172fe0 | .udata | Menus | known only | The Calendar's layout class; its slot 17 draws text.
  - CampQuestChoice vtable | 0x21e1150 | .udata | Menus | known only | The quest list's choice, with the layout `app::ui::CampQuestLayout`.
  - CampQuestChoice::vf16 | 0xde9520 | .ecode | Menus | known only | Fills the quest list rows through a helper shared with two other callers.

Spike #191 for #193 (Calendar) and #194 (quest list). The Calendar builds a `MultiLineCursorManager` grid of 5 columns
and 6 rows (7 in month 12), so the cursor index is the day of the month minus one and the column picks the weekday
name. No hook seen so far logs a day's events or discounts: their text is in `Calendar_Message.msg`, read by label or
hash. The quest list's rows are the quest ids whose status is neither 0 nor 6, in id order; each row's text getters
reach the quest table, but which field is the name, deadline or description is open.

| Item | Where | Status |
|---|---|---|
| Calendar grid | `CalendarChoice` makes a `MultiLineCursorManager`, 5 columns, 6 or 7 rows | Pinned (static and log) |
| Focused day | grid index + 1 (cursor `index`, `count=30`) | Pinned in the log; the 35-day month 12 is static only |
| Weekday name | serial `9285 + column` (5 names) | Pinned in the log; their text `[NEEDS VERIFICATION]` |
| Month shown | `CalendarChoice+0x980`, `int` (12 means 7 rows) | Static; the run-time value `[NEEDS VERIFICATION]` |
| Today's cell | `CalendarChoice+0x984`, `+0x988`, passed to the grid on build | `[NEEDS VERIFICATION]` |
| Events, discounts | `Calendar_Message_Event_*`, `_Sale_*`, `_Holiday_*`, `_Scenario_*` labels | Labels pinned; how a day picks them `[NEEDS VERIFICATION]` |
| Quest rows | ids 0–134 with status not 0 and not 6, in id order | Static; matches against the list `[NEEDS VERIFICATION]` |
| Quest deadline | `0xf31460`: days left, or none when below 0 | Static; which row shows it `[NEEDS VERIFICATION]` |
| Quest name, description | the quest table entry, through `0x3ca780`, `0x3ca910`, `0x3caa50` | `[NEEDS VERIFICATION]` |

## Evidence

### Calendar layout

- `CalendarChoice` has two vtables, `0x21d4228` and `0x21d4470`, and its layout class is `app::ui::CalendarLayout`
  (vtable `0x2172fe0`; own slots 0, 1, 16, 17, 18, 20, 36, 38, 39). The layout's slot 17 (`0xb2d090`) draws text
  with the `font_rect_C4_screendescription` font rect; most of its calls are thunks into the protected sections, not
  followed.
- Slot 9 (`0xda1690`) allocates `0x100` bytes, builds them with `0xe9be50`, and wraps them in a
  `shared_ptr<MultiLineCursorManager>`.
- Slot 16 (`0xda1b80`) sets rows to `6 + (month == 12)`, with the month the `int` at `+0x980`, and calls
  `0xe9cfa0(manager, 5, rows, rows × 5, −1)`. It then adds one cell per grid position, enabled when the position is
  below 30 (`0x1e`) or the month is 12, and ends with `0xe9cf70(manager, [+0x984], [+0x988], 0)`, the start position.
- Slot 17 (`0xda2120`) binds two input handlers only when the month is not 12 (input ids 10 and `0x2b`); that these are
  the month buttons is `[NEEDS VERIFICATION]`.

**The 2026-11-22 walk** (maintainer, Debug build, log kept locally; grid and cursor records from the mod's
`MultiLineCursorManager` hook, #589):

- On opening, the grid reported `count=30 columns=5`, at `index=11` (`column=1 line=2`). Command `0x14` added 5 to the
  index, `0x15` took 5 away, `0x16` added 1: rows of 5. The walk reached 17 cells, from index 2 to 28, none above 29.
- Each `resolve` of serials 9285 to 9289 during the walk came on a move to a cell whose column was that serial minus
  9285, through columns 0 to 4. A move within one column resolved nothing, so the game resolves the weekday only on a
  change. The text of 9285 to 9289 is not logged (`label=(unknown)`). The 5 columns fit `GetCalendarDow`'s `d mod 5`,
  but the 2026-11-17 inventory saw 7 weekday names: `[NEEDS VERIFICATION]` which is right.
- Opening also resolved `System_Calendar_01` to `_05` (the prompt, the Events and Special Discounts headings and the day
  and night markers), serials 9290 to 9294 (5 names, the column headings by the lead) and 9306.
- The game compiled `EN/message/system/Calendar_Message.msg` as the Calendar opened, with 37 labels:
  `Calendar_Message_Event_N` (7), `_Event_N_N` (11), `_Sale_N` (9), `_Scenario_N` (6), `_Scenario_N_N` (3) and
  `_Holiday_N` (1). No `request`, `lookup` or `resolve` record during the walk named one of them, so the day panel
  gets its text by another path. The `pane` records' text pointers didn't decode.

### Quest list

- `CampQuestChoice` (vtable `0x21e1150`) has the layout `app::ui::CampQuestLayout` (`0x2184a30`, `0x2184b80`), and
  `CampQuestDetailChoice` (`0x21e1a58`) the layout `app::ui::CampQuestDetailLayout` (`0x21848e0`). The list uses the
  shared cursor slot 9 (`0xd49f00`): the log's `class=0x22045e8`.
- `0xf31210`, called by `CampQuestChoice::vf16` (`0xde9520`) and by `0xde8d10` and `0xde8e10`, fills a
  `fw::StaticArray<datQuestID,135>` with every id from 0 to `0x86` whose status is neither 0 nor 6, in id order. The
  status comes from a thunk into `.text1` (`0x443d65a0`), not followed: that it returns the record's `+0x0` state,
  and what 6 means (no setter writes it), is `[NEEDS VERIFICATION]`. `vf16` then checks each listed quest's table flag
  (`+0x10 & 0x20`, `0x3cb460`) only to set the layout's `IsTutorial` property while flag `0x39f3` is off.
- Days left: `0xf31460` calls `Quest_GetRemainDays`'s body (`0x3ca2d0`) and returns it with a "has value" byte, false
  when below 0. Its callers are `0xb8b190` and `0xb8f0d0` (both in the `CampQuestLayout` range) and `0xd2acd0`.
- Row text: `0xb8b190` (921 lines) calls quest getters that read the table entry (`0x3ca570`): `0xf316b0` (u16 at
  `+0x40 + 2i`), `0xf316e0` (`+0x48 + 2i`), `0xf32450` (`+0x30 + 2i`), `0xf32510` (`+0x6`), `0xf320f0` (`+0x28`, 8
  bytes), `0xf321a0` (`+0x1c`, u16), and three times each through `0x3ca780`, `0x3ca910` and `0x3caa50`. Those three
  branch on a quest kind from `0x3c9a80` (0, 1 or 2) and read either table fields (`+0x34 + 2i`, `+0x38`, `+0x3c`,
  `+0x1e`), the quest record (`+0xc + 4i`) or a thunk into the protected sections with `[0x2dff850]`, next to the
  mission table's `[0x2dff848]`. They return ids, not hashes: which is the name, the client or the description is
  `[NEEDS VERIFICATION]`. The table entry's eight message hashes (template) aren't read by any of these.
- In the walk, `CampQuestChoice` opened twice with `count=4` and one move (0 to 3), then closed: the rows weren't
  walked. `count=4` is likely the category tabs `[NEEDS VERIFICATION]`. Opening resolved 13 `System_Quest_list_`
  labels (`01` to `16`), 5 `System_QUEST_` labels (`12` to `24`) and serials 9567 and 9606 to 9608. No quest name was
  resolved by serial; the game compiled `EN/message/quest/Quest_1.msg` and `Quest_2.msg` at start-up. `0xf31210`
  doesn't sort by the record's `+0x2` order number; a sort elsewhere is `[NEEDS VERIFICATION]`.

### Wording for #193 and #194

- Calendar, `<weekday> <date>, <events>, <discounts>`: weekday from `9285 + column`, date from the shown month and
  `index + 1`; events and discounts wait on the day panel's path, so a first cut says the weekday and date only.
- Quests, `<quest>, <status>, <deadline if any>, n of total`: total and order from `0xf31210`'s list, status from the
  record's `+0x0`, deadline from `0xf31460`'s "has value"; the name and the details key's description wait on the
  getter question above.

## What the next session must check

With the Debug build, `ui-text-panes` on, and stop 2 of `docs/sessions/session-3/stop-2-calendar.txt`:

1. Calendar, today: note the HUD date and weekday, then `read base+0x2cd78d8 0x18`. Check that the grid's open `index`
   is the day minus one and that `9285 + column` is the HUD weekday's serial.
2. Calendar, LB and RB: the grid's `count` stays 30, and 35 in month 12. Say aloud which of 5 or 7 weekday headings
   the screen shows.
3. Calendar, a day with an event and one with a discount (the icons): note the cell index and say each panel line
   aloud; check for any `pane` or `request` record at that moment. Without one, the next lead is the layout's slot 17
   (`0xb2d090`) and the `Calendar_Message` labels by MD5.
4. Quest list: walk every row of every tab, saying each row's name, status and deadline aloud with its index. Then
   `read base+0x2ccaea4 0xca8`: the listed rows should be the ids with state not 0, in id order.
5. Quest details: open one row and say the description aloud; check for a text pane or `request` record.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[how-to](../contributors/how-to/query-the-ghidra-project.md)), on the exe of the build above, 2026-11-22:

1. `vtable app::ui::ope::CalendarChoice`, `vtable app::ui::CalendarLayout`; `decompile 0xda1690`, `0xda1b80`,
   `0xda2120`, `0xb2d090`.
2. `vtable app::ui::ope::CampQuestChoice`, `vtable app::ui::ope::CampQuestDetailChoice`,
   `vtable app::ui::CampQuestLayout`, `vtable app::ui::CampQuestDetailLayout`; `callers 0x3ca570`, `0x3ca2d0`,
   `0xf31460`, `0xf31210`; `decompile 0xde9520`, `0xf31210`, `0x3cb460`, `0xf31460`, `0xb8b190`, `0xf316b0`,
   `0xf316e0`, `0xf320f0`, `0xf321a0`, `0xf32450`, `0xf32510`, `0x3ca780`, `0x3ca910`, `0x3caa50`.
3. The maintainer's session of 2026-11-22 (Debug build from `main`, Reloaded-II): the Calendar opened from the main
   menu and walked across all 30 cells, then the quest list opened from the map. From the mod's message log (kept
   outside the repository), the `grid`, `cursor` and `resolve` records between the `Calendar_Message.msg` compile and
   the next `RootMenuChoice` record, and the records around each `CampQuestChoice` record. No game text is quoted
   here beyond message labels.
