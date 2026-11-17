# Title menu and its System submenu in capture session 5 (#210)

- **Source:** capture session 5 (#229) on the maintainer's machine: Debug mod from branch `capture-1` at `219282c`
  through Reloaded-II, `hooks 31/31`, Steam Input on, `opt-in-screen-class.txt` in place. Read from the session's
  `message-log.txt` (`cursor`, `screen`, `cue` and `input` records; formats in `src/Metaphor.Core/MessageLog.cs` on
  `capture-1`) with grep and awk, against the maintainer's narration. The log and narration hold game text, so they
  stay local; only classes, indexes, flags and times are kept. Log time is video time + 51 s.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Findings

**Nothing logged tells the System submenu from the title menu: same cursor manager, same owner class, same flags.**
`[NEEDS VERIFICATION]` for a `TitleChoice` field or an item count that does.

- Every cursor input from the title menu and from the submenu came from one manager (`0x484126c0`, class RVA
  `0x22045e8`) owned by one `TitleChoice@ope@ui@app` object, `fields=none`. 24 `screen` records name `TitleChoice`;
  none name another class until Settings opens.
- Confirm on title index 2 (pad `0x4000`, cue 57) is followed 25 ms later by `cmd=0x1 index=0` on that same manager:
  the submenu's first item. Down then moves it to index 1; index 2 never occurs in the submenu.
- Back from the submenu (pad `0x2000`, cue 58) is followed by `cmd=0x1 index=2`: the title menu again, on its third item.
- Confirm on submenu index 0 opens `ConfigTopChoicePc<ConfigTopChoicePcLayoutInit>` on a new manager (`0x484141f0`);
  backing out of it lands on the title menu at index 2 (`flags=0x66`), not on the submenu.
- `flags` is `0x46` in the submenu and on the first title visits, and `0x66` on the title menu after a return from the
  submenu or Settings (3 inputs). Bit `0x20` is never set in the submenu, but its absence doesn't mean submenu.
- The `CursorInput` record carries no item count, so `n of 2` vs `n of 3` can't be read from it.

| Log time | Input | Cursor record | Reading |
|---|---|---|---|
| 80.2 s | (screen opens) | `cmd=0x1 index=0 flags=0x46` | title, item 1 |
| 82.5, 84.4 s | down ×2 | `cmd=0x16 index=1`, `index=2` | title, items 2 and 3 |
| 88.9 s | confirm | `cmd=0x1 index=0 flags=0x46` | submenu, item 1 |
| 91.6 s | down | `cmd=0x16 index=1` | submenu, item 2 |
| 95.4 s | back | `cmd=0x1 index=2 flags=0x66` | title, item 3 |
| 97.5 s | confirm | `cmd=0x1 index=0 flags=0x46` | submenu, item 1 |
| 101.3, 103.5 s | down, up | `index=1`, `cmd=0x17 index=0` | submenu, items 2 and 1 |
| 103.9 s | confirm | new manager, `ConfigTopChoicePc…` | Settings |
| 110.4 s | back (out of Settings) | title manager `index=2 flags=0x66` | title, item 3 |

## What a fix can rely on today

A state machine over the title manager's inputs: a confirm (cue 57) while the title cursor is on index 2, followed by
a `cmd=0x1 index=0` on the same manager, enters the submenu; a back (cue 58) or a return from Settings leaves it with
the cursor on index 2. Finding the field that holds the submenu state, or the manager's item count, would remove the
dependence on the cue order.

## Steps

1. `awk` over `cursor`, `screen` and `compile` records with log time 20–135 s; the same with `input` (non-zero pad
   masks) and `cue` records, collapsed by kind.
2. Count `screen` records by `ownerClass` over the whole log.
3. Match each input to the cursor record 1–25 ms after it, and to the narration at video 00:28–01:05.
