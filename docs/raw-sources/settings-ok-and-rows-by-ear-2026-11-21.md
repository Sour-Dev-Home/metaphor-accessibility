# Settings pages: the OK button's cursor flags and the System rows' order, from the by-ear run (#560)

- **Source:** the mod's `message-log.txt` from the maintainer's by-ear run on 2026-11-21 (`session.ps1 run`, Debug mod
  build 57add22 from #648, `-OptIn ui-text-panes`, 999 s, hooks 28/28; PR #648's comment of that day), re-read with
  grep and awk. The log holds game text, so it stays local; only labels, flags, class names, indices, counts and line
  numbers are kept. Line numbers are that log's.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `grep -n` for ` cursor ` records whose screen is `?$ConfigMenuChoicePcT@$0H?@@ope@ui@app` (the four pages), counted
   by `flags`.
2. Each record with `flags=0x43`, printed with the cursor records before and after it (command, index, flags, page,
   count).
3. For the walks, awk over lines 169409–175300 and 305000–308900: for each cursor record, the labels of the `resolve`
   records since the one before it.

## The OK button

- Of the pages' 635 cursor records, 618 have `flags=0x47` and 17 have `flags=0x43`.
- 11 of the 17 are settled inputs (`cmd=0x1`) at `index=0`, one on each page visit that went past the last row. Each
  follows a down move (`cmd=0x14`, `index=0`, `flags=0x47`) from a settled cursor on the last row (`index=count-1`):
  for example 172147 → 172310 → 172327 (System, 20 rows) and 187396 → 187448 → 187461 (Sound, 6). The pages seen
  this way: System 20 and 24 rows, Sound 6, Graphics 7 and 6, Display 4.
- The other 6 are moves off it (`cmd=0x14` or `0x15`); the next settled cursor has `flags=0x47` again: up gives the last
  row (172626 → 172661, index 19), down gives row 0 (187516 → 187531, index 0 on both sides of OK).
- No `0x43` record comes anywhere else: not on a page's opening cursor nor on an up move to row 0 (175259, `0x47`),
  which are row 0. On the other screens in this log (the category list, the title, the quick menu, a yes/no) every settled
  cursor has bit `0x4` set (`0x46`, `0x47` or `0x66`).
- So bit `0x4` clear on a settings page is the OK button, as heard (Enter on it closes the page). Whether the bit means
  the same on other screens, and which message names the button (`System_Option_88` is a candidate), is
  `[NEEDS VERIFICATION]`.

## The System rows

Each move resolves the focused row's name label, then its help line's label, alongside rows being redrawn. On the down
walk the last name resolved before each settled cursor is the focused row's; on the up walk at `top=0` it is the
first. O_ is `System_Option_`, S_ is `System_subject_name_PC_`.

- **Title, 20 rows.** Down walk 169424–172147 (rows 7–19) and up walk 174189–175259 (rows 12–0) agree on rows 7–12:
  O_14, O_16, O_17, O_103, O_89, O_21, O_22, O_71, O_27, O_23, O_112, O_72, O_24, O_73, O_25, O_91, S_04, S_05,
  S_07, S_64. Rows 13–19 come from the down walk only; the up walk's moves there scrolled and resolved the whole
  window.
- **In play, 24 rows.** Down walk 305947–308859, one name and help pair per move for rows 7–22: O_89, O_21, O_22,
  O_71, O_27, O_23, O_112, O_72, O_24, O_73, O_25, O_28, O_91 (with O_102 before it, as on the title page), S_04,
  S_05, S_07; row 23 resolves O_25, O_28 then S_64. That is the title's rows 4–19 with O_28 at row 18. Rows 0–6 were
  only seen as the page's fetch at 305804 (O_14, O_16, O_17, O_18, O_19, O_20, O_103 with their help lines), so their
  order is `[NEEDS VERIFICATION]`.
- **Sound, Graphics, Display** don't scroll, so only their fetch order is seen (PR #648's comment):
  `[NEEDS VERIFICATION]`.
