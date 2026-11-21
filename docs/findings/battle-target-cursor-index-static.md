# The target cursor is told the target's slot index through one virtual function; it has no cursor manager

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md),
  [`battle-state-session6-2026-11-17.md`](../raw-sources/battle-state-session6-2026-11-17.md),
  [`menu-values-session5-2026-11-17.md`](../raw-sources/menu-values-session5-2026-11-17.md)

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

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-20.

1. `vtable 0x2250280`; `decompile 0xf26ec0` and `0xf27a50`.
2. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0xf26ec0 96`: the jump and the two
   constants. Then `--probes <exe> tools/Metaphor.SignatureCheck/battle-turn-probes.json`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
