# Settings pages: which press opens which yes/no dialog, from two by-ear runs (#562)

- **Source:** the mod's `message-log.txt` from the maintainer's two by-ear runs on 2026-11-21 on PR #648's Debug mod
  build (`session.ps1 run`, `-OptIn ui-text-panes`): run A at 17.00 (the run of
  `settings-ok-and-rows-by-ear-2026-11-21.md`, 360,001 lines) and run B at 17.38 (331,268 lines), with the maintainer's
  comments on #562 of that day. Re-read with grep, sed and awk (Git for Windows). The logs hold game text, so they stay
  local; only labels, button masks, flags, class names, indices and line numbers are kept, plus the two labels' text,
  quoted to name them. Line numbers are each log's own.
- **Saved:** 2026-11-21.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. awk over each log: for each `input pad0` record with a non-zero `buttons=`, keep the mask; for each ` cursor `
   record, keep its `screen=` and `flags=`. At each first cursor on `YesNoChoiceSample@ope@ui@app` after another
   screen, print the screen before it, that screen's last `flags`, and the last non-zero mask; then the next screen
   after the dialog.
2. `sed -n` over the records between each such press and the dialog's first cursor.
3. `grep -n "System_Option_6[34]"` over run B, and the `compile` record before the hit.

Button names are the pad mask's, from `pad-and-keyboard-state-2026-11-16.md`: `0x2000` B, `0x4000` A, `0x0008`
Start.

## Which press opened each dialog on a settings page

Every dialog that opened while one of the four pages (`?$ConfigMenuChoicePcT@$0H?@@ope@ui@app`) held the cursor:

| Run, line | Page | Press | Cursor before | Opens on | After the dialog |
|---|---|---|---|---|---|
| A 185456 | System | B | `flags=0x47` | index 0 | category list |
| A 202639 | Graphics | A | `flags=0x43` (OK button) | index 0 | category list |
| A 217959 | Sound | B | `0x47` | index 0 | category list |
| A 312678 | System | B | `0x47` | index 0 | category list |
| A 315440 | System | B | `0x47` | index 0 | category list |
| A 317945 | System | B | `0x47` | index 0 | category list |
| A 321845 | System | Start | `0x47` | index 0 | the same page |
| A 323751 | System | B | `0x47` | index 0 | category list |
| A 324975 | System | Start | `0x47` | index 0 | the same page |
| A 325605 | System | B | `0x47` | index 0 | (not followed) |
| A 338925 | Sound | B | `0x47` | index 0 | category list |
| B 193714 | System | B | `0x47` | index 0 | (not followed) |

- Between each press and the dialog's first cursor there is only a sound cue record (`cue id=29`), 10-17 ms; no other
  press, cursor or resolve record (A 312660-312678, 321828-321845, 323735-323751).
- B on a page with nothing changed goes straight to the category list with no dialog (A 190896 → 190938, 224352 →
  224383, 345811 → 345845).
- Every dialog record is `count=2 flags=0x46`; nothing in them tells the two apart.
- B on a sub-screen opened from a page also opens one: A 354918, from `ConfigBrightnessChoice@ope@ui@app` (Graphics),
  back to the Graphics page after. Its question is unknown. B on `SingleChoiceBase@ope@ui@app` (a value list) goes back to
  the page with no dialog (A 336213, 344674).
- The maintainer heard the B dialog as the save-changes question and the Start one as Restore Defaults (comments on
  #562). The A-on-OK dialog went to the category list after Yes, as the B ones did.

## The labels

Run B's dump of `EN/message/system/System_Message.msg` (`compile id=6`, line 47704) holds `System_Option_63` "Save
settings?" (line 51578) and `System_Option_64` "Return to default settings?" (line 51585). Neither label appears in a
`resolve` or `pane` record of either run near a dialog, so that the dialogs draw these two messages is
`[NEEDS VERIFICATION]` (by ear).

## Not checked

- Keyboard: run A has one non-zero keyboard read (line 292846), none on a settings page, so which keys open the dialogs is
  `[NEEDS VERIFICATION]`.
- Whether Start opens the dialog with nothing changed, and whether A on OK with nothing changed opens one.
