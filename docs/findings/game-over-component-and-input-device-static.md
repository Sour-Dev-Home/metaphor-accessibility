# The game-over screen is UUIC_Common_GameOver, and its button glyph follows the game's own input-device global

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
  (`Gameover_CreateUI`, `Gameover_Open`); builds on [pad-icon-glyph-tables-static.md](pad-icon-glyph-tables-static.md)
- **Functions:**
  - Gameover_CreateUI | 0x140907690 | .ecode | Dialogue | known only | Script function: finds or makes `UUIC_Common_GameOver` in the UI registry. | finding
  - Gameover_Open | 0x1409077f0 | .ecode | Dialogue | known only | Script function: opens the component through `0xf5eb40`. | finding
  - UUIC_Common_GameOver::vf13 | 0x140f5eea0 | .ecode | Dialogue | known only | The screen's update: its message, then the wait for input. | finding
  - InputDeviceGet | 0x1407adec0 | .ecode | Settings | read | Returns the input-device global at `0x1427a8bc0`. | finding; src/Metaphor.Hooks/InputDeviceRead.cs
  - ConfirmInputRead | 0x1407ad220 | .ecode | Settings | known only | Reads the decide inputs and sets the input-device global to 0, 1 or 2 by which device gave them. | finding

For #778. The screen is the UI component `UUIC_Common_GameOver@ui@app` (vtable `0x2252e48`) with the layout
`GameOverLayout@ui@app` (vtable `0x21ae498`). The game picks keyboard or pad glyphs from one global, `0x27a8bc0`: 0
after pad input, 1 or 2 after keyboard or mouse input. `ActionButtonGlyph` draws a key glyph when it is 1 or 2. The mod
names the key from its own `LastInputDevice` instead, which can disagree with what the game draws; reading the game's
global would make the words match the screen.

## Evidence

- **The component.** `Gameover_CreateUI` looks up `UUIC_Common_GameOver` in the registry at `0x2ca3248` by the
  `type_info` data at `0x2bb92c0` (the decorated name `.?AVUUIC_Common_GameOver@ui@app@@` starts at `0x2bb92c8`), and
  if it isn't there allocates `0x58` bytes, builds it with `0xf5e2a0` and inserts it. FNV-1a 64 of the name without
  its leading `.`, low 16 bits, is `0x287b` (the same hash that gives `UUIC_Battle_Damage` `0xb65f`, as the turn-icon
  note found).
- **The layout.** `GameOverLayout::vf19` and `vf20` start animations `0x114b` to `0x1159`; none of its own functions
  call `ActionButtonGlyph` (`0xa7d710`) or `PadMaskGlyph` (`0xa7cd40`), whose callers are listed by
  `callers 0xa7d710`. Which part draws the "continue" glyph is `[NEEDS VERIFICATION]`: `ActionGuideLayout::vf1`
  (`0xd1c980`) is a caller and a lead.
- **The global.** `global 0x27a8bc0`: 45 writes in seven input functions (`0x7abf30`, `0x7ac4c0`, `0x7ac8a0`,
  `0x7acc80`, `0x7ad220`, `0x7ad750`, `0x7aea70`) and one read, `0x7adec0`, which returns it. In `0x7ad220` the
  bindings of four actions (map `0x2ee85d0`, actions `0x20`, `0x1f`, `0xf`, `0xe`, key code at `+0x1c`) tested with
  `0x7b23a0`, or `0x7b3010`, set 1; the same bindings tested with `0x7b2290` set 2; and a pad mask read (`0x7ad620(0xffff)`) sets 0 and refreshes the glyph set (`0xa7eda0`). Which of 1 and 2 is the
  mouse is still `[NEEDS VERIFICATION]`, as the glyph note says; both draw key glyphs.
- **The wait.** `UUIC_Common_GameOver::vf13` calls `0x7ad220(1)` and `0x7ad750(1)`, then `0x7ae520` and `0x7b3390`,
  which reads `0x27a8bc4`, the dword after the global. The game-over is waiting on the same input functions that set it.

## Live check

Debug session with `opt-in-inspector.txt`, at the game-over screen.

```text
[game-over-keyboard]
# After pressing a key: expect 01000000 (or 02000000).
read base+0x27a8bc0 8
[game-over-pad]
# After pressing a pad button: expect 00000000.
read base+0x27a8bc0 8
```

If the keyboard read gives 1 while the mod still said "Press A", the mod's `LastInputDevice` is the fault and the fix
is to read `0x27a8bc0` (build-pinned) for the game-over words, nonzero naming the key.

**Result** (by-ear session 2026-11-23, Steam build 18330018, mod `0c122d3` Debug, inspector reads of
`base+0x27a8bc0`, reported on #778): with Button Display set to a fixed type it read 1 and stayed 1 after a pad press;
with Button Display Auto it read 0 after pad input and 2 after an arrow key. The next dword stayed 1 throughout. So
under Auto the global follows the device (0 pad, 2 keyboard), and 1 doesn't name one. The mod reads it through
`InputDeviceGet`'s load (`0x7adec0`, `mov eax,[rip+disp32]`), whose signature matches once on disk
(`tools/Metaphor.SignatureCheck --probes`, 2026-11-23).

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23.

1. `vtable` `app::ui::UUIC_Common_GameOver`, `app::ui::GameOverLayout`.
2. `decompile` `0xf5ea70`, `0xf5e9a0`, `0xf5eea0`, `0xca9ff0`, `0xcaa4c0`, `0xcaa400`, `0xcaa000`, `0xcaa2b0`,
   `0xca9ef0`, `0x9077f0`, `0x907690`, `0x7adec0`, `0x7ad220`, `0x7ae520`, `0x7b3390`.
3. `global 0x27a8bc0`; `callers 0xa7d710`, `0xa7cd40`.
4. The name bytes at `0x2bb92c8` from the exe (`.debug`, file offset = RVA − `0x2729000` + `0x2727000`); FNV-1a 64 in
   PowerShell, checked against `UUIC_Battle_Damage`'s known `0xb65f`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
