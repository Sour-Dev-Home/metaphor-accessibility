# Menu value getters behind the script functions: what is hookable (#155)

- **Source:** our reading of the game exe on disk, as shipped (ADR 0002: nothing patched, unpacked or run), starting
  from the script functions' addresses in `menu-values-community-leads-2026-11-17.md`. Tools: a throwaway .NET 9.0
  reader on Iced 1.21.0 (disassembly, call sites, byte pattern counts in the executable sections), not committed; and
  `tools/Metaphor.SignatureCheck --probes` with `menu-value-probes.json`.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Offsets and function addresses only (ground rule 4). Names in the second column are ours.

## Findings

**Most registered script functions are thin wrappers around a native getter.** Five of those natives are plain code
in `.ecode` with a unique pattern, so the menus' own calls to them can be observed. Whether a menu calls them at all is
`[NEEDS VERIFICATION]` until a run; the UI may read the same fields directly.

| Script function (RVA) | Native, our name | What it does |
|---|---|---|
| `GetMoney` (0x90a510, `jmp`) | `0x3b0c30` `GetMoney` | `mov rax,[0x272e310]; mov eax,[rax+0x22C58]; ret`: money is an `int` at `+0x22C58` of the object the global at `0x272e310` points to. |
| `GetBagItem` (0x907810, `call`, then `movzx eax,al`) | `0x3bcb10` `GetBagItem` | Dispatches on `itemId >> 12` through a table of 7 per-category functions with `itemId & 0xFFF`; the count is a byte. |
| `GetPlayerUnitID` (0x90a9f0, `jmp`) | `0x3b0eb0` `GetPlayerUnit` | Returns `[0x272e310] + 0x228 + index * 0x16A8`: an array of per-member units, `0x16A8` bytes each. |
| `UnitGetParam` (0x912a50, `call`) | `0x3e6680` `UnitGetParam` | `(unit, index)`; reads the unit's `+0x4` (a kind, compared with 2) and the four `int`s at `+0x1628`…`+0x1634`. The return width is `[NEEDS VERIFICATION]`. |
| `UnitGetAffinity`, `UnitGetAffinityType` (0x9129b0, 0x9129d0) | `0x3e45a0` `UnitAffinity` | Reached through `0x3e4570`, which passes `(unit, [unit+0x94] as ushort, element, 0)`: the unit's archetype ID is a `ushort` at `+0x94`. Returns a `short`. |

- `CharaGetEquip` (0x903ec0) gets the unit, then jumps to `0x3e5f40`: `movzx eax,dx; mov eax,[rcx+rax*4+0x1628]; ret`.
  The equipped items are 4-byte IDs at unit `+0x1628` by slot, the same fields `UnitGetParam` reads. 11 bytes; not
  hooked.
- `HumanParamGetLevel` (0x90aca0) calls `0x3b3610`, which reads a `ushort` at `[0x272e310] + 0x24668 + id * 2`
  and counts thresholds up to 5: a rank of 1 to 5, likely a royal virtue, not a character's level
  `[NEEDS VERIFICATION]`.
- These jump into `.text1`, which wasn't read: `GetBattleParty` (`0x3b0e30`), `UnitGetLevel`, `UnitGetMaxHP`
  (`0x3e62f0`), `CharaGetClassID` (`0x38a190`), `GetClassLevel` (`0x38a540`), `SetMag` (`0x3b3e70`),
  `UnitGetEquipWeaponParam` and `UnitGetEquipArmorParam`. Level, HP, archetype rank and Mag stay open.

## The listeners (Debug, opt-in)

With `opt-in-menu-values.txt` in the mod's folder, one listener per native above observes it after it returns and logs
`value fn=<name> args=<decimal,…|none> result=<decimal> caller=0x… call=0x… via=… frames=…`. A getter called every
frame logs once per change of result, and again after 1 s without a call with the same arguments. Unit arguments are
addresses; `GetPlayerUnit` records map them to a member index.

## Steps

1. Disassemble each script function's RVA from the leads, and the native it calls or jumps to.
2. Count each native's leading bytes in `.ecode` and `.text1`, with RIP and call displacements as `??`, until one match.
   `GetBagItem`'s opening matches 11 dispatchers that way, so its pattern keeps its first `lea` displacement, which
   ties it to this build's layout.
3. `--probes <exe> tools/Metaphor.SignatureCheck/menu-value-probes.json`: 5 of 5 matched once.
