# Key Configuration: the OK button's mode and each cell's action, from the combined session (#589)

- **Source:** the mod's `message-log.txt` from the maintainer's combined session on 2026-11-21 (Debug mod build of
  ten PRs on main, NVDA; PR #684's comments of that day), re-read with grep and awk. The log holds game text, so it
  stays local; only labels, fields, class names, indices, counts and line numbers are kept. Line numbers are that
  log's.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Adds to [`grid-cursor-fields-static-2026-11-21.md`](grid-cursor-fields-static-2026-11-21.md). Records: `cursor` and
`grid` (each settled input on the grid cursor, class `0x22046a8`, screen `ConfigMenuChoiceKeyConfig@ope@ui@app`),
`keyconfig-state` (the choice's `+0x900`–`+0x91f`, logged when it changes) and `resolve` (`GetMessageBySerial`, with
the label the mod's serial table gives).

## Steps

1. `grep -n keyconfig-state`, cutting the 32-bit fields at `+0x910` and `+0x914` out of `bytes=`, each with the
   `cursor` record before it (index, count, flags).
2. awk over lines 178300–300000: for each `grid` record, the `System_Keybind_name_PC_*` and
   `System_mid_subject_name_key_PC_*` labels of the `resolve` records since the one before it.
3. awk over the `cursor` records: each change of `count`, with the last action label resolved before it.

## The OK button: `+0x914`

- `+0x914` reads 1 on three records and 0 on all others (115 changes logged): 211967 (index 22, count 23), 230468
  (index 0, count 23) and 244772 (index 1, count 12), the three visits to the OK button.
- The next change after each is back to 0 on a cell: 214590 (index 22, flags `0x46`), 233837 (a new choice, index 0)
  and 246784 (index 0, flags `0x42`).
- The flags' item bit (`0x4`) is clear on each OK visit (`0x42`), but also clear on cells from 246784 to the end of the
  screen's use (for example 246872, 247030, 254083, 260095: `+0x914` 0, flags `0x42`), so it doesn't tell the button.

## Each cell is one action

- The view shows six rows of two cells: the `grid` records' `line` runs 0–5, `columns=2`, and the `pane` records drawn
  with them sit at two x positions (700, 2022) and six y positions (750–1630, every 176), for example 203785–203800.
- Counts seen: 23, 3, 14, 12, and never another (290 `cursor` records). Every view's labels are one of five lists, in
  this resolve order (`System_Keybind_name_PC_` numbers), each after the sub-tab labels `_PC_01`/`02`/`03`/`04`/`21` at
  the first view of a tab:

| Sub-tab (`System_mid_subject_name_key_PC_`) | Count | Labels in resolve order |
| --- | --- | --- |
| `_01` | 23 | 32 33 35 34 61 62 64 63 86 87 89 88, then 19 18 91 90 29 30 83 58 20 25 28 |
| `_02` | 3 | 37 38 36 |
| `_03` | 14 | 92 68 55 47 27 70 69 66 57 65 46 45, then 59 48 |
| `_04` | 14 | 93 51 95 97 60 49 98 96 52 94 54 99, then 56 53 |
| `_21` | 12 | 05 04 03 02 07 01 06 16 15 10 09 08 |

- Which sub-tab each list is: the count matches, and the lists follow the tab labels' order as the tabs are walked
  (23 → 3 → 14 → 14 → 12 → 23, lines 203801–211495).

## Rows: pinned by the labels each scroll reveals

Each down move that scrolls (`scroll` up by 1) resolves only the newly shown row's two labels, between that move's
`grid` record and its settled one:

- `_01`, count 23, walked to the end twice (229239–230232 and 236597–237796): scroll 1 → 19 18, 2 → 91 90, 3 → 29 30,
  4 → 83 58, 5 → 20 25, 6 → 28 alone (row 11 has one cell, index 22). Scrolling up from 6 to 5 (237567, index 20)
  resolves 89 88: row 5.
- `_03`: scroll 1 → 59 48 (217901, 240255). `_04`: scroll 1 → 56 53 (210415, 219863, 243066).
- With the view at scroll 6, the whole view re-resolves (214589) as 19 18 91 90 29 30 83 58 20 25 28: rows 6–11 in
  row order. So a whole view's labels resolve top row first, and the scroll-0 view's twelve are rows 0–5.

So cell `2r` and `2r + 1` bind the two actions of row `r`'s pair. Which of the pair is the left cell is
`[NEEDS VERIFICATION]`: every pair resolves in the same order whatever the move, and the order isn't the labels'
numbers (35 before 34, 64 before 63, 89 before 88, 19 before 18), but nothing in the log ties a label to a column.

## Not told by the log

- Field and Dungeon (`_03`, `_04`) both have 14 cells, and the choice's `+0x900`–`+0x91f` don't change with the
  sub-tab. A switch between them can also keep the index and count, so no new settled input is logged: from Battle to
  Dungeon at index 0 (225058), then on to Event (225435), the 225436 resolves show Field's view drawn between them.
- Which device tab (`System_mid_subject_name_key_PC_15`/`16`/`17`) is shown: all three resolve together with every
  tab change and nothing else in the records differs. The maintainer visited all three, and every view's labels are
  one of the five lists above, so the actions don't seem to change with the device tab.
