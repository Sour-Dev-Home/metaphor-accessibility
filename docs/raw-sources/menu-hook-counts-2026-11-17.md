# In-game menu screens: what the hooks logged (issue #154)

- **Source:** one run on the maintainer's machine through Reloaded-II 1.31.0, mod build `3dc9b59` (`hooks 23/23`),
  with `opt-in-ui-text-panes.txt`, `opt-in-create-msg-from-string.txt` and `opt-in-dialogue-window.txt` in the mod's
  folder, recorded with OBS (about 11 min). The mod's `message-log.txt` (one run, 770 s) and `transcript.txt` hold game
  text and stay local. Only record kinds, counts, cursor numbers and message file paths are kept here. Screens were
  matched to log times from video frames; the mod started 61 s before the recording, so video time = log time − 61 s,
  to within a couple of seconds.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. Started the game from Reloaded-II with the three opt-ins; checked `hooks 23/23` in Reloaded-II's log.
2. The maintainer loaded a save and walked the in-game menu: Skill, Item (with Memorandum), archetype details,
   Equipment, Party, Follower, Quest (with Travellers' Voices), Calendar, Tutorials, Settings, and the archetype
   inheritance screen; then a short scene with a reply choice.
3. Records in `message-log.txt` were counted by kind, cursor records by manager, and pane records by the time window
   of each screen. No text was printed.

## Results

Records by kind over the run: pane 136 667, register 41 876, resolve 4 272, input 1 009, selected 952, cursor 671,
cue 623, compile 90, lookup 87, msgwindow 24, request 21, player-name 10, select 1.

### Per screen

"Panes" is the number of distinct pane addresses drawn in the window; "texts" the number of distinct texts behind them;
"cursor" the manager that handled the screen's list and the highest index it reached.

| Screen | Video time | Pane records | Panes | Texts | Cursor manager (max index) |
|---|---|---|---|---|---|
| Skill | 65–95 s | 4 422 | 31 | 55 | `0x66908d80` (7: 8 characters) |
| Item | 95–130 s | 7 138 | 50 | 39 | `0x669088d0` (11) |
| Memorandum | 130–160 s | 7 770 | 126 | 53 | `0x466f0bc0` (28) |
| Archetype details | 175–240 s | 17 631 | 141 | 96 | `0x63764040` (4) |
| Equipment | 240–290 s | 12 960 | 133 | 90 | `0x6525ba40` (5) |
| Party | 290–360 s | 16 664 | 161 | 83 | `0x7d1a8a80` (6) |
| Follower | 360–395 s | 4 651 | 71 | 53 | `0x63763050` (16) |
| Quest | 395–437 s | 10 107 | 99 | 51 | `0x13c76a520` (8) |
| Calendar | 438–467 s | 4 442 | 39 | 25 | `0xd672d10` (27) |
| Tutorials | 505–537 s | 4 797 | 44 | 35 | `0x132e6d2a0` (4) |
| Settings | 537–575 s | 10 688 | 173 | 106 | `0x1256b02f0` (27) |
| Archetype inheritance | 575–605 s | 6 211 | 70 | 87 | `0x5da7ba0` (19) |

- **One cursor class for every list but one.** 44 managers handled input; all have class `0x22045e8` except Calendar's
  day grid, `0x22046a8`. This agrees with #93's shared cursor
  (`docs/raw-sources/spike-session-2-2026-11-16.md`).
- **Each screen makes its own manager**, so a manager's address names a screen only for that visit.
- **Pane text covers each screen.** Every screen's labels, values and descriptions reached the pane listener; 5–25 % of
  pane records per screen had no text behind them.

### The title's message file compiles during play

`EN/message/system/Title_System.msg` compiled at −49 s (title screen), then at 400 s (Quest, opening Travellers'
Voices), 450 s (Calendar), 537 s and 706 s. `TitleMenuFocus` takes the first cursor after that compile as the title
menu's, and the transcript has "Main menu" plus the first title item three times during play.
