# Nine-item quick menu, from the by-ear session of 2026-11-21 (#677)

- **Source:** the maintainer's by-ear session of 2026-11-21, Debug mod build `57add22` (#648), its `message-log.txt`
  (lines 292940-292948) and the maintainer's report on #677. The logs hold game text, so they stay local; only labels,
  counts and item names are kept here.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## What it shows

- On a save further into the game, the quick menu has 9 rows (`count=9`).
- As it opened, the help lines resolved in cursor order as `System_EasyMenu_` 18, 08, 12, 13, 10, 09, 16, 11, 14.
- `_18` is the help line of the item named by `_17` (the label pairing in
  [`quick-menu-serials-run-2026-11-20.md`](quick-menu-serials-run-2026-11-20.md)), and its text is "Use a skill". So
  row 0 is a new item whose name is probably `System_EasyMenu_17`, "Skill". [NEEDS VERIFICATION]: the log doesn't
  resolve the name label itself.
- Rows 1-8 resolve the same help lines, in the same order, as the 8-row menu's rows 0-7: the old layout moved down one.
- The maintainer heard the last row as "Return to Title".
