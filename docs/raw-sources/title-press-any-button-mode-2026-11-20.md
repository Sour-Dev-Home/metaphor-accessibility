# Title "Press any button": TitleChoice's mode is 0 until a button shows the menu (#555)

- **Source:** the shared Ghidra project (Ghidra 12.2, `scripts/dev/ghidra.ps1`: `offset 0x4a8`, `decompile 0xe2c620`),
  and the maintainer's local `message-log.txt` from the by-ear check of 2026-11-20 (Debug mod build `62f58cf`, the
  #553 branch, which logs `mode=` on each `cursor` record; Reloaded-II's log: `Ready on game build 548ddc95…, hooks
  19/19`), read with grep. The log holds game text, so it stays local; only labels, modes, indexes and times are kept
  here, and the one message's text is quoted.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

While the title shows "Press any button", `TitleChoice+0x4a8` is 0. A button press shows the three items and sets it
to 1; back on the title menu sets it to 0 again, so the prompt shows once more. The prompt's text is the message
`Title_PressAnyKey` in `EN/message/system/Title_System.msg`, which reads `PRESS ANY BUTTON`.

## Static

- `offset 0x4a8` lists the writes in `TitleChoice`'s code: only the decide and back handler `0xe2c620` writes it
  (`0xe2c6cc` 1, `0xe2c78d` a register, `0xe2c8b9` 1, `0xe2cd46` 2). Nothing in analysed code writes 1 before it.
- `0xe2c620` opens with `if (+0x4a8 == 0)`: it hands the `+0x3e0` list (the title's items) to the layout at `+0x3d8`
  with the cursor at `+0x4ac` (`0xd40ce0`), sets the cursor through the choice's `vf11` (`ChoiceGetCursor`), writes 1
  at `0xe2c6cc` and returns `0x39`. So the items are handed over only on leaving mode 0.
- Otherwise, when button check `0x7ae520(5, …)` passes in mode 1, it sets `+0x4ac` to 1 if the byte `+0x4b0` is set
  (else 0), sets the cursor there, writes 0 to `+0x4a8`, calls `0xd3ca80` on the layout and returns `0x3a`: back from
  the title menu to the prompt. Which button 5 is, and what `+0x4b0` means, are `[NEEDS VERIFICATION]`.
- The cursor handler `0xe2d200` returns at once in mode 0 ([finding](../findings/title-names-voice-tutorials-static.md)).

## Live

Every `TitleChoice` cursor record in the log, by mode:

| Log time (ms) | What happened | `index` | `mode` |
|---|---|---|---|
| 71,388 | first title cursor after boot (the screen showed "Press any button", by eye, #555) | 0 | 0 |
| 168,019 | first record after it: the items shown | 1 | 1 |
| 500,649 | first title cursor after Return to Title | 0 | 0 |
| 501,333 | `input pad0 buttons=0x0008` (a button press) | | |
| 501,351 | next record: the items shown | 1 | 1 |

Between those, mode 1 and 2 follow the title menu and its System submenu, as the finding expects. Mode 0 appears only on
the first record of each visit. The cursor manager's input function is called during the prompt as well, which is why
the mod spoke the menu then (#555).

`Title_PressAnyKey` appears once in the log's `compile` record for `Title_System.msg` (11,530 ms, id 56, 3,001
characters), with the single line `PRESS ANY BUTTON`. The screen draws the prompt from an image with the same words
([`title-menu-item-images-2026-11-20.md`](title-menu-item-images-2026-11-20.md)).
