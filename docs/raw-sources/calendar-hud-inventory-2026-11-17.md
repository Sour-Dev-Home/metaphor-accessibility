# Calendar, HUD date and objective: fields shown (issue #190, the rest)

- **Source:** capture evening 1 (#229), session 5: the same run, recording and method as
  `field-inventory-2026-11-17.md` (Debug mod build `219282c`, OBS recording read with ffmpeg 9.0.2 frames and the
  `watch` skill's WhisperX narration, `message-log.txt` counted by record kind). Only labels, class names, counts and
  timestamps are kept; no game text. Video time = log time − 51 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

The maintainer read the HUD aloud in a town (18:52) and in a dungeon, opened the Calendar from the main menu (30:25)
and the quest screens from the large map (19:16–21:07).

## Results

| Screen | Class | Seen | Fields shown | What changes |
|---|---|---|---|---|
| HUD date and weather | none | 15:15 onward, towns and the base only | Month/day, weekday, time of day, temperature in °C with a weather icon. | Advances with time of day; absent in dungeons. |
| HUD deadline countdown | none | throughout the field | Mission label; "Days Until" and the goal; the number of days. | The number drops by day. |
| HUD next objective | none | throughout the field | One objective line under the countdown. | Changed between saves (two different lines seen, 15:15 and 33:25). |
| Calendar | `CalendarChoice` (1 `screen`, 0 `cursor` records) | 30:25 | Title and prompt; month number with LB/RB; a grid of weeks with 7 weekday names; Today marker; icons on days with events or deadlines; right pane: the focused date and weekday, an Events list and a Special Discounts list. Travellers' Voices, B Back. | Focused day changes the right pane. The cursor moves logged no `cursor` record, so this screen's cursor is not a `CursorManager` one. |
| Quest list and details | `CampQuestChoice`, `CampQuestDetailChoice` | 19:16 | See `field-inventory-2026-11-17.md` (large map and travel). | — |
| Large map weather strip | none | 20:20 | Per location, Day 1 to Day 3 weather icons. | — |

The game compiled `Calendar_Message.msg` when the Calendar opened (30:25).

## Not seen

The game's own "next objective" prompt beyond the HUD line, and a day-advance announcement: no day changed in this run.
