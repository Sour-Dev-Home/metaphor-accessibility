# Quick menu and message serials, from the by-ear run of 2026-11-20 (#584)

- **Source:** the maintainer's by-ear run of 2026-11-20, Debug mod build `38d2b09-dirty` (main + #578 + #582), its
  `message-log.txt` and `report-log.txt`, re-read with grep and awk on the maintainer's machine. The logs hold game
  text, so they stay local; only labels, serials, flags, class names, indices and times are kept here.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -a " compile "` for the system message files: `7795 ms compile id=6 path=EN/message/system/System_Message.msg`
   and `7829 ms compile id=48 path=EN/message/system/System_Message_PC.msg`.
2. From each file's logged source, up to the next `compile` record, the labels in order: a line `@<label>` outside a
   message body, followed by `{`. Numbered from the file's first serial (1142 and 11453, see the finding).
3. Every `resolve via=GetMessageBySerial` record for serials 1518–1536, 1888–1890 and 11469–11474.
4. Every report-log `cursor` record whose `screen=` is `InstantMenuChoice@ope@ui@app`, and the first record of another
   screen after it.

## What it shows

- **Message counts.** `System_Message.msg`'s source has 790 messages, `System_Message_PC.msg`'s 354. A body line in
  `System_Dummy` starts with `@` (it reads `@System_Dummy`); it is text, not a second label. Counting it would make
  791 and shift every later serial by one.
- **The settings categories check the numbering.** `System_category_name_PC_01`–`_06` are messages 16–21 of
  `System_Message_PC.msg`, so serials 11469–11474; the Settings list resolved 11469–11473, `flags=9`, as it opened
  (46779 ms), and the settings capture names those five.
- **The difficulty help lines check it.** `System_mode_01`–`_03` are serials 1888–1890 (messages 746–748 of
  `System_Message.msg`). #540's run resolved 1890, 1889, 1888 as the difficulty cursor reached Normal, Easy and
  Storyteller, which these labels describe in that order.
- **The quick menu.** `System_EasyMenu_01`–`_18` are serials 1518–1535. As the quick menu opened (469755 ms, again at
  526896, 659867 and later), one frame resolved, with `flags=8` and `message=set`, in this order: 1525, 1529, 1530,
  1527, 1526, 1533, 1528, 1531. Those are `System_EasyMenu_08`, `_12`, `_13`, `_10`, `_09`, `_16`, `_11`, `_14`. Serial
  1532 (`_15`) was not resolved.
- **The labels pair up.** `_01`–`_07` are one-word item names and `_08`–`_14` their help lines, in the same order
  (`_08` helps `_01`, `_14` helps `_07`); `_15` is a name and `_16` its help line; `_17` a name and `_18` its help line.
- **The menu's cursor.** The first cursor record after the resolves: `469772 ms ... index=0 ... screen=InstantMenuChoice@ope@ui@app count=8`.
  The quick menu has 8 rows in every record of the run.
- **Two rows open their screens.** Settling on index 3 then confirming opened `InstantTutorialChoice@ope@ui@app`
  (1454530 ms), and index 5 opened `GlossaryListChoice@ope@ui@app` (1400561 ms, three times in the run). In the resolve
  order, the 4th help line is `_10` (tutorials, for `_03`) and the 6th is `_16` (for `_15`).
