# Battle's Order menu and its Tactics submenu, from the by-ear session (#771)

- **Source:** the mod's `message-log.txt` and `live-notes.md` from the maintainer's by-ear session of 2026-11-23 (mod
  `0c122d3`, Debug), F8 mark 8 (1100859 ms), re-read with grep and awk. The log holds game text, so it stays local;
  only serials, flags, labels, class names, indices, counts, times and line numbers are kept. Line numbers are that
  `message-log.txt`'s.
- **Saved:** 2026-11-23.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -n "screen=BTL_OrderT"` for the cursor records, counted by screen and count with `sort | uniq -c`.
2. `grep -n -E "serial=20(6[89]|7[0-9]) "` for the Order menu's row serials.
3. `awk` over 1080000–1101000 ms for ` resolve ` and ` cursor ` records, without descriptions (`flags=7`).

## What the log shows

- **The screens.** Cursor records name `BTL_OrderTop@ope@ui@app` with `count=3` (19 records, first at line 475315) and
  `BTL_OrderTactics@ope@ui@app` with `count=6` (31 records, first at line 535532). The maintainer's notes: the Order
  menu is the one LB opens in a battle, its rows Party, Tactics, Escape; the Tactics submenu's rows Let them decide,
  Fight to kill, Prioritize attacks, Conserve MP, Prioritize health, Attack normally, both by ear and sight.
- **The Order menu's names.** Opening it resolves serials 2068, 2069 and 2070 with `flags=8` and `label=(unknown)`
  (lines 475304–475306 at 644604 ms, again at 1041536 and 1059141 ms): the mod knows no file holding them, so it has no
  text for them.
- **The Tactics names.** A move down from row 0 (1096406 ms) and one up to row 3 (1098521 ms) each fetch six names with
  `flags=9`, in this order: serials 1537, 1536, 1538, 1539, 1540, 1541, labels `System_Battle_Tactics_02`, `_01`,
  `_03`, `_04`, `_05`, `_06` (lines 537147–537157 and 537966–537976). Their texts, in that order, are the six rows the
  maintainer listed, in his order.
