# "Save complete" pop-up, from the by-ear session of 2026-11-22 (#622)

- **Source:** the maintainer's by-ear session of 2026-11-22, Debug session build, its `message-log.txt` (log times
  1285700 and 1328906 ms) and the maintainer's report on #622. The logs hold game text, so they stay local; only labels,
  hashes and the one short line are kept here.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## What it shows

- Both saves end with one `request via=MsgWindowShowLine kind=1 hash=FBE58C2A2EA528A312669F640C64928F`, then a
  `msgwindow current=` record with a non-zero pointer. No `MsgRequestByName` record and no `resolve` precede it.
- That hash is the MD5 of `System_Save_05` (`printf %s System_Save_05 | md5sum`). Its message, compiled earlier in the
  same run, reads "Save complete." then `<WAIT>`.
- These are the run's only kind-1 shown lines. Kind 1 is the system-message kind
  ([`msg-request-functions-2026-11-16.md`](msg-request-functions-2026-11-16.md)).
- The mod spoke only kind-0 shown lines, so the pop-up was silent.
