# The target cursor is told the target's slot index through one virtual function; it has no cursor manager

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md),
  [`battle-state-session6-2026-11-17.md`](../raw-sources/battle-state-session6-2026-11-17.md),
  [`menu-values-session5-2026-11-17.md`](../raw-sources/menu-values-session5-2026-11-17.md)
- **Functions:**
  - BattleTargetCursorFocus | 0xf26ec0 | .ecode | Battle | hooked | `UUIC_Battle_TargetCursor` slot 4: focuses target slot `index`. | finding
  - UUIC_Battle_TargetCursor vtable | 0x2250280 | .udata | Battle | hooked | 14 slots; slot 4 at `+0x20` is swapped (#253). | finding

`UUIC_Battle_TargetCursor` (vtable `0x2250280`) holds no `CursorManager`, so the shared cursor hook never sees it
(#247, 2026-11-20). Its slot 4, `0xf26ec0` (`BattleTargetCursorFocus`, our name), takes `(this, uint index)` and
passes it to its layout: that index is the targeted slot. Which enemy a slot is, and who calls slot 4, are still
`[NEEDS VERIFICATION]`. Static only.

## Evidence

- **Slot 4.** Thirteen bytes: it loads the layout at `[this+0x18]` and, if set, jumps to a shared layout function
  (`0xaea700` by the jump's displacement). The decompiler inlines that function: when `index < [layout+0x68]` it calls
  virtual `+0xA0` on entry `index` of the array at `[layout+0x60]` (entries `0x6820` bytes); otherwise it calls it on
  the array's base. So slot 4 focuses entry `index`, one entry per possible target.
- **Its neighbours.** Right after slot 4 sit `0xf26ed0` (same layout, virtual `+0xA0` with no index), and two
  functions returning 6 and 10: likely the party and enemy slot counts, which fits the leads note's list of up to 10
  entries `[NEEDS VERIFICATION]`.
- **No other state.** Slot 13 (`0xf27a50`) is an empty function, and the component is never looked up through the UI
  registry (`global 0x2bb9480`, the turn-icons note). Slot 4 is called through the vtable, so `callers` lists no
  caller.
- **Two other leads.** Session 5 logged `UnitAffinity` 44 times while targeting: its first argument is a unit, the
  targeted one by how the Weak tag follows the cursor `[NEEDS VERIFICATION]`. Package `+0x7A8` changed while an ally
  was chosen (session 6).
- **Signature.** Slot 4 and its three neighbours, with the jump's displacement wildcarded, match once on disk at rva
  `0xf26ec0` (`tools/Metaphor.SignatureCheck/battle-turn-probes.json`). The function is too short to hook inline; the
  mod would locate it and swap the vtable's slot 4 (`0x2250280 + 0x20`), which is data, not code. That is a new kind
  of patch, so it needs the maintainer's yes and a hook-loader build check like every other.

## The vtable swap (#253)

Added 2026-11-24 for the swap the maintainer decided on 2026-11-23 (#253).

- **Where the slot is.** The vtable is at rva `0x2250280` in `.udata`, which the section table marks initialised,
  readable, not writable (`0x40000040`); slot 4 is the 8 bytes at `0x22502a0`, holding `0x140f26ec0` on disk. The
  8 bytes before the vtable (`0x2250278`) hold `0x14248cfa0`, its RTTI complete object locator, whose class is
  `UUIC_Battle_TargetCursor@ui@app` (Ghidra's `vtable` query). `rtti 0xf26ec0` finds the function in this one vtable
  only, and its only reference is that data slot, so every call to it goes through slot 4.
- **No code to patch, no `.text1`.** `0xf26ec0` and `0xaea700` are in `.ecode`. The vtable's slots 0 and 1 jump into
  `.text1` (`0xf7a63a0`, `0xf7a9930`) and the constructor isn't in analysed code (`global 0x2250280` finds no
  instruction), so the mod can't locate the vtable from a load; it takes the rva for this build and checks it
  (below). Nothing in `.text1` is touched.
- **What the index indexes.** `[layout+0x60]` is a `fw::StaticArray<TargetCursorLayout::MeshInfo, N>`: two such
  vtables follow the cursor's, `N = 6` (`0x2250348`) and `N = 10` (`0x2250320`), and the two constant functions
  after slot 4 are their slot 3. So the index is a cursor mesh, one per possible target position, among 6 or 10.
  That mesh `i` is enemy `i` of the battle package's list is a guess `[NEEDS VERIFICATION]`: six is the encounter
  record's enemy slot count, and the list skips empty ones
  ([order note](battle-enemy-list-order-and-update-hook-static.md)), so a formation with a gap would shift it. Which
  cursor (enemy side or ally side) a call is for isn't known either.
- **What the mod checks before writing.** The loader's build check and the slot-4 signature matching once; then
  that slot 4 holds that signature's address, and that the locator before the vtable names
  `UUIC_Battle_TargetCursor@ui@app`. Otherwise it writes nothing.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-20.

1. `vtable 0x2250280`; `decompile 0xf26ec0` and `0xf27a50`.
2. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0xf26ec0 96`: the jump and the two
   constants. Then `--probes <exe> tools/Metaphor.SignatureCheck/battle-turn-probes.json`.

For the swap, 2026-11-24, the same project and build, through `scripts/dev/ghidra.ps1 batch <file>` (4 runs, 22
queries):

3. `vtable 0x2250280`, `global 0x2250280`, `disasm 0xf26ec0`, `rtti 0xf26ec0`; `disasm 0xf26e60`, `0xf26f40`,
   `0xf26f20`, `callers 0xf26e60`.
4. `rtti` and `disasm` of `0xf26ed0` to `0xf26f00`, `decompile 0xaea700`, `callers 0xaea700`,
   `vtable app::ui::UUIC_Parts_Base`; `rtti 0xaea700`, `vtable app::ui::TargetCursorLayout`, `vtable 0x2250348`,
   `vtable 0x2250320`.
5. `--bytes <exe> 0x2250278 0x78`: the locator pointer and the 14 slots, slot 4 `0x140f26ec0`; `--probes` again:
   `BattleTargetCursorFocus` one match at rva `0xf26ec0`.
6. The section table from the exe's PE header: `.ecode` `0x1000`-`0x17c0000`, `.udata` from `0x17c0000`
   (`0xf69000` long, characteristics `0x40000040`).

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.

## In memory, 2026-11-24

Same build, Debug mod build `fe67a150`, by-ear session setup. The swap first failed to install: every check above
matched in memory (locator `0x14248cfa0`, slot 4 `0x140f26ec0`, class `.?AVUUIC_Battle_TargetCursor@ui@app@@`, image
base `0x140000000`), then `WriteProcessMemory` returned false with error 998 (`ERROR_NOACCESS`). `VirtualQueryEx` gave
the slot's page `PAGE_READONLY` (`0x2`), image memory. Making the page writable with `VirtualProtect` for the write
and putting `PAGE_READONLY` back (#253) installs it; the game ran on with no fault.

7. `scripts/dev/session.ps1 inspect base`, `read 0x142250278 0x30`, `read 0x14248cfa0 0x18`, `read 0x142bb9488 0x30`.
8. `VirtualQueryEx` on `0x1422502a0` from a separate PowerShell process (query access only).
9. A Debug build that put the swap result and `GetLastPInvokeError` in the install error (not committed), then the fix:
   the loader log reads `hook BattleTargetCursorHook installed at module offset 0xf26ec0`.
