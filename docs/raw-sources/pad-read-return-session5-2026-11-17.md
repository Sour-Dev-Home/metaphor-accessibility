# Capture sessions 4 and 5: what a pad read's `returned=0` goes with (issue #225)

- **Source:** capture evening #229, sessions 4 and 5, on the maintainer's machine. Session 5: the Debug mod from branch
  `capture-1` at `219282c` through Reloaded-II (`hooks 31/31`), Steam Input on all session, one controller, OBS
  recording with narration (video time = log time − 51 s). Session 4: the same evening's earlier run, also with the
  pad-read listener. Both `message-log.txt` files stay local (they hold game text); only the `input` records (no text)
  are used, counted with `grep` and `awk`. The record is written by `src/Metaphor.Hooks/InputHooks.cs` on `capture-1`
  (`input pad<n> buttons=0x… handed=0x… returned=<al>`, logged per source only when the text changes). The function:
  `PadRead`, rva `0x10d0a00`, in `docs/raw-sources/input-route-2026-11-16.md`.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Because a record is written only on a change, one record stands for every read after it until the next one.

## Pad 1: always 0, with stale or junk bits

| | Session 5 | Session 4 |
|---|---|---|
| `pad1` records | 15 | 132 |
| … with `returned=1` | 0 | 0 |
| First record | 6.26 s, `buttons=0x410e` | 6.18 s, `buttons=0x410e` |
| Next record | 2,099.09 s (shutdown) | 658.74 s (shutdown) |
| Shutdown records span | 0.44 s, to the log's end | 0.33 s, to the log's end |
| Masks seen at shutdown | `0x0000`, `0x1a0c`, `0x03f9`, `0x8000` | `0x0000`, `0x1a0c`, `0xfab9`, `0x8000` and 5 others |

So every pad 1 read in both sessions returned 0. Through the whole session the state held the same `0x410e`, and the
listener handed it to the dialogue controls unchanged (`handed=0x410e`). At shutdown the reads alternate, often two in
the same millisecond, between `0x0000`, `0x1a0c` and other masks, including `0x8000` (the repeat button) once in each
session. With one controller and Steam Input on, XInput slot 1 has no pad, so `returned=0` on pad 1 means "no pad in
that slot", and the state it leaves is not a pad state.

## Pad 0: 0 only in short gaps, together with the keyboard

| | Session 5 | Session 4 |
|---|---|---|
| `pad0` records | 3,465 | 1,234 |
| Gaps with `returned=0` | 24 | 16 |
| Gap length | 185 to 201 ms | 179 to 207 ms |
| Buttons during a gap | `0x0000` every time | `0x0000` every time |

In session 5, every pad 0 gap starts within 1 ms of a `keyboard … returned=0` record, and the keyboard's 24 gaps last
185 to 201 ms too. A missing controller wouldn't stop the keyboard read, so these look like the game pausing input for
about 12 frames, not a failed XInput read. Pad 0 never returned 0 for longer than 207 ms in either session, though the
maintainer's controller dropped during session 5's start sequence (narration, video 0:02). With Steam Input on, pad 0
should be Steam's virtual pad, which would hide a physical disconnect; that is `[NEEDS VERIFICATION]`, as the log
can't tell the two apart.

The session 5 gaps, in video time (two fall before the video starts and one after it ends): 0:03, 0:28, 1:07, 1:28,
2:42, 9:05, 10:59, 11:21, 12:05, 14:13, 14:28, 14:59, 17:48, 18:53, 26:06, 30:24, 30:35, 32:27, 32:28, 33:48. The
narration puts several at screen changes (0:28 load game, 1:07 loading a save, 14:13 leaving sound settings, 14:59
loading a save). What starts a gap is `[NEEDS VERIFICATION]`: a by-frame look at the video at those times would say.

## For #225

- `returned=0` from `PadRead` means the state isn't a fresh pad read: either no pad in that slot (pad 1, every read), or
  the game's short input pause (pad 0, with the keyboard, about 200 ms). In both cases the controls should skip it.
- Pad 1's state carries bits for the whole session (`0x410e`), and `0x8000` at shutdown, so ignoring `returned=0` is
  what keeps the repeat key from firing without a press.
- Not confirmed: a physical pad with Steam Input off, and a second connected pad (pad 1 returning 1).
