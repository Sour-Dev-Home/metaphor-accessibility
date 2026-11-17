# Title menu labels: which message holds the title menu's items, and how the cursor settles (#94)

- **Source:** the maintainer's two local message logs from spike session 2
  (`docs/raw-sources/spike-session-2-2026-11-16.md`, parts B and C), read offline with `grep` and `awk` on
  2026-11-16. The logs hold game text, so they stay local; only labels, counts and indexes are kept here.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

The title menu's items are the lines of one message, `Title_System_08` in `system/Title_System.msg`, in cursor index
order: index 0 is its first line, 1 its second, 2 its third. There are **3**. The cursor that moves through them
settles one call after each move, on the next input that isn't a move command.

## Method and counts

1. **The file.** Each log has one `compile` record for `EN/message/system/Title_System.msg` (part B at 18,477 ms,
   part C at 32,302 ms; id 56, 3,001 characters). It holds 25 labelled messages: `Title_System_00` to `_21`
   (with `_01_00`), `Title_PressAnyKey` and `Title_Copyright`. Read by their text, the others are the start-up
   notices, network, language, data-collection and demo-data prompts and their Yes/No (or Agree/Disagree) choices,
   and the licence prompt. `Title_System_08` is the only message whose lines are the title menu's items: 3 lines,
   then `<WAIT>`.
2. **The cursor.** Every `cursor` record has class `0x22045e8`. The first after the compile in each log is the title
   menu's (part B 27,451 ms, part C 41,371 ms), and that manager kept one address for the title screen (part B
   `27,451`–`39,941` ms and again `48,929`–`70,029` ms; part C `41,371`–`59,942` ms). Its `top` stayed 0 and `row`
   (so `index`) took only 0, 1 and 2 in both logs: 3 positions, matching the 3 lines. Other managers of the same
   class came later, on other screens (part B: 12 more managers, up to index 28; part C: none).
3. **Order.** In part B the maintainer loaded a save; the title menu's last record before the load screens was
   index 1, the second line. The order of the other two lines isn't confirmed by a selection:
   `[NEEDS VERIFICATION]`, by ear in the next session.
4. **Settling.** The title menu's manager logged 38 move records (commands `0x14`–`0x17`: 19 per log). 37 were followed
   one frame later (about 17 ms) by a record with command `0x1`; in 13 of those the index differed from the move's
   (mostly `0x14`/`0x15`, which the vertical menu put back). Each move's index was the previous `0x1` record's index
   plus 1 (`0x14`, `0x16`) or minus 1 (`0x15`, `0x17`), wrapping at 3, in 37 of 38 (the other followed a move
   directly). So the cursor's position is the index after a non-move input, and `0x16`/`0x17` move down/up on this
   menu.

## Not established

- Whether the title menu's cursor is a new manager when the player returns to the title from a game, and whether the
  file is compiled again then: neither log did that.
- The difficulty select. Part B shows two more managers of the same class right after the title menu (3 and 6
  positions, moved by `0x14`/`0x15`), but which screens they are, and which messages hold their items, the logs don't
  show.
