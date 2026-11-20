# The voice language is 1 for Japanese and 2 for English, applied without a restart

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community
  mods do
- **Sources:** [`game-session-1-2026-11-20.md`](../raw-sources/game-session-1-2026-11-20.md)

For #103 question 3: `base+0x27a8c78` (as set) and `base+0x2dff8e4` (as applied) hold 2 for English and 1 for Japanese.
Both change as soon as the setting changes, and both keep the value after a relaunch. `base+0x27a8c74` stayed 2.

## Evidence

| When | `0x27a8c78` | `0x2dff8e4` | `0x27a8c74` |
|---|---|---|---|
| English, Settings (00:23:17) | 2 | 2 | 2 |
| Japanese, just switched (00:44:51, 00:45:31) | 1 | 1 | 2 |
| Japanese, second launch (00:47:06) | 1 | 1 | 2 |

The game asked for no restart (maintainer). That `0x27a8c74` is the text language is still `[NEEDS VERIFICATION]`: it
didn't move with the voice. Whether voice cues change with the value is open: no voiced line played after the switch
in either run.

## Steps

Session 1 (#496), stops 3 and 8: `stop-3-settings.txt` and `stop-8-voice.txt` with `session.ps1 inspect -Batch`.
Which global is which is in the [static note](title-names-voice-tutorials-static.md).
