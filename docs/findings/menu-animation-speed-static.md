# Menu animations play through SpriteStudio players in protected code, with no UI-only speed found

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [movie-pause-open-static.md](movie-pause-open-static.md) and
  [ui-component-open-points-static.md](ui-component-open-points-static.md)
- **Functions:**
  - LayoutStartAnimation | 0x140a6ffd0 | .ecode | Menus | known only | The shared start-an-animation most layouts' open and close (vf20) call; passes straight to 0xa70010
  - LayoutPlayAnimation | 0x140a70010 | .ecode | Menus | known only | Hands the animation to an ss::Player through two calls into protected code
  - FrameDelta | 0x141070ef0 | .ecode | Menus | known only | Returns the float global at 0x2f0d548, the frame time the keyboard repeat counts down by

For #958: speeding up menu animations can't be built safely from what is readable. Every layout's open and close
animation starts through one function (`0xa6ffd0`), which only hands the animation to a SpriteStudio player
(`ss::Player`) through calls into protected code, so neither a per-animation speed nor a skip is reachable outside
`.text1`. The one time value found, the frame delta at `0x2f0d548`, is shared by the whole game, so scaling it would
speed up gameplay too. Not built; no hook proposed.

## Evidence

- **One starter.** `callers 0xa6ffd0` lists the `vf20` of dozens of layouts (config menu, key config, academia, battle
  panels, item list, result, calendar, camp) and `MoviePauseLayout::vf20` reaches it through `0xa6ffd0` as well
  ([movie-pause-open-static.md](movie-pause-open-static.md)). So a change there would reach every menu at once.
- **Into protected code.** `0xa6ffd0` only calls `0xa70010`. That one calls `0xebfc10` with the player and the
  animation, then wraps a lambda over `ss::Player*` (its `std::_Func_impl` vtable is at `0x2129298`) and calls
  `0x141f340` with it. Ghidra names both callees `thunk_` to functions at `0xf0052a0` and `0x13c5b9a0`, outside the
  analysed sections. Jump stubs into `.text1` are off limits for hooks ([ADR 0010](../adr/0010-text1-hooks-stay-opt-in.md)),
  and what the player does with a speed, if it has one, is `[NEEDS VERIFICATION]`: not readable.
- **The frame delta.** `0x1410ee300`, the keyboard manager, subtracts `FUN_141070ef0()` (the float at `0x2f0d548`) from
  its repeat timers each frame. That it is the frame time every system reads is our reading of its use there; a value
  shared that widely can't be scaled for menus alone. Rule 5 (single-player only) isn't in question; safety is.
- **What would be needed.** A UI-only speed would need either the `ss::Player` update readable (it isn't), or a
  per-layout switch that skips straight to the animation's end, which nothing analysed exposes.

## Steps

Ghidra 12.2 with JDK 25.0.5+8, the shared project from `pwsh scripts/dev/ghidra.ps1 build` on the build above,
2026-11-24, as `pwsh scripts/dev/ghidra.ps1 batch` files: `decompile 0xa6ffd0`, `callers 0xa6ffd0`,
`decompile 0xa70010`, `disasm 0xa70010`, `decompile 0x10ee300`, `decompile 0x1070ef0`. Nothing in `.text1`, `.xcode`
or `.xtext` was decompiled. The three functions' sections are from
`dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> <rva> 4` on each.
