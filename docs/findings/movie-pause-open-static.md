# The cutscene skip menu opens through its component's request handler, on a request of 1

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; the by-ear evidence is the maintainer's report on #809 (session of 2026-11-23 evening,
  mod 4d936b2 Debug)
- **Functions:**
  - MoviePauseRequest | 0x140f737c0 | .ecode | Menus | hooked | UUIC_Common_MoviePause::vf5: shows the skip menu's layout on a request of 1, else closes a shown one | finding
  - MoviePauseLayout::vf20 | 0x140d08510 | .ecode | Menus | known only | The skip menu layout's close: starts animation 4768 through the shared start-an-animation | finding

The "Skip?" menu a 2D cutscene opens on Start or Menu is shown by its component's request handler,
`UUIC_Common_MoviePause::vf5` (`0xf737c0`, `.ecode`, a whole function, not a jump stub): on a request holding the byte 1
it shows the layout with no animation; on any other request it closes a shown layout, which plays animation 4768. That
matches the by-ear log, where 4768 came on every close and the open logged no `ui-anim`. That a request of 1 comes on
each open is our reading, `[NEEDS VERIFICATION]` by ear.

## Evidence

- **The component.** `UUIC_Common_MoviePause` has vtable `0x2254530`, written by `0xf734e0` and by its slot 0; its
  slot 5 is `0xf737c0`, slot 9 `0xf73830` (true when the layout's slot 28 reads false), slot 13 an empty function. That
  the layout it holds at `+0x18` is a `MoviePauseLayout` (vtable `0x21baf10`, written by `0xd06480` and its slot 0) is
  `[NEEDS VERIFICATION]`: nothing analysed stores it, but the close below lands on that class's slot 20.
- **The request.** `0xf737c0(this, request)` decodes the request through `0xf484a0`, the decoder
  [ui-component-open-points-static.md](ui-component-open-points-static.md) found for `EventItemGet`: a has-value byte at
  request `+0x40`, a type at `+0x38` checked against the `type_info` at `0x2bb9378` (its name is `.D`, MSVC's `char`;
  a mismatch ends in a no-return call), and the value, one byte at `+0x0`. Read with
  `Metaphor.SignatureCheck --bytes` at `0x2bb9378`.
- **Value 1, the open.** The handler calls the layout's slot 30 with 0 (`fw::ui::LayoutArrayBase::vf30`, `0x7fd9d0`,
  which clears bit 0 of the layout's `+0x8` flags), then jumps to its slot 21 (`LayoutArrayBase::vf21`, `0x7f90a0`,
  which calls slot 30 with 0 again). Neither starts an animation.
- **Anything else, the close.** It asks slot 28 (`LayoutArrayBase::vf28`, `0x7fa2f0`, bit 0 of `+0x8`) and, when set,
  jumps to slot 20, `MoviePauseLayout::vf20` (`0xd08510`), which calls `0xa6ffd0` with animation `0x12a0` (4768) on
  the layout's player at `+0x28`, then passes `0x129a` to `0xa700a0` for the part at `+0x150` (what that does is
  `[NEEDS VERIFICATION]`).
- **By ear.** In the 2026-11-23 evening session every `ui-anim class=MoviePauseLayout@ui@app anim=4768` landed on the
  same millisecond as a back press, and each open (Start, then cue 4) logged none (#809). So slot 20 is this layout's
  close, though [ui-component-open-points-static.md](ui-component-open-points-static.md) reads slot 20 as most layouts'
  open, and the request of 1 is the open.
- **Callers.** No analysed code calls `0xf737c0`: its references are the vtable slot and one from `.xcode`
  (`0x14cb18e4`). How often the game sends a request, once per open or more, is `[NEEDS VERIFICATION]`; the hook's
  Debug log (`ui-request`) records each.

### Signature

`MoviePauseRequest`: the whole function, `0xf737c0` to its `ret` at `0xf7382f`, with the call's displacement
wildcarded; one match on disk (`tools/Metaphor.SignatureCheck/ui-component-probes.json`). It starts `40 53` (push rbx),
not `E9`, so it is no jump stub into `.text1` ([ADR 0010](../adr/0010-text1-hooks-stay-opt-in.md)). Not yet checked in
memory.

## Steps

Ghidra 12.2 with JDK 25.0.5+8, the shared project from `pwsh scripts/dev/ghidra.ps1 build` on the build above,
2026-11-23. Each line is one `pwsh scripts/dev/ghidra.ps1 batch` file.

1. `vtable app::ui::UUIC_Common_MoviePause`, `vtable app::ui::MoviePauseLayout`.
2. `decompile` of `0xf737c0`, `0xf738c0`, `0xf73830`, `0xd08510`, `0xd10990`, `0xd0ab90`, `0xd0b6a0`, `0xd0e430`.
3. `decompile` of `0xf484a0`, `0x7f90a0`, `0x7fd9d0`, `0x7fa2f0`; `disasm 0xf737c0`; `callers 0xf737c0`, `0xd08510`;
   `global 0x2254530`, `0x21baf10`.
4. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes "$METAPHOR_GAME_EXE" <rva> <n>` on
   `0xf737c0` (128 bytes, which also names the section) and `0x2bb9378` (32 bytes); then `--probes` with
   `ui-component-probes.json`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
