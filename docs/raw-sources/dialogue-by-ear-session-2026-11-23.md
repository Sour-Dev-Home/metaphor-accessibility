# Choice timing, the reader toggle and name entry keys, from the by-ear session of 2026-11-23 (#814, #808, #807)

- **Source:** the maintainer's by-ear session of 2026-11-23, mod `0c122d3` Debug with `opt-in-testing.txt`: its
  `message-log.txt`, `transcript.txt` and live notes. The logs hold game text and a typed name, so they stay local; only
  labels, times, key and button codes are kept here.
- **Saved:** 2026-11-23.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## A voiced choice: the select window call comes with the prompt (#814)

New game's first choice, voiced, with "Voiced lines" saying the name only:

- 3819544 ms: `select kind=0 first="MES_e00_002_001_004" second="MES_e00_002_001_005"`, then
  `select-window kind=0` with the same labels, then `request via=MsgWindowShowLine kind=0` for the prompt, all in the
  same millisecond. The transcript says "Choice, 2 options" at 3819544 ms, over the voice.
- 3828225 ms: the first `cursor` record, `screen=CommonTalkChoice@ope@ui@app count=2 index=0`, and the first option
  in the transcript at 3828224 ms: 8.7 s after the window call. The maintainer heard the options when they showed.
- An unvoiced choice (Sunshade Row): the prompt and "Choice, 2 options" at 3420802 and 3420803 ms, the first option at
  3421202 ms.

So the select window call marks when the choice starts, not when its options show; the first cursor does. That the
voice's end is what shows them is `[NEEDS VERIFICATION]`.

## The reader turned off in the field without being heard (#808)

- 2909535 ms: `input pad0 buttons=0x0c00 handed=0x0c00` (LB and RB together, the reader chord), with the transcript's
  "Dialogue reader off" at 2909534 ms. The report log has no speech failure. The chord was handed to the game, and
  in the same millisecond the field's layouts (`FieldPlayerInfoLayout@ui@app`, `FieldMapLayout@ui@app`) start
  animations; a cue (id 26) follows at 2909574 ms.
- No read in the session holds T (`0x54`) in the field. The only T presses (`held=54`) are at 3343149 ms, in a menu,
  turning the reader on ("Dialogue reader on" at 3343148 ms), and at 3772333 ms.
- So the "silent" off came from the pad chord, not T, and the mod did say it. Why the maintainer didn't hear it is
  `[NEEDS VERIFICATION]`; the chord also reaching the game is #811.

## Name entry: the screen's updates cover the typing (#807)

- 3702480 ms: the first `name-entry player=1 chars=0`, before any letter; then `chars=1` to `chars=5` from 3702758 to
  3703404 ms. The transcript says "Dialogue reader off" at 3703401 ms, between `chars=4` and `chars=5`, and "No dialogue
  to repeat" at 3712604 ms.
- The update hook runs every frame while the screen takes text (`docs/findings/name-entry-static.md`), so the mod's
  "name entry up" (an update within the last second) is true from before the first letter.
