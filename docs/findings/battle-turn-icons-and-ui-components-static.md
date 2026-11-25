# Turn icons sit in the static game-data block, and the battle UI components hang off one type-keyed registry

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`battle-state-session6-2026-11-17.md`](../raw-sources/battle-state-session6-2026-11-17.md),
  [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md),
  [`menu-values-session5-2026-11-17.md`](../raw-sources/menu-values-session5-2026-11-17.md)
- **Functions:**
  - GetPlayerUnitID | 0x90a9f0 | .ecode | Battle | known only | Script function that jumps to the party unit getter. | finding; docs/findings/battle-target-cursor-index-static.md
  - UUIC_Battle_PressTurnIcon open | 0xf1bc10 | .ecode | Battle | known only | Stores the turn-icon side and computes the icon total. | finding; docs/findings/battle-target-cursor-index-static.md
  - UUIC_Battle_PressTurnIcon refresh | 0xf1bcc0 | .ecode | Battle | known only | Refreshes the turn icons from the same fields as the open. | finding; docs/findings/battle-target-cursor-index-static.md

Static prep for #287, read in Ghidra only; nothing here has run against the game yet, so every meaning below is a
candidate for tonight's inspector session. `Package@btl` itself has **no static pointer path**: its owner is the
`"battle"` task, created in `.text1`, and no global holds it. The turn icons, party HP and the UI components do have
static paths, below.

## Evidence

### G, the game-data block

`G` is the pointer at RVA `0x272e310`. Its value in the file is `base+0x2ca66f0` (in `.debug`), and `global 0x272e310`
lists 150 instructions, none a write, so `G` is constant. `GetMoney`, `GetPlayerUnitID` and the turn-icon getters
all read through it.

| Candidate | Where | Evidence | Confidence |
|---|---|---|---|
| Turn-icon side | `G+0x25460`, `int` | `0x3b0f80` returns it; `UUIC_Battle_PressTurnIcon`'s open (`0xf1bc10`) and refresh (`0xf1bcc0`) store it at component `+0x28` and test it for 1. Set by `0x3b4190`, called from `Participate::vf20` (`0x1ab9a0`) | High that the icon UI reads it; side meaning is a lead |
| Turn icons A | `G+0x25464`, `short` | `0x3b0f10`; set by `0x3b4170` (callers: `Participate::vf20`, `0xc9580`, `0xc9680`) | High read by the UI; full icons is a lead |
| Turn icons B | `G+0x25466`, `short` | `0x3b0ed0`; set by `0x3b4130`, same callers | Half icons is a lead |
| Turn icons C | `G+0x25468` when side is 0, else `G+0x2546a` | `0x3b0ee0`; set by `0x3b4140`, `0x3b4180` | Meaning `[NEEDS VERIFICATION]` |
| Icon total | component `+0x24` = B + 2 × (A + C) | `0xf1bc10` and `0xf1bcc0` compute it | High; reads as half-icon units |
| Party unit n | `G+0x228 + n × 0x16A8` | `GetPlayerUnitID` (`0x90a9f0`) jumps to `0x3b0eb0`, which returns it. Session 5 measured the same stride | High |
| Party HP, MP | unit `+0x0C`, `+0x10` | Session 6 checked them against two frames | Confirmed at phase changes; per action is `[NEEDS VERIFICATION]` |

Elapsed player turns is `base+0x27293b0+0x14`, a static object of its own: `BtlGetElapsedPlayerTurn` reads it, as
`battle-state-session6` and its leads say.

### R, the UI component registry

The pointer at RVA `0x2ca3248`, call it `R`, is null in the file and holds an MSVC `unordered_map` from a `type_info`
to a UI component. Each getter (`0xdc7f0` for `UUIC_Battle_PressTurnIcon`, `0x220250` for `UUIC_Battle_Damage`,
`0xc5ce0` for `UUIC_Battle_Message`) hashes the type with `__std_type_info_hash`, masks it with `R+0x1b8`, takes the
bucket's last node from `[R+0x1a0] + bucket × 0x10 + 8`, walks back through each node's `+8`, compares the `type_info`
at node `+0x10`, and returns node `+0x18`. The list head is `R+0x190`.

| Component | `type_info` at | FNV-1a 64 of its name, low 16 bits | Getter |
|---|---|---|---|
| `UUIC_Battle_PressTurnIcon` | `0x2bb9068` | `0xc04d` | `0xdc7f0` |
| `UUIC_Battle_Damage` | `0x2bb9288` | `0xb65f` | `0x220250` |
| `UUIC_Battle_Message` (action banner) | `0x2bb8f78` | `0x2b2f` | `0xc5ce0` |
| `UUIC_Battle_Command` (wheel) | `0x2bb8f40` | `0x4451` | `0x2bb8f48` is loaded in `0xc8c90` |
| `UUIC_Battle_PartyPanel` | `0x2bb90a0` | `0x08a6` | lookups in `0x1c4eb0`, `0x3231a0`, `0xf173b0` |
| `UUIC_Battle_TacticalSituation` | `0x2bb9028` | `0xa70f` | not looked for |

The hash is FNV-1a 64 over the decorated name without its leading `.` (for example `?AVUUIC_Battle_Damage@ui@app@@`).
That this is what `VCRUNTIME140`'s `__std_type_info_hash` does is from memory `[NEEDS VERIFICATION]`; tonight's `rtti`
on the component confirms or refutes it.

`UUIC_Battle_TargetCursor` (vtable `0x2250280`) is not looked up through `R` in analysed code (`global 0x2bb9480`
finds nothing). The targeted enemy stays `[NEEDS VERIFICATION]`, with `Package+0x788` and `+0x7A8` as session 6 left them.

### Damage popups: one function, for a listener

`0xf173b0` (`UUIC_Battle_Damage`, `(component, request*)`) receives every damage popup. It's called from the battle
GUI's result handler `0x3231a0` through `0xf17150` (a number), and `0xf17250`, `0xf172a0`, `0xf172f0` (no number, sent
when a result short is `0x100`, `4` or `0x200`). The request has the target at `+0x0`, a kind 1 to 8 at `+0x8`, the
negated HP change at `+0xC`, flags at `+0x24` and `+0x25`, and a mode at `+0x2C`. `0xf17250` sends kind 7. Which kind
is Weak, Resist or Miss is `[NEEDS VERIFICATION]`. The inspector can't see a call, so this is for the next listener,
not tonight.

`Participate::vf20` (`0x1ab9a0`) sets all three icon counts and the side, and touches the battle info at `0x1ad955`:
the action-resolution step, the hook candidate for attacker and action.

## Tonight

Deploy a Debug build with `opt-in-inspector.txt`
([how-to](../contributors/how-to/inspect-the-running-game.md)). `follow` reads the pointer and then adds the offset,
so `follow base+0x272e310 0x25460` is `G+0x25460`.

1. **Field, before a fight:** `pwsh scripts/dev/session.ps1 inspect 'follow base+0x272e310 0x25460' 'follow base+0x2ca3248 0x1b8'`.
   The first should print `base+0x2ccbb50`. Then `read` 8 bytes at the second address to get the mask `m`.
2. **Player turn start, 4 icons:** `read <G+0x25460> 12`, then `watch <G+0x25460> 12`. Expected: side, then A = 4.
3. **After a weakness hit (a half icon):** `changes`. Expect A down by one and B up by one. Repeat after Pass, a miss
   and on the enemy turn, noting the side.
4. **Component check, any player turn:** bucket offset = `(0xc04d & m) × 0x10 + 8`. Run
   `follow base+0x2ca3248 0x1a0 <offset> 0x18 0x0`, then `rtti` on the result. Expect
   `UUIC_Battle_PressTurnIcon@ui@app`, which confirms the hash and the path. If it names another class, add `0x8` before
   `0x18` to step one node back. Then `read <component>+0x24 8`: the total and the side.
5. **Party HP after damage:** `read base+0x2ca7fcc 8`: HP and MP of unit 1 (`G+0x228+0x16A8+0xC`). `watch` it and
   take one hit.
6. **Ambush fight:** repeat step 2 at the first enemy turn. The side value with no player turn first is the ambush
   lead.
7. **If the battle listener from #294 is deployed:** its `start pkg=0x…` record gives the package. Then
   `read <pkg>+0x780 0x30` while moving the target cursor (`+0x788`, `+0x7A8`), and `rtti` on each pointer there.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project from `pwsh scripts/dev/ghidra.ps1 build` on the
build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `decompile 0x90a510`, then `0x3b0c30`: `GetMoney` reads `G+0x22c58`. `global 0x272e310`: no write. The 8 bytes at
   file offset `0x272e310 - 0x2729000 + 0x2727000` (`.debug`'s virtual and raw starts, from the PE section table) hold
   `0x142ca66f0`.
2. `callers 0xc8c90` and `decompile 0xc9ac0`: the battle task and its `Package` at task `+0x48`. Decompiling
   `0x211220` and `0x20b3c0` turns up no global that stores the package.
3. `decompile 0xdc7f0`, `0x220250`, `0xc5ce0`, `0xc5e60`: the registry lookup. `global 0x2bb9070`, `0x2bb9290`,
   `0x2bb90a8`, `0x2bb9480`: who looks each component up. The `type_info` addresses are where each class's decorated
   name sits in `.debug`, less 0x10.
4. `callers 0xdc7f0`, `decompile 0x2bc810` (calls at `0x2bd8ae` to `0x2bd944`), `decompile 0xf1bc10` and `0xf1bcc0`,
   then `0x3b0f10`, `0x3b0ed0`, `0x3b0ee0`, `0x3b0f80`.
5. `offset 0x25464`, `0x25466`, `0x25468`, `0x25460`, then `callers` of `0x3b4170`, `0x3b4130`, `0x3b4140`,
   `0x3b4180`, `0x3b4190`.
6. `callers 0x220250`, `decompile 0x3231a0` (calls at `0x323f94` and after), `0xf17150`, `0xf17250`, `0xf173b0`.
7. `decompile 0x90a9f0`, then `0x3b0eb0`: the party unit address.
8. FNV-1a 64 (basis `0xcbf29ce484222325`, prime `0x100000001b3`), computed in PowerShell and checked against the known
   values for `a` and `foobar`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled. Calls that land there (task creation, `0x1514c0120`) were
noted and not followed.
