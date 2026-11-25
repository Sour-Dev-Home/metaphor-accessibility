# Battle's item list keeps its tab index at +0x44b8; the System tutorial list builds its rows per category from a list read in .text1

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; the classes and counts are from the by-ear sessions of 2026-11-23
  ([`class-name-and-battle-item-tab-serials.md`](class-name-and-battle-item-tab-serials.md))
- **Functions:**
  - BTL_ItemSelect::vf17 | 0x140d932c0 | .ecode | Battle | known only | Sets up the item list: picks the tab holding the last used item, else the first tab with items. | finding
  - SystemTutorialChoice::vf17 | 0x140e0c220 | .ecode | Menus | known only | Builds the tutorial list per category 0 to 5 and enables the category switch when more than one has rows. | finding
  - TutorialsOfCategory | 0x140e06750 | .ecode | Menus | known only | Fills a list with the tutorial IDs whose category equals the one asked for. | finding

For #851 and #806. `BTL_ItemSelect`'s tab index is the `int` at choice `+0x44b8`: its setup writes it, and wraps it
modulo the tab count at `+0x4458`. That tab `n` is `CommandBattle_IT_CATEGORY_0<n+1>` is `[NEEDS VERIFICATION]`: the
tabs come from a list at `+0x4450`, whose values weren't read. `SystemTutorialChoice` lists the tutorials of one
category at a time, from a source list filled in `.text1`, so which tutorial a row is can't be told statically.

## Evidence

- **The tab index.** `BTL_ItemSelect::vf17` sets `+0x44b8` to −1, then for each of the `+0x4458` tabs (an `int` list
  at `+0x4450`) fills a row list and sets `+0x44b8` to the tab whose rows hold the item ID at `+0x4440` (probably the
  last one used). If none did, it sets 0, then steps `+0x44b8 = (+0x44b8 + 1) % +0x4458` until a byte of the list at
  `+0x4498` is set. `offset 0x44b8` finds writers in five other functions (`0xd750d0`, `0xd78170`, `0xd7bb20`,
  `0xd85ad0`, `0xd89c60`), the likely LB and RB handlers `[NEEDS VERIFICATION]`, and readers in `vf14` and `vf56`. That
  fits the log: an opening lands on another tab than the first (count 5 at 2074504 ms, the second tab's count).
- **The tutorial list.** `SystemTutorialChoice::vf17` calls `0xe06750` for each category 0 to 5 (6 is skipped) with
  the choice's list at `+0x1478` (count `+0x1480`); a category with rows is appended to the list at `+0x17c0` (count
  `+0x17c8`), and `+0x180c` counts them. When more than one has rows it registers a second input callback (the
  category switch, `[NEEDS VERIFICATION]` which button). `0xe06750` takes up to 206 tutorial IDs from
  `thunk_FUN_14ec051c0` (in `.text1`, not followed) and keeps those whose `0xf2a630` equals the category, in source
  order. So the 33 rows of 2026-11-23 are one category's or several's, and their IDs are in the choice at `+0x1488`
  on, `[NEEDS VERIFICATION]`: rows 9 to 11 opened tutorials 22 to 24 that session.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23:

1. `vtable app::ui::ope::BTL_ItemSelect`; `decompile 0xd932c0`; `offset 0x44b8`; `offset 0x4460`.
2. `decompile 0xe06750`; `decompile 0xe0c220`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled; `0xec051c0` was noted and not followed.
