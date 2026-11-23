# PAD_ICON 6, 7 and 12 show LT, RT and B on an Xbox-glyph controller (#109)

- **Source:** the maintainer's by-ear session of 2026-11-22 ("c", Debug dev build of main plus open PRs), F8 marks 12
  and 21, written up on issue #109 by the maintainer's account (comment of 2026-11-22T21:58Z), with the tags read from
  the session's local `message-log.txt` (lines 650318 and 650346). The log holds game text, so it stays local; only the
  tags, the buttons seen and the line numbers are kept here.
- **Saved:** 2026-11-23.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Finding

On a controller with Xbox glyphs, the Basic Controls tutorial draws:

| Tag | What the line says it does | Glyph on screen |
|---|---|---|
| `<PAD_ICON 7>` | dash (hold while moving) | RT |
| `<PAD_ICON 12>` | dodge | B |
| `<PAD_ICON 6>` | Fae Sight | the left trigger (LT) |

After a keyboard press the same lines draw a key glyph instead, which follows the player's key binding
([`pad-icon-glyph-tables-static.md`](../findings/pad-icon-glyph-tables-static.md)); which key each number shows was not
noted. The other numbers, and the PlayStation and Switch glyphs, were not checked.

## Method

1. Controller in hand, Button Display on Auto, open the Basic Controls and Fae Sight tutorial pages; note each glyph.
2. Match each line to its tags in the message log by the time of the F8 mark.
