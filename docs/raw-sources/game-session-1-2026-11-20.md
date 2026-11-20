# Game session 1: title, settings and a new game's prologue (#496)

- **Source:** game session 1 (#496) on the maintainer's machine, 2026-11-20, Debug mod build `e77eeab` through
  Reloaded-II (`hooks 22/22`), opt-ins `inspector`, `create-msg-from-string`, `ui-text-panes` and `dialogue-window`.
  Read from the session's save folder: `inspector-log.txt` (every inspector answer, by stop), the main run's
  `message-log.txt` and `transcript.txt` (game start about 00:04:19 to 00:45:58 local time), the second launch's
  (00:47:39), and Reloaded-II's log for the main run. Parsed with grep, awk and Python 3.12; the maintainer's notes by
  ear and eye are marked as such. The logs hold game text, so they stay local; only addresses, values, counts, labels
  and times are kept here.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Log time is milliseconds since the game started; local time is about log time plus 00:04:19 (Reloaded-II's first mod
line, `speech: OneCore`, is at 00:04:19, and the inspector's stamped answers fall where the log's records put each stop).

## Order

The opening scene starts before name entry: the first line request (`MES_e00_002_001_000_00`) is at 1278 s, name entry
at about 1317 to 1340 s (a message window opens and closes there), then the scene goes on at `_000_03`. #496 lists name
entry (stop 4) before the scene (stop 5).

## Stop 1: inspector

`base` answered `ok 0x140000000 0x14d49000` at every stop. No slowdown (maintainer).

## Stop 2: title menu and System submenu (#210)

- **Cursor records.** 41 `cursor` records name `TitleChoice@ope@ui@app`, all `count=3`, on three managers over the
  run (`0x3ca389c0` for the first title screen, then two more after returning to the title). The submenu logged one record:
  on decide at title index 2 (232551 ms, `cmd=0x16`), the next record (234109 ms) is `index=0 count=3` on the same
  manager. Moves inside the submenu logged none; back (265796 ms) logged `index=2`. So the count doesn't tell the
  submenu from the title menu, and session 5's pattern holds: decide on index 2, then index 0 on the same manager.
- **Transcript.** After the third item the mod spoke the title's first item again, `n of 3` (the #210 bug).
- **Inspector.** The batch's chain `follow base+0x2ee7338 0x48 0x0 0x8 0x40` answered `0x88000b002540e11d`, and every
  longer chain failed at step 5. The snapshot (00:09:57): `[base+0x2ee7338]` = `0xcb6f7a0`; `[0xcb6f7a0+0x48]` =
  `0x40606940`; at `0x40606940` the qwords `0x5c85d20`, `0x5c85d28`, `0x5c85d28`; at `0x5c85d20` the qword
  `0x72add670`, then non-pointer values. `rtti` found no class on `0x5c85d20` or `0xcb6f7a0`. Reloaded-II's log names
  `0x2ee7338` the "global menus-open flag".

## Stop 3: Settings (#96, #103 question 3)

Read at 00:23:17 in English: `base+0x27a8c78` = 2, `base+0x27a8c74` = 2, `base+0x2dff8e0` 16 bytes =
`2, 2, 2, 1` as dwords, so `base+0x2dff8e4` = 2.

Settings cursor records (counts only): the top list `ConfigTopChoicePc` has 5 rows; `ConfigMenuChoicePcT` pages log 20
or 24 (`0x76`), 6 (`0x77`), 7 (`0x78`) and 4 (`0x79`) rows; `ConfigMenuChoiceKeyConfig` logs 3, 12, 14 and 23.

## Stop 4: name entry (#106 question 1)

- **Sweep.** The 00:26:49 sweep (after the name was confirmed) holds the typed name at `base+0x2ccae60`, UTF-32LE, 8
  characters, in the read of `base+0x2cca6f0` at `+0x770`: `G + 0x24770` with `G` = `base+0x2ca66f0`. No ASCII or
  UTF-16LE copy is in any of the 74 reads. The 00:24:31 sweep, before the new game reached naming, holds zeros there.
  The maintainer saw the default name there before naming, and the typed name again after a second new game (by
  inspector, not in this log).
- **`create-msg-from-string`.** 181 `player-name` records, all `match=unset`: the run had no `spike-player-name.txt`,
  so none was compared. Lengths 0 to 9 bytes. Locations: `heap` (48), `module+0x17e400f` (26), six at
  `module+0x21982b8` to `+0x21982e0` (14 each), three at `module+0x2ee9310`, `+0x2ee9350`, `+0x2ee9390` (7 each),
  `module+0x2ee8f50` and `module+0x17f4e38` (1 each). None is at `module+0x2ccae60`. Around name entry (1317 to
  1333 s) only 1-byte `heap` strings and empty strings went through.

## Stop 5: the opening scene's speaker (#74, #106 questions 2 and 3)

- **By ear and eye (maintainer).** The box shows the alias for the whole scene; the reader says the source name (#74's
  bug).
- **`CharaID.bin`.** `follow base+0x2eebc18 0x40 0x10` answered `0x5e7c1ad0` in both new games; the batch's `0x10` step
  already lands on entry 0, so entry `n` is at `0x5e7c1ad0 + n × 0x6c`. The 0x2a30-byte dump holds 100 entries, 61 with
  any key; empty keys are MD5 of the empty string. Entry 51's default key is `MD5("CharaName_FO12_Mor")`, slot 0's
  `MD5("CharaName_FO12_Mor_00")` with flag `0x978`, slot 1's `MD5("CharaName_FO12_Mor_01")` with flag `0x979`. Other
  entries' slot flags run `0x964` to `0x99a`.
- **Flags.** In all four dumps (00:30:52 and 00:31:03 after the scene, first game; 00:33:51 and 00:33:56 at the end of
  the scene, second game) flag `0x978` is set and `0x979` clear. 197 to 200 flags are set; 29 of the 51 flags `0x963`
  to `0x995` are among them.
- **Hash listener.** `MessageSerialByHashListener` was installed (Reloaded-II: module offset `0x9e8e60`) and logged no
  record in either run. No `GetMessageBySerial` record fell inside the scene's lines either.

## Stop 6: dialogue, voice and input

- **Maintainer.** The reader toggle works on pad and keyboard; speech doesn't talk over voice acting; nothing swallowed,
  no stray speech in free movement. Two bugs: Gallica's lines in the scene after the opening cutscene aren't read, and
  pad X (repeat) there replays the opening's last line.
- **Route.** That scene compiles `EN/message/event/e01_002_001.msg` (1474647 ms; second game 1864422 ms) and
  `e01_004_001.msg` (1480653 ms; 1866797 ms). `msgwindow-hook` logs `close` just before. Then `msgwindow current=`
  turns non-null (`0x124ceb180` from 1485810 ms for about 64 s; second game `0x11ffc8a90` from 1870737 ms, three
  times, until 1970762 ms). In those windows: no `request` (`MsgRequestByName`), no `select`, no `resolve`, no
  `msgwindow-hook open`, and no `pane` text longer than 25 characters. The opening scene's read lines each came as a
  `request` (15 in the first game, 15 in the second). Gallica's lines later in the field
  (`MES_D00_01_e01_010_001` and on, `dng_D00_01.msg`) came as `request` records with `msgwindow-hook open` and were
  read. In the transcript the opening's last line repeats three times (first game) and twice (second) after the
  scene's last request: the stale repeat.

## Stop 7: tutorial pop-ups (#118, #109)

- **Maintainer.** The reader says nothing. Icons: Menu (three lines) to skip, A next (bottom right), B back after the
  first page; they switch to the keyboard's keys on keyboard input and back on pad input.
- **Log.** No `resolve` record with a serial from 10256 to 10859 in the run (0 of 3579). `System_Tutorial_1.msg` and
  `_2.msg` compile together each time a pop-up comes up: 1985324, 2202377, 2337565 and 2338829 ms. Between those times:
  `pane` records 6393, 5981, 104 and 79, with 12, 45, 7 and 9 distinct texts, none longer than 25 characters; no
  `request`, and 0, 10, 0 and 3 `resolve` records, none in the tutorial range. The two files' compiled text holds
  `PAD_ICON` 61 times, `WEATHER_ICON` 6 and `SHOP_ICON` 4.

## Stop 8: Japanese voice (#103 question 3)

- After switching to Japanese (00:44:51 and 00:45:31): `base+0x27a8c78` = 1, `base+0x2dff8e4` = 1, `base+0x27a8c74` = 2.
  No restart was asked for (maintainer).
- Second launch (00:47:06): the same three values.
- **Cues.** Neither run played a voiced line after the switch: the main run's records after 2400 s are 47 cues on two
  banks and no `request`, and the second launch logs 23 cues on two banks (ids 0, 1, 2, 56, 57, 1068), no `request`. The
  comparison is open.

## Speech backend

Reloaded-II: `speech: OneCore` at 00:04:19, no later `speech:` line. NVDA was started after the game (maintainer).
