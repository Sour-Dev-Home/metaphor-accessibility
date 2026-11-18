# The title's System submenu shares the title menu's choice and cursor (#208, #210)

- **Source:** the maintainer's local message log from session 4 (`docs/raw-sources/screen-class-session-2026-11-17.md`:
  Debug mod build `b6c4f3f`, `ChoiceCursorGetterListener` installed), read offline with `grep` and `awk` on
  2026-11-17. The log holds game text and stays local; only times, indexes, button masks and class names are kept.
  The cut is replayable: `tests/fixtures/sessions/title-screen-class.txt` (log times 21 000–131 000 ms, written by
  `tools/Metaphor.SessionFixture`; fixture time = log time − 21 624 ms).
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

The two-item System submenu (Settings, Credits) is not a screen class of its own: the title menu's choice
(`TitleChoice`) and its cursor manager serve it too. `ConfigTopChoicePc<ConfigTopChoicePcLayoutInit>` and
`ConfigMenuChoicePcT<0x76>`, which the session note lists for the submenu, are the Settings screens opened from it.
So the screen class tells the title menu from everything in play, but not from its System submenu. What does is
`[NEEDS VERIFICATION]`: a field of the title's choice, or the manager's item count (the 16-byte entry vector at
`+0x88`/`+0x90`, `docs/raw-sources/screen-class-link-2026-11-17.md`).

## The sequence

All cursor records below are the one manager whose owner is `TitleChoice`, unless named; each settle is command `0x1`.

| Log ms | What happened | Cursor |
|---|---|---|
| 102 126 | Settled on the title's third item (System) | index 2, flags `0x46` |
| 108 092 | Pad `0x4000` pressed, confirm cue (`id=57`) | |
| 108 111 | Settled with no move before it: the submenu's first item | index 0 |
| 114 179 | Moved down, settled: the submenu's second item | index 1 |
| 121 797 | Moved up, settled | index 0 |
| 122 045 | Pad `0x4000`, confirm cue | |
| 122 262 | A new manager, owner `ConfigTopChoicePc<ConfigTopChoicePcLayoutInit>`, which then moves through indexes 0–2 | index 0 |
| 126 964 | A new manager, owner `ConfigMenuChoicePcT<0x76>` | index 0 |
| 127 980–128 947 | Pad `0x2000` three times (back), the last with cue `id=58` | |
| 128 965 | The title's manager again, on the third item | index 2, flags `0x66` |

- The title's manager logged only indexes 0–2 all session, with flags `0x46` in both the title and the submenu.
- The narration puts the System submenu at video 0:31, log 122 s: the Settings screens, by the table above.
- The cursor events drop an input equal to its manager's last one, so going back from `ConfigMenuChoicePcT` to
  `ConfigTopChoicePc`, and from Settings to the submenu, logged nothing: neither cursor had moved since.
