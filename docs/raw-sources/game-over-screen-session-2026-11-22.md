# Game-over screen, from the by-ear session of 2026-11-22 (#623)

- **Source:** the maintainer's by-ear session of 2026-11-22, Debug session build with `ui-text-panes` and
  `create-msg-from-string`, its `message-log.txt` (log times 2549927 to 2611719 ms) and the maintainer's reports on
  #623. The logs hold game text, so they stay local; only labels, hashes, times and speaker names are kept here.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## What it shows

- The party falls at 2549927 ms (the last `BattleHitResult` leaves the last ally at `hpLeft=0`).
- At 2564439 ms: `request via=MsgWindowShowLine kind=2 hash=F3B9AB242EADC4896C1FD526E1699033`, a voice cue at the same
  millisecond, then `msgwindow current=` non-zero at 2564445 and `0x0` at 2567587. No request by name precedes it.
- That hash is the MD5 of `MES_GAMEOVER_000_000`, compiled at start-up from `EN/message/Event_Resident.msg`: speaker
  More, clip `v_co_51300_c051`. The file holds 13 `MES_GAMEOVER_` labels (`000_000` to `004_001`), each voiced (`v_co_`
  clips), spoken by More or "Mysterious Voice". Which one plays when is `[NEEDS VERIFICATION]`.
- Between the window closing and the confirm press (pad `0x4000` at 2610746 ms) the log has no `resolve`, `request`,
  `select` or `cursor` record and no readable pane text. The maintainer saw "Fantasy is Dead" on screen until A or Space,
  so those words are drawn, not a compiled message `[NEEDS VERIFICATION]`. Message files for the reloaded save compile
  at 2611719 ms: no choices follow.
- That the window closes when the voice clip ends, rather than on a timer, is `[NEEDS VERIFICATION]`.
