# Gallica's first reply choice reaches only the select window hook (#541)

- **Source:** the maintainer's by-ear check of 2026-11-20 (a test build carrying #578's select window hook), Debug
  mod through Reloaded-II. Read from that run's `message-log.txt`, as relayed to this work for #541. The log holds game
  text, so it stays local; only record types, kinds, labels, screen classes, counts and times are kept here.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

When Gallica's first reply choice opened, after the opening cutscene, the scripts' choice hook (`select` record) logged
nothing. The select window hook added in #578 (`SelectWindowRequest`, `0x9ee5e0`) did, with kind 0 and both labels.
So this choice is opened by a native call of `0x9ee5e0`, one of the routes in
[`reply-choice-routes-static.md`](../findings/reply-choice-routes-static.md), not by `0x9ee3f0`, which the hook
doesn't see. The choice moved on the shared talk-choice cursor.

| Time (ms) | Record | Fields kept |
|---|---|---|
| 321428 | `select-window` | `kind=0 prompt="MES_e01_004_001_002" choice="MES_e01_004_001_003"` |
| 321428 | `request via=MsgWindowShowLine` | `kind=0` (the prompt, shown in the same call) |
| 323627 | `cursor` | `screen=CommonTalkChoice@ope@ui@app count=2` |
| 324261 | `selected` | `id=0` |

No `select` record lies between the window opening and the selection. Nothing was spoken for the choice.

A later Gallica choice in the same run took the same route, and a system yes/no dialog shows the other kind:

| Time (ms) | Record | Fields kept |
|---|---|---|
| 809321 | `select-window` | `kind=0 prompt="MES_e01_004_002m_000" choice="MES_e01_004_002m_001"` |
| ~828000 | `cursor` | `screen=CommonTalkChoice@ope@ui@app count=2`, moves to index 1, then 0 |
| 266310 | `select-window` | `kind=1 prompt="System_mode_08" choice="System_Choice_YESNO"` (the yes/no dialog) |

So in this run kind 0 came with reply choices and kind 1 with the system yes/no dialog.

## Not established

- That every native reply choice has kind 0, and every kind 0 window is a reply choice: two samples so far.
- Which native caller of `0x9ee5e0` opens them.
