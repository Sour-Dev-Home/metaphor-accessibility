# The date and time of day are two ints in the game-data block; quest state is a 135-record array beside them

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md),
  [`calendar-hud-inventory-2026-11-17.md`](../raw-sources/calendar-hud-inventory-2026-11-17.md),
  [`community-survey-calendar-quests-shops-followers-2026-11-19.md`](../raw-sources/community-survey-calendar-quests-shops-followers-2026-11-19.md),
  [`metaphor-010-templates-shop-follower-quest-2026-11-19.md`](../raw-sources/metaphor-010-templates-shop-follower-quest-2026-11-19.md)

Static prep (#494) for #191 and game session 3 (#498). Read in Ghidra only, not yet run against the game: every meaning
below is `[NEEDS VERIFICATION]` until the session's reads agree. `G` is the game-data block at `base+0x2ca66f0`
([money note](money-follower-rank-and-shop-static.md)). The calendar script getters read `G` directly; the deadline
and objective have no field of their own. Batch files: [`docs/sessions/session-3/`](../sessions/session-3/).

| Data item | Best lead | Confidence |
|---|---|---|
| Date | day count `G+0x311e8` (`base+0x2cd78d8`), `int`; month, day and year computed from it | High for the field and the formula |
| Weekday | `G+0x311ec` (`base+0x2cd78dc`), day count mod 5, five names | High: three samples match |
| Time of day | `G+0x311f0` (`base+0x2cd78e0`), `int`, index 0 to 6 of `System_General_Time_01`–`_07` | High; 3 afternoon and 6 late night matched |
| Quest state | `G+0x247b4 + id × 0x18` (`base+0x2ccaea4`), 135 records | High |
| Quest days left | computed from the record's start day and the quest table's limit and date | High in code |
| HUD deadline countdown | one accepted quest's days left | Low |
| Current objective | an accepted quest's progression (`+0x4`) and the quest's message hashes | Low; text readable as is `[NEEDS VERIFICATION]` |

## Evidence

### 1. Date

`GetCalendarDay` (`0x907840`) goes to `0x3b0390`, which returns the `int` at `G+0x311e8`; its only analysed writer is
`0x3b0400`. Everything else is computed from that count `d`:

- `GetMonth` (`0x90a520` to `0x72a2f0`) and `GetDay` (`0x907920` to `0x729ff0`): with `x = (d + 150) mod 365`,
  month is `x / 30 + 1`, capped at 12, and day is `x mod 30 + 1`; in month 12 the day is `x − 329`, so the last month
  has 35 days.
- `GetYear` (`0x90aa90` to `0x72ab30`): `(d + 150) / 365 + 1`. So `d = 0` is month 6, day 1, year 1.
- `SetDay` (`0x910450` to `0x72b210`) and `SetMonth` (`0x9104a0` to `0x72b320`) rebuild `d`
  through `0x3b0400`, and `SetDay` copies the old count to the calendar object's `+0x5c` first. The calendar object
  is `[base+0x2ca3318]`; the getters take it and ignore it.

**Weekday.** `GetCalendarDow` (`0x907850` to `0x72a140`) returns `d mod 5` (the divide by 5 is in the disassembly), and
`SetDay` stores the same value at `G+0x311ec` through `0x3b0410`. The week has five days, not seven: the value is the
weekday's index, `System_General_Week_01` + value (serial 9285 + value), in the Calendar grid's column order Flames,
Waters, Arbors, Metals, Idols Day (`src/Metaphor.Features.Menus/CalendarFocus.cs`, heard 2026-11-22). Three samples
agree: the 2026-11-23 live read (day 131, value 1, shown Watersday), and two save headers matched with the load list's
2026-11-22 read-outs, day 124 (value 4, Idols Day, serial 9289 resolved) and day 1 (value 1, Waters Day)
([save-slot note](save-slot-header-in-save-files.md), from #979). The 2026-11-17
inventory's "7 weekday names" is taken for a miscount.

### 2. Time of day

`GetCalendarTime` (`0x9078a0`) goes to `0x3b03e0`, which returns the `int` at `G+0x311f0`. `SetCalendarTime`
(`0x910430`) jumps into `.text1`, not followed; the only analysed write is the new-game reset (`0x3b2840`, which
clears `G`'s `0xac338` bytes and sets the time to 3). The scheduler (`0x72b630`) loads the script for the year and
month (`COMMON/scheduler/script/…`) and picks its entry by the time value from a name table at `0x27a7dc8`, then
copies the time to `G+0x711fc` (`0x3b0480`) once run. So the time is a small index, one entry per time of day; how
many and which is which comes from the session. `IsTimeProgressUI` (`0x90b2b0`) and `DateUI_ChangeTime`
(`0x904aa0`) reach `.text1`.

**Time index, matched 2026-11-24.** The index names `System_General_Time_01` + index (serial 9297 + index, seven
labels, so 0 to 6). Index 3 is afternoon (the autosave row's header held 3, serial 9300 resolved and "afternoon"
was shown) and 6 is late night (slot 4's header, shown late night); a new game starts at 3 (`0x3b2840`). The other
five indexes' names are the game's own label text, read at run time, and which time each is stays
`[NEEDS VERIFICATION]` by ear ([save-slot note](save-slot-header-in-save-files.md), section "Matched with the load
list", from #979).

### 3. Quests

`0x3b1340(id)` returns `G+0x247b4 + id × 0x18`. The script setters pass a state to `0x3cc750`, which stores it in the
record's first `u16`: `Quest_SetState_Obtained` (`0x90dcc0`) 1, `Quest_SetState_Accepted` (`0x90dc60`) 2,
`Quest_SetState_ReadyToDebrief` (`0x90dce0`) 3, `Quest_SetState_Completed` (`0x90dc80`) 4, `Quest_SetState_Expired`
(`0x90dca0`) 5. The record:

- `+0x0` state, `u16`, as above (0 before the quest is obtained, by the new-game clear).
- `+0x2` order number, `u16`: on obtain or complete, when still 0, set from counter `0xf4` plus one
  (`G+0x233f4 + 0xf4 × 4`, `CounterGet`'s array: `0x904a10` reaches `0x3b07e0`). The quest list's order is the lead.
- `+0x4` progression, `u16`: `Quest_GetProgression` (`0x90db40` to `0x3ca2a0`) returns it in state 2, else −1.
- `+0x8` start day, `int` (below). `+0x14` flags; obtain and complete set bit 2.

Loops in `0x3ca100` and `0x3cc750` run to 135 (`0x87`), so there are 135 records: `0xca8` bytes, one `read`.

**Days left.** `Quest_GetRemainDays` (`0x90db50` to `0x3ca2d0`) reads the quest table entry from `0x3ca570`, which is
`0x10 + id × 0xe0` into a loaded table: the 010 template's `QuestID` layout (a `0x10` header and 224-byte records). From
that entry's `u16`s `+0xa` (a day limit), `+0xc` (a month) and `+0xe` (a day) it takes the smaller of the record's
start day plus the limit, minus `d`, and the deadline date's count minus `d`, and 0 once the date is reached. The
date's count is `(m − 6) × 30 + day` for months 6 to 12 and `(m + 6) × 30 + day` for 1 to 5, so on the deadline day
itself the result is 1 `[NEEDS VERIFICATION]` and the year is ignored. A record in state 0 or 1 has no days left.
`Quest_GetNumSpecifiedDaysToLimit` (`0x90db10` to `0x3ca100`) counts quests in state 2 or 3 with at most N days left.

**HUD deadline.** No readable code draws it. The lead is that the main mission is one of the 135 quests in state 2 and
the countdown is its days left: the session reads every record and checks one. If none matches, the day-end diff
([day-advance note](day-advance-watches-static.md)) is the next place to look. Confidence: low.

**Objective.** No field holds text. Each quest has eight message hashes in its table entry (template); the objective
line is likely one of them chosen by progression (`+0x4`) `[NEEDS VERIFICATION]`. Whether the game's own line can be
read as is depends on whether it is drawn as text: with `ui-text-panes` on, the log shows any drawn text pane, so the
session checks for the HUD objective line there (count and timestamp only; no game text is kept). Confidence: low.

## Tonight

Stops 1, 2 and 5 of #498. Run each batch with
`pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-3/<file> -WaitKey -SaveLog <out>`, `<out>` a folder
outside the repository.

| Stop | Batch | What confirms |
|---|---|---|
| 1, town | `stop-1-town.txt` | `read base+0x2cd78d8 0x18`: with `d` the first `int`, `x = (d + 150) mod 365` gives the HUD's month (`x / 30 + 1`) and day (`x mod 30 + 1`); a mismatch rejects the formula. The third `int` is a small number; note it with the HUD's time of day. The second is `d mod 5`; note it with the weekday. The quest dump has states 0 to 5 only; for each state-2 record, the days-left formula with its table entry (or, short of that, a count that drops by one at stop 5) matches the HUD countdown for one of them |
| 2, Calendar and quests | `stop-2-calendar.txt` | Moving the Calendar cursor changes nothing in the day count (it is today, not the focus). The quest list's quests are the records in state 1 to 3, in `+0x2` order; the list's count matches. The log shows whether the HUD objective line and the quest details are drawn text panes |
| 5, day end | `stop-5-day-end.txt` | `changes`: the day count rises by 1 and the time falls back to the morning value; the quest records change only where a quest expired (state 5) or was advanced |

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says
otherwise. Script function addresses come from the registration list.

1. `decompile 0x907840`, `0x3b0390`, `0x907850`, `0x72a140` (and `disasm 0x72a140`: the divide by 5), `0x907920`,
   `0x729ff0`, `0x90a520`, `0x72a2f0`, `0x90aa90`, `0x72ab30`, `0x910450`, `0x72b210`, `0x9104a0`, `0x72b320`,
   `0x3b0400`, `0x3b0410`; `offset 0x311e8`, `offset 0x311ec`; `global 0x2ca3318`.
2. `decompile 0x9078a0`, `0x3b03e0`, `0x910430` (a jump into `.text1`, stopped there), `0x3b2840`, `0x72b630`,
   `0x3b03f0`, `0x3b0480`, `0x90b2b0`, `0x904aa0`; `offset 0x311f0`, `offset 0x311f8`.
3. `decompile 0x90dc60`, `0x90dc80`, `0x90dca0`, `0x90dcc0`, `0x90dce0`, `0x3cc750`, `0x3b1340`, `0x90db40`,
   `0x3ca2a0`, `0x90db50`, `0x3ca2d0`, `0x90db10`, `0x3ca100`, `0x3ca570`, `0x90db20`, `0x3ca250`, `0x904a10`,
   `0x3b07e0`, `0x3b39e0`; `callers 0x3ca2d0`, `callers 0x729ff0`, `callers 0x3b03e0`.

Nothing in `.text1` was decompiled: `SetCalendarTime`'s, `Quest_GetStatus`'s, `Quest_Daily_Process`'s and
`IsTimeProgressUI`'s targets were noted and not followed. Names and file paths are the exe's own; no game text is
quoted.
