# Backing out of Settings to the title's System submenu: what the cursor hook logged (#557)

- **Source:** the maintainer's by-ear check of 2026-11-20 (test build of #553 and #554), Debug mod through Reloaded-II.
  Read from that run's `message-log.txt` (`cursor`, `input` and `cue` records) with `grep` and `awk`. The log holds game
  text, so it stays local; only managers, classes, indexes, modes, pad masks, cue ids and times are kept here.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

**On the way back from Settings, the submenu's cursor manager logged nothing until the player pressed down.** The
`cursor` record is written only when an input differs from that manager's last one (`CursorEvents`), so the submenu's
return, with the same input it had before Settings opened, was dropped before `MenuScreens` could hear it.

| Log time | Input | Cursor record | Reading |
|---|---|---|---|
| 181.4 s | up | title manager `cmd=0x1 index=0 mode=2` | submenu, Settings |
| 181.6 s | confirm (pad `0x4000`, cue 57) | | |
| 181.8–185.1 s | right ×4 | new manager, `ConfigTopChoicePc<ConfigTopChoicePcLayoutInit>`, index 0 to 4 | Settings' categories |
| 185.5 s | confirm | new manager, `ConfigMenuChoiceKeyConfig`, `count=23` | a category's list |
| 186.9, 188.3, 188.8 s | back ×3 (pad `0x2000`) | none | back to the submenu |
| 189.6 s | down | title manager `cmd=0x16 index=1 mode=2` | submenu, Credits |

- The title manager logged none of the four moves made on Settings' categories, so it wasn't handed them.
  `[NEEDS VERIFICATION]`: whether its handler is called at all while Settings covers it (a call with the same input
  every frame would leave the same log).
- Leaving the category list for the categories (the first back) logged nothing either: the same gap one level down.
- The other two returns to the title manager in this run logged a record that differed from its last one (the title
  menu at index 2 after the submenu, mode 1; a dialogue choice reusing the manager), so they were heard.

## Steps

1. `grep` the `cursor` records with log time 181–191 s, and the `input` and `cue` records in the same window.
2. List every cursor record whose manager had logged before another manager's record in between (`awk` over manager
   and command), to find other returns.
