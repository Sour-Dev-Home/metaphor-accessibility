# The game's window procedure turns off the Sticky, Toggle and Filter Keys shortcuts while it has focus

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`win32-accessibility-shortcut-keys-microsoft-docs-2026-11-23.md`](../raw-sources/win32-accessibility-shortcut-keys-microsoft-docs-2026-11-23.md)
  (the constants, flags and Microsoft's sample);
  [`arcana-v0.1.0-alpha.3-2026-11-16.md`](../raw-sources/arcana-v0.1.0-alpha.3-2026-11-16.md) (Arcana swallows these
  calls in Persona 3)
- **Functions:**
  - GameWindowProc | 0x10c2880 | .ecode | Settings | known only | The game window's message handler; on `WM_ACTIVATE` and `WM_DESTROY` it sets the Sticky, Toggle and Filter Keys settings.
  - SavedStickyKeys | 0x2f13eac | .debug | Settings | known only | The `STICKYKEYS` the game restores; `0x2f13eb4` (`TOGGLEKEYS`) and `0x2f13ebc` (`FILTERKEYS`) follow it.

`METAPHOR.exe` imports `SystemParametersInfoA` (not `W`) and, in analysed code, calls it from one function, its window
procedure, with only `SPI_SETSTICKYKEYS` (`0x3B`), `SPI_SETTOGGLEKEYS` (`0x35`) and `SPI_SETFILTERKEYS` (`0x33`). It
follows Microsoft's "Disabling Shortcut Keys in Games" sample: while the window is active, each feature that is off
loses its shortcut; on deactivation, minimising or destruction, the saved settings are set back.

## Evidence

- **One importer, one caller.** Of the shipped binaries (`METAPHOR.exe`, `crash_handler.exe`, `libcurl-x64.dll`,
  `steam_api64.dll` and four PhysX DLLs) only `METAPHOR.exe` contains the text `SystemParametersInfo`, and only as
  `SystemParametersInfoA`. Ghidra resolves no symbol `SystemParametersInfoW`. The import slot is at `0x17c0f88`
  (`.udata`); its only callers in analysed code are 8 indirect calls, all in `0x10c2880`.
- **A window procedure.** `0x10c2880` takes four arguments and switches on the second as a message number (`0x2`,
  `0x6`, `0x100`, `0x101`, `0x201`, ...). Its own references are data only (`0x26b6314`, `0x26b6324` in `.udata`;
  `0x14cbe7d8` in `.xcode`), as a procedure registered by address would be.
- **The actions.** Each call sets `ecx` (the action) and `edx` (the size) with `lea`: `0x3B` with size 8, `0x35` with
  size 8, `0x33` with size `0x18`, matching `STICKYKEYS`, `TOGGLEKEYS` and `FILTERKEYS`. No `SPI_GET*` action is called
  from analysed code.
- **When.** The calls sit in two cases of the function's message switch:
  - `0x2` (`WM_DESTROY`): sets all three back from the saved structures (`0x10c333a`, `0x10c3351`, `0x10c3368`).
  - `0x6` (`WM_ACTIVATE`): when wParam's low word is `WA_INACTIVE`, or its high word says minimised, it sets the saved
    structures back (`0x10c32c3`, `0x10c32da`, `0x10c32f1`). Otherwise, for each structure whose `dwFlags` bit `0x1`
    (the feature's "on" flag) is clear, it clears bits `0x4` and `0x8` (`*_HOTKEYACTIVE`, `*_CONFIRMHOTKEY`) on a copy
    and sets the copy (`0x10c320d`, `0x10c323a`, and the shared call at `0x10c32f1`). A feature that is on is left alone.
- **What a player loses.** While the game has focus, pressing Shift five times, or holding Num Lock or right Shift for
  eight seconds, does nothing if that feature was off when the game saved its settings. Each set writes the whole saved
  structure, so a feature the player switched on outside the game after that save is switched off again when the game
  regains focus, and set back to the saved state when it loses it. [NEEDS VERIFICATION: by ear in the running game.]
- **Not found.** Where the saved structures are filled. Analysed code only reads them (`global 0x2f13eac`,
  `0x2f13eb4`, `0x2f13ebc`: reads and `lea`s, all in `0x10c2880`), so the `SPI_GET*` calls, or whatever fills them, are
  probably in code Ghidra doesn't analyse (`.text1`). [NEEDS VERIFICATION]

## Blocking these calls

A guard would hook either `SystemParametersInfoA` in `user32.dll` or the import slot at `0x17c0f88`. The hook loader
installs only hooks whose address comes from a signature scan of the game module
([ADR 0006](../adr/0006-hooks-through-reloaded-shared-hooks.md)), and neither target is a game function found that way,
so a guard needs a decision first (#821). Hooking the window procedure instead can't skip only these calls.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23. The game
wasn't run. Section bounds from the exe's PE section table, read as data.

1. `batch`: `callers SystemParametersInfoW` (no such symbol), `callers SystemParametersInfoA`.
2. `disasm 0x10c2880`, read from `0x10c31d6` to `0x10c3368` for each call's `ecx`, `edx` and `r8`.
3. `batch`: `decompile 0x10c2880` (the switch cases), `global 0x2f13eac`, `global 0x2f13eb4`, `global 0x2f13ebc`,
   `global 0x2f0d540`, `callers 0x10c2880`.
4. PowerShell `Select-String -Encoding ascii -Pattern 'SystemParametersInfo[AW]'` over every `.exe` and `.dll` in the
   game folder.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
