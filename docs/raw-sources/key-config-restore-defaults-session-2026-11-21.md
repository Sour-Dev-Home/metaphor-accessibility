# Key Configuration: which press opens which yes/no dialog, from one by-ear run (#562)

- **Source:** the mod's `message-log.txt` from the maintainer's by-ear run on 2026-11-21 at 18.43 on the combined
  session Debug build (ten PRs on main, NVDA; 749,574 lines), with the maintainer's comment on PR #683 of that day.
  Re-read with grep, sed and awk (Git for Windows). The log holds game text, so it stays local; only labels, button
  masks, flags, class names and line numbers are kept, plus three labels' text, quoted to name them. Line numbers are
  the log's own.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. The awk of `settings-confirm-dialogs-session-2026-11-21.md`, step 1: at each first cursor on
   `YesNoChoiceSample@ope@ui@app` after another screen, print the screen before it, the last `input pad0` record and
   whether a `select-window` record came in the 200 lines before.
2. `sed -n` over the records between each press and the dialog's first cursor.
3. awk over the system message dump for lines holding "default", with the label above each.

Button names are the pad mask's, from `pad-and-keyboard-state-2026-11-16.md`: `0x0008` Start; `0x0800` is not named
there.

## Dialogs opened while Key Configuration held the cursor

The page is `ConfigMenuChoiceKeyConfig@ope@ui@app` (cursor `flags=0x46` or `0x42`, `count=23`).

| Line | Press | Select window before | Prompt |
|---|---|---|---|
| 249737 | `0x0800` | yes, line 249728 | `System_message_PC_40` |
| 257547 | `0x0800` | yes, line 257538 | `System_message_PC_40` |
| 265736 | Start (`0x0008`, line 265722) | none | none |
| 267525 | Start (`0x0008`, line 267511) | none | none |

- `System_message_PC_40` is "Binding overlaps with an action in `<V 0>`. Overwrite?": the `0x0800` dialogs are the
  binding-overlap question, which the select window already names. They aren't Restore Defaults.
- The Start dialogs open as the four pages' do: no select window, the press the only record before them. The maintainer
  heard Key Configuration's Restore Defaults dialog from the menu key as Yes/No alone (PR #683's comment).
- No B press opened a dialog on this page in this run.

## The label

No `resolve` or `pane` record near lines 265736 and 267525 names a label. The dump holds `System_Option_64` "Return to
default settings?" (line 62054) and the key configuration's `System_message_PC_19` "Key configurations will be restored
to their default settings. Proceed?" (line 123259). Which the dialog draws is `[NEEDS VERIFICATION]` (by ear).
