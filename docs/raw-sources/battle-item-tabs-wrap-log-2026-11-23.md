# Battle's item list: tab switches and wraps on the Magic items and HP recovery tabs (#737)

- **Source:** the mod's `message-log.txt`, `report-log.txt` and `transcript.txt` from the maintainer's by-ear session of
  2026-11-23 (mod `0c122d3`, Debug; `live-notes.md` of that session: names silent after the wrap on the Magic items and
  HP recovery tabs, fine on Ailment cures and Food), re-read with grep and awk. The log holds game text, so it stays
  local; only serials, flags, class names, indices, counts, times and line numbers are kept. Line numbers are that
  `message-log.txt`'s.
- **Saved:** 2026-11-23.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -E " flags=9 "` over the whole log, then the gap in ms between each two name fetches, counted with
   `awk | sort -n | uniq -c`.
2. `awk` over 919100–922500 ms and 958000–993000 ms for ` resolve ` records with `flags=9` and ` cursor ` records of
   `BTL_ItemSelect@ope@ui@app`, leaving out the settled `cmd=0x1` inputs; the same windows of `report-log.txt` for the
   settled inputs and its `silent change=` lines, and of `transcript.txt` for what was said.

## What the log shows

- **Bursts.** Names of one fetch come 0 to 6 ms apart (one gap of 34 ms); the shortest gap between two fetches is
  100 ms (step 1).
- **Magic items (40 rows).** From index 5 of the 14-row tab, the 40-row tab's first page, 8 names, resolves at 919528 ms
  (lines 523040–523055) and its cursor settles at `index=0`, said "1 of 40" with its name. The wrap up (`cmd=0x15`,
  `index=39 top=7 row=32`) fetches 8 names at 919799–919800 ms (lines 523169–523177), 271 ms after the first page,
  then the same 8 at the next move up but one (921904 ms, lines 523943–523964). The transcript says only "40 of 40",
  "39 of 40", "38 of 40". The mod times its run gap (250 ms) with `Environment.TickCount64`, whose steps are coarser
  than the log's clock, so the wrap's names fell inside the first page's run `[NEEDS VERIFICATION]`: the cause is read
  from the code, not logged.
- **HP recovery (11 rows).** From the Food tab (9 rows) at `index=0`, the 11-row tab's first page, 8 names, resolves at
  961484 ms (lines 530563–530583) and its cursor settles at `index=0 count=11` (line 530598): `report-log.txt` has no
  focus line for it and nothing is said, as only the count changed. The wrap up (`index=10 top=7 row=3`) fetches 3
  names at 962279 ms (lines 530872–530878), the rows it brought into view (9 to 11) in list order, and is logged
  `silent change=FocusMoved ... index=10 count=11` (report line at 962288 ms).
- **Ailment cures (14 rows), for comparison.** Its wrap up from `index=0` (914559 ms) fetches the whole view again,
  8 names ending at the last row, two of them rows already known.
