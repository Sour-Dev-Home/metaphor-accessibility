# Settings pages: which serials resolve as a page opens and moves, from the by-ear run (#560)

- **Source:** the mod's `message-log.txt` from the maintainer's by-ear run on 2026-11-20 (Debug mod build from `main`
  with #578 and #582, as in #558's comment; the Reloaded-II mod folder, written 21:24 local time), re-read with grep and
  awk. The log holds game text, so it stays local; only serials, flags, class names, indices, counts and times are kept.
- **Saved:** 2026-11-20.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -a -n` for `ConfigMenuChoicePcT` and `ConfigMenuChoiceKeyConfig` in the log.
2. The `resolve`, `cursor` and `input` records around each page's first `cursor` record, printed with awk as time,
   serial, flags and the cursor's `index`, `top` and `count`.

## What it shows

Times are milliseconds since the game started. This run opened System, Sound and Key Configuration; no Graphics or
Display (`$0HI`, `$0HJ`) record is in the log.

- **System** (`?$ConfigMenuChoicePcT@$0HG@@ope@ui@app`, `count=20`). Its first cursor is at 52798 ms. Just before it, at
  52780 ms, 14 serials resolve with `flags=9`, in this order: 1636, 1652, 1639, 1655, 1640, 1656, 1726, 11706, 1713,
  1714, 1644, 1660, 1645, 1661. The first move (index 0 to 1, 55515 ms) resolves the same 14 again in the same order.
  Moves to index 2 (56415 ms) and 3 (57297 ms) resolve none.
- **Sound** (`?$ConfigMenuChoicePcT@$0HH@@ope@ui@app`, `count=6`). Its first cursor is at 62985 ms. Before it, at
  62965 to 62966 ms, 13 serials resolve: 11661, 1699, 11662, 1700, 11663, 1701, 1717, 1718, 11660, 11717, 11801, 11664
  and 11721, all with `flags=9` except 11801 (`flags=8`). The move to index 3 (64999 ms) resolves the same 13 again in
  the same order. The other moves resolve none.
- **Before every category-list open** (46746, 51314 and 62932 ms), serials 1706, 1707, 1708 and 1712 resolve with
  `flags=8`, then 11469 to 11473 with `flags=9`.
- **Each move logs two `cursor` records:** `cmd=0x14` with the new index (`result=20` after button `0x0040`,
  `result=21` after `0x0010`), then `cmd=0x1` with the same index about 17 ms later. `top` equals `index` on every
  record.
- **Key Configuration** (`ConfigMenuChoiceKeyConfig@ope@ui@app`, `class=0x22046a8`, `count=23`, `flags=0x46`). Its
  first cursor is at 67968 ms, index 0. Each of two presses of button `0x0040` (68383 and 69216 ms) moves the index by
  2: 0 to 2, then 2 to 4. Before the first cursor, at 67950 to 67951 ms, serials 11745 to 11747 resolve with
  `flags=6`, 11604 to 11608 with `flags=8`, and 12 serials from 11508 to 11564 with `flags=9`.
