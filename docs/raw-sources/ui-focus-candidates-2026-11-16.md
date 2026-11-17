# UI focus candidates: the menu cursor manager and the UI text pane (#93, second sitting prep)

- **Source:** our reading of the game exe on disk, as shipped (ADR 0002: a disassembler on the exe as-is; nothing
  patched, unpacked or run). Tools: a throwaway Node.js v26.3.0 script that maps MSVC run-time type information (RTTI)
  to vtables, a throwaway disassembler on Iced 1.21.0 (.NET 9.0.319), and `tools/Metaphor.SignatureCheck` (`--bytes`,
  `--probes`). The scripts aren't committed; the method below repeats them.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Method

1. The exe keeps MSVC RTTI. Each polymorphic class has a type descriptor whose name string reads `.?AV<Class>@<ns>@@`,
   16 bytes into it. A complete object locator holds `1`, then at `+0x0C` the descriptor's RVA and at `+0x14` its own
   RVA; the 8 bytes before a vtable hold the locator's address. So: find the name, then the locator naming it, then
   the pointer to the locator: the vtable follows it.
2. Each vtable slot is an address in `.ecode` (the plain code section; `.text1` is the protected one). Disassemble
   each, and list which vtables share a function.
3. Write a pattern from `--bytes` and check it matches once with `--probes tools/Metaphor.SignatureCheck/ui-focus-probes.json`.

Class names and offsets only are quoted here: identifiers, not game content (ground rule 4).

## Findings

**Menus share cursor classes.** RTTI names a family in `app::ui::ope`: `CursorManagerBase`, `CursorManager`,
`MultiLineCursorManager`, `ComplexCursorManagerNoScroll`, `CursorManager_StateChgOnly`, `FreeCursorManager`,
`CursorManagerSoundParts`, `CursorObjectBase`, and menu "choice" classes built on templates such as
`ChoiceBase_WithNext<Layout>` and `ChoiceBase_NoNext<Layout>` (one per screen), including `TitleChoice`,
`TitleMenuChoice` and `ChoiceBase_NoNext<TitleLayout>`. Which of them the title menu uses is `[NEEDS VERIFICATION]`.

| Class | vtable RVA | Slot 1 | Slot 4 |
|---|---|---|---|
| `CursorManagerBase` | `0x2204578` | `0xe99660` | `0xe98770` |
| `CursorManager` | `0x22045e8` | `0xe99660` | `0xe98770` |
| `MultiLineCursorManager` | `0x22046a8` | `0xe9c7d0` | `0xe98770` |
| `ComplexCursorManagerNoScroll` | `0x2204740` | `0xe99660` | `0xe98770` |
| `CursorManager_StateChgOnly` | `0x21c8d88` | `0xe99660` | `0xd824a0` |

- **Slot 1, `0xe99660`:** `mov eax,[rcx+64h]; add eax,[rcx+60h]; ret`. Read as the cursor's index: a top (scroll)
  value at `+0x60` plus a row at `+0x64`.
- **Slot 4, `0xe98770` (we call it `CursorManagerInput`):** `int (this, const int* command)`. If bit 0 of `[this+0xAC]`
  is set, commands `0x16` and `0x17` return at once. Otherwise it saves `+0x60` and `+0x64`, calls slot 5, 6, 7 or 8
  for commands `0x14`, `0x15`, `0x16`, `0x17` (`0x18` returns `0x18`), then calls `0xe99c30` and `0xe99b40` with the
  manager and a pointer to the move's result, and puts `+0x60` and `+0x64` back if they return false. It returns 1 or the move's result.
  Read as the shared input handler that moves the cursor. Which command is which direction is `[NEEDS VERIFICATION]`.
- Slots 5–8, `0xe99c30` and `0xe99b40` start with a `jmp` into `.text1` (protected), so they weren't read.
  `CursorManagerSoundParts` (vtable `0x2204568`, one slot) suggests the cursor's sound is played by a part attached
  to the manager, perhaps from one of those calls `[NEEDS VERIFICATION]`. It may be why session 1 saw no
  `criAtomExPlayer_SetCueId` cue on the title menu.

**UI text is drawn by panes.** `app::ui::TextPane` (vtable `0x2205688`, 24 slots) holds a font element
(`fw::font::FontElem`, vtable `0x23622b8`). Its slot 21 (`0xebf970`, we call it `TextPaneDraw`) is `void (TextPane*)`:
it reads `[this+0x30]` (8 bytes), `[this+0x38]` (float) and `[this+0x3C]` (32 bits), the flags at `[this+0x0C]`, and the
element at `[this+0x58]`. When the byte at `[this+0x50]` is set it hands `this+0x48` (8 bytes) to `0x1409bb0`, which
swizzles it as four lanes (read as a colour). It then calls `0x1409300` on the element with the float as its second
argument, the 32-bit value as its third and `[this+0x08]` as its fourth (read as the element's draw). `0x1409300` has one other caller in plain
code: `LicenseTextPane` (`app::ui::layout::title::license`), slot 21 `0xd3d820`, a byte-for-byte copy apart from its
call offsets. So `[this+0x30]`/`[this+0x34]` are read as the position and `+0x48` as the colour; where the element keeps
its text wasn't found `[NEEDS VERIFICATION]`. Callers in `.text1` aren't visible to this method.

## Signatures

Both in `src/Metaphor.Core/hook-signatures.json` and `tools/Metaphor.SignatureCheck/ui-focus-probes.json`; one match
each on disk (`--probes`, 2026-11-16). `TextPaneDraw`'s pattern fixes its first call's offset (`E8 19 B9 55 00`),
because without it the licence copy matches too. Neither is checked in memory yet.

## Caller tracing (session 1's `(unknown)`)

`CallerAddress` walked the stack with `RtlCaptureStackBackTrace`, which unwinds each frame by its registered unwind
data. Every walk found no frame in the game module. Why is `[NEEDS VERIFICATION]`; a guess is a frame between hook and
game (the hooking library's stub or the runtime's thunk) without unwind data that moves the stack pointer. The prep
keeps the walk, logs how many frames it captured (`frames=`), and when it finds no game frame, reads the thread's own
stack upward from the hook for values in the module that follow a call (`via=scan`, up to three: `caller=`, then
`more=`). The right one's direct call names the hooked function or a thunk to it; a stale value can come first.

## What each listener logs (to the local `message-log.txt`)

- `cue …` (`CriCueSetListener`, unchanged hook): as before, with `via=walk|scan|none frames=N` and `more=…`.
- `cursor mgr=0x… class=0x<vtable rva> cmd=0x14 result=1 index=3 top=0 row=3 flags=0x… caller=…`
  (`CursorManagerInputListener`): after each handled input, when the command, result or cursor changed for that
  manager. `class=` maps to the table above. Numbers only.
- `pane at=0x… class=0x… x=… y=… f38=… i3c=0x… colour=0x…(set|unset) n08=0x… flags=0x… elem=0x… text=…`
  (`TextPaneDrawListener`, opt-in with `opt-in-ui-text-panes.txt`): each text pane when its draw state changes, with
  the first string a probe finds through the pane's or the element's pointer fields (`text=pane+0x…` or `elem+0x…`,
  else `(none)`). Game text: the log stays local.

## Leads not followed

- `criAtomExPlayer_Start`: not taken further. The three matches are CRI code in `.text1`
  (`ryo-cri-atom-patterns-2026-11-16.md`), and the cursor classes above are a closer lead to the focus.
- The exe holds no `criAtomExPlayer_SetCueName` string; the only `criAtomExPlayer_` name in it is
  `criAtomExPlayer_SetSequencePrepareTime`.
