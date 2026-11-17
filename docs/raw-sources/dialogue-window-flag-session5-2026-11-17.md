# Capture session 5: the dialogue window flag against the window hooks (issue #232)

- **Source:** capture evening #229, session 5, on the maintainer's machine: the Debug mod from branch `capture-1` at
  `219282c`, launched through Reloaded-II with `opt-in-dialogue-window.txt` and `opt-in-dialogue-window-flag.txt`
  (`hooks 31/31`, none skipped), Steam Input on, OBS recording with narration (video time = log time − 51 s).
  `message-log.txt` holds game text, so it stays local; only the `msgwindow` and `msgwindow-hook` records (no text),
  times and counts are used here, paired with `awk`. Record formats: `src/Metaphor.Core/MessageLog.cs`; the flag reader:
  `src/Metaphor.Hooks/DialogueWindowWatch.cs`; the hooks: `src/Metaphor.Hooks/DialogueWindowHooks.cs` (all on
  `capture-1`). The flag's fields: `docs/raw-sources/dialogue-window-flag-2026-11-16.md` (on `session-3`).
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## The records

The flag is read on every pad read and logged only when it changes:
`msgwindow current=<[W+0x60]> state=<[W+0x560]> msg+48=<[[W+0x60]+0x48]> handles=<[M+0x18]>+<[M+0x58]>`. The hooks log
`msgwindow-hook open` (`MsgOpen`'s body) and `msgwindow-hook close` (`MsgClose`'s body), both in `.text1`.

The 35-minute run logged 180 `msgwindow` records and 47 hook records (23 opens, 24 closes). The first flag record
(6.26 s) reads `window=none handles=none`: the globals weren't set yet. From 8.37 s on, every read succeeded.

## Result: the hooks track message handles, not the window

### `handles` follows the hooks exactly

Pairing each hook record with the next change in `handles` (the sum of both counts):

| | Count |
|---|---|
| Hook events followed by a matching ±1 change (open +1, close −1) | 45 of 47 (22 opens, 23 closes) |
| Lag, hook to the flag record | 3 to 35 ms (one pad read; 44 of 45 within 17 ms) |
| Hook events with no change | 2: an open at 859.84 s and a close at 893.99 s |
| Changes with no hook | 2: `0+0` → `0+1` at 18.62 s, `0+0` → `1+0` at 776.64 s |

The four exceptions fit the reference counts in the 2026-11-16 note: the handle opened at 776.64 s (by a path the hooks
don't see) was opened again by `MsgOpen` at 859.84 s (count bumped, map size unchanged), released at 893.99 s, and
released for good at 905.10 s, when `handles` dropped to `0+0`. 38 of the 45 matched changes are in the second count
(`M+0x58`) and 7 in the first (`M+0x18`): the opens at 1,342.16 s, 1,390.24 s and 1,487.28 s, their closes, and the
close at 905.10 s.

### The window pointer `current` is the window, and the hooks don't match it

`current` (`W+0x60`) went from 0 to a pointer 58 times and back to 0 58 times. Every record with `current=0x0` has
`state=6` (94 of 94), and no record with a pointer has state 6, as the note's close routine predicts.

| Window episode (`current` 0 → pointer) | Count |
|---|---|
| Inside a hook open-close interval | 21 |
| With no hook open | 37 |

| Hook open-close interval | Count |
|---|---|
| With a window inside it | 13 |
| With no window at all (10 to 27 ms long) | 8 |

Of the 13 intervals with a window, the window came 16 ms to 54 s after the hook's open (10 within 112 ms). Of 24 hook
closes, 8 came with the window still open (it cleared 16 to 17 ms later, once 2.96 s), and 16 came after it had
already cleared (0.3 s to 77 s after). So the hooks agree with the window only when one script both opens a message
file and shows a line; 37 windows, including the conversations of the narrated "window flag" step, opened with no
hook event.

### What the window fields say

| `state` | `msg+48` | Records | When |
|---|---|---|---|
| 0 | 1 | 61 | a line on screen (50 of the 58 episodes start here) |
| 4 | 2 | 14 | a choice on screen |
| 5 | 0 | 6 | always the same object, `0x1315afe20`, 0.9 to 2.0 s each |
| 1 | 1 | 2 | the same object, `0x1315afe20`, 0.14 s and 0.92 s, between lines of a conversation |
| 2 | 1 | 2 | the object `0x464779c0`, while the first `handles` count is 1 |
| 6 | `-` | 94 | closed (`current=0x0`) |

The choices line up with the narration: state 4 at 846.34 s (video 13:15, the maintainer reading out a two-option
choice) and at 1,025.39 s (video 16:14, the narrated choice in the "window flag" step). What screens states 5, 1 and 2
belong to is `[NEEDS VERIFICATION]`.

### Lag against the line

Of the 87 line requests (`MsgRequestByName` and its twin), 34 came with the window closed; for each, the next flag
record was the window opening, 5 to 17 ms after the request (the next pad read). The other 53 came with the window
already open (later lines of the same conversation reuse the pointer). A close is seen at the first pad read after the
game clears `W+0x60`; the log has no independent close time to measure it against.

## For #232

- The field to read is `current != 0` (`[[base+0x2f04960]+0x60]`), with `state` 4 marking a choice. It changes with
  every window in the run, one pad read after the game's request.
- The acceptance check "matching the window hooks" can't hold: the hooks bracket message handles, and match the window
  in 21 of 58 episodes. The window pointer is the better source, not just an equal one.
- Not shown: that states 1, 2 and 5 are dialogue windows the controls should act on, and that `W+0x60` is never left
  set behind a menu or battle screen. A video check at the times above settles both.
