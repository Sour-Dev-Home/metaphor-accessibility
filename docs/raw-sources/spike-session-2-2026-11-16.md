# Spike session 2: spoken choices, menu focus, input and the scan order (issues #82, #93, #58, #105)

- **Source:** four runs on the maintainer's machine through Reloaded-II 1.31.0, Debug mod from branch
  `spike-prep-session-2` (main with #124's spoken choices, plus the listeners from
  `docs/raw-sources/input-route-2026-11-16.md` and `docs/raw-sources/ui-focus-candidates-2026-11-16.md`): the signature
  spike (Release); `scripts/listen.ps1` twice (the first attempt without the controller connected, then with it); and a
  launch through Reloaded-II with the `opt-in-ui-text-panes.txt` opt-in. Logs and the transcript hold game text, so they
  stay local; only labels, counts, indexes and colours are kept.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. Signature spike over the 27 probes in `tools/Metaphor.SignatureCheck/*-probes.json`, merged into one file.
2. Part B, Steam Input off, controller connected before launch: `listen.ps1` (`hooks 17/17`); the maintainer arrowed
   through the title menu, pressed each controller button and some keys, loaded a save, turned NVDA on, spoke to the
   NPC whose choice was silent in session 1, and pressed each button during dialogue.
3. Part C, Steam Input on: launched through Reloaded-II with the opt-in (`hooks 18/18`), watched a minute, arrowed
   through the title menu, pressed each button.

## Results

- **#105 confirmed.** All 26 single-match probes match once in memory at their disk address, including
  `CompileMsgFile`, `MsgRequestByName` and `MsgRequestSelectByName`, which the Release build hooks (`hooks 3/3` in the
  smoke test). `criAtomExPlayer_Start` still matches 3 times (known, unhooked).
- **#82 works in the game.** The transcript has the NPC's prompt line, then `Choose: <option 1>; <option 2>`, once per
  time the choice opened, and the maintainer heard "Choose" and the first option by ear with NVDA. The second option's
  text is only `......`, which the screen reader reads as nothing, so it sounded silent. Speaking focus as it moves
  between options is #95, not built.
- **#93: a shared menu cursor.** `CursorManagerInput` logged 107 records in part B and 41 in part C, all from one cursor
  class (`0x22045e8`), with an `index` that followed the maintainer's arrowing (0, 2, 1, 0, …; part C: 0, 1, 0, 1, 2, …),
  at the title menu (about 20–80 s after start) and again later in play (about 130–190 s). Which screens use this class
  beyond the title menu is `[NEEDS VERIFICATION]`.
- **#93: text panes.** With the opt-in, `TextPaneDraw` logged 1,396 draws in part C with two colours:
  `0x4220000044372ab8` (1,306) and `0x42080000441144a0` (85), plus 5 unset. That the second is the highlighted item is
  `[NEEDS VERIFICATION]`. The hook in `.text1` ran for the whole run with no crash or visible effect.
- **#58: input.** Steam Input off: 235 records from pad 0 and 23 from the keyboard. Steam Input on: the physical
  controller still reads as pad 0 (87 records), and a virtual pad 1 changed on almost every frame (239,941 records), so
  a repeat-key hook must ignore it or pick the right pad. Free during dialogue, by the maintainer's test: the d-pad, X,
  both bumpers and both triggers; the other buttons open menus or act.
- **Process note:** after the game was ended by force, small `METAPHOR` processes (25–29 MB, parent already gone)
  appeared and exited within seconds, without the maintainer starting anything; likely the game's Steam relaunch stub
  `[NEEDS VERIFICATION]`.
