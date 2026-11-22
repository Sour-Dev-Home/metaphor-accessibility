# Battle's item list: the names it fetches as it scrolls, from the by-ear run (#247, PR #646)

- **Source:** the mod's `message-log.txt` from the maintainer's by-ear run of 2026-11-22 (session build with #631 and
  #646, Debug, PR #646's comments of that day), re-read with grep, sed and awk. The log holds game text, so it stays
  local; only serials, flags, class names, indices, counts and line numbers are kept. Line numbers are that log's.
- **Saved:** 2026-11-22.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -n` for ` cursor ` records whose screen is `BTL_ItemSelect@ope@ui@app` (143 of them), keeping the moves
   (`cmd=0x14` down, `0x15` up, not the settled `cmd=0x1`).
2. `sed -n` over lines 1003300–1010000 for ` resolve ` and ` cursor ` records, keeping names (`flags=9`) and dropping
   descriptions (`flags=7`) and `flags=2`.

## What the log shows

- **Cursor fields.** On this list `top` is the row within the view and `row` the rows scrolled: on the 14-item tab
  `top` climbs 0–7 at `row=0` (lines 1003859–1005759), then stays 7 while `row` goes 1, 2, 3 (lines 1005902, 1006167,
  1006283), and going back up `top` falls 6–0 at `row=3` (lines 1006752–1009895). `index` is their sum. The view holds
  8 rows.
- **Fetch before settle.** Each move's names resolve in the move's millisecond (`cmd=0x14` or `0x15`), before the
  settled input (`cmd=0x1`) some 8 ms later, and the moves are 200–2000 ms apart.
- **The first page.** The tab's 8 names, serials 5173–5179 and 5181, resolve in one millisecond (at 1983419 ms)
  before its cursor settles at `index=0` with `count=14`.
- **Scrolling down.** To `index=8` (`row=1`) one name resolves, 5182. To `index=9` (`row=2`) seven: 5175–5179, 5181,
  then 5183, the rows from 2 to 9 without row 8's 5182. To `index=10` (`row=3`) one, 5184. Each time the last name is
  the row the scroll brought in.
- **Moving up without scrolling.** The fetches go on: 5182 alone at `index=9`; 5176–5179, 5181, 5183 at `index=8`;
  5184 at `index=6`; and the whole view's 8 names at `index=6` and `index=5`. None is the focused row's alone.
- **Other tabs.** Switching tabs moves the same manager's cursor to `index=0` with another `count` (11, 5, 14) after the
  new tab's page resolves, 800 ms or more after the last input.
- No move in this run scrolled up, and the skill list wasn't scrolled.

## Reading

The mod took every name fetched more than 250 ms after the list's last focus as the start of a new page from row 0.
Each scroll's fetch came that long after the last settled input, so from `index=8` on the rows were renumbered from the
view's redrawn names: `index=9` and `10` had no name (silent), and at `index=6` (line 1008198, the view's 8 names
fetched from row 3) it said row 9's name, Thrilling Tintinnabulator, for Heartwarmer, as heard.
