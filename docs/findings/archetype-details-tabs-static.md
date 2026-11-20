# Archetype details builds its tab list as kinds 0, 2, 1; the choice has no static path yet

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`archetype-screen-classes-session5-2026-11-17.md`](../raw-sources/archetype-screen-classes-session5-2026-11-17.md),
  [`screen-class-link-2026-11-17.md`](../raw-sources/screen-class-link-2026-11-17.md)

Static prep (#493) for game session 2 (#497), stop 5, for #227. Read in Ghidra only, not yet run against the game.
The [tree note](archetype-tree-scene-and-choice-static.md) found that the tabbed getter `0xd976f0` returns the manager
at `+0x1740`, `+0x1750` or `+0x1760` by the kind at the tab index `+0x1738`. This note adds the order the tabs are built
in, so each on-screen tab has a predicted kind and manager, and a route to the choice by sweep. The values the tabs show
(rank, EXP, bonuses) are in the [menu values note](menu-values-party-unit-static.md), section 6.

| Question | Best lead | Confidence |
|---|---|---|
| Which kind each tab is | Tab 1 kind 0 (`+0x1740`); then kind 2 (`+0x1750`) when unlocked; then kind 1 (`+0x1760`) | High in code; kind 2's manager is by elimination |
| When a tab is missing | Kind 2 needs flag `0x1177` and a count above 1; kind 1 needs flag `0x3941` clear | High in code |
| Reaching the choice | No static pointer; sweep the Status scene (key 4) while details is open | Low |

## Evidence

### The tab list

`ClassDetailBase<CampArchetypeDetailLayout>::vf17` (`0xd926e0`), the setup shared by the three details classes, empties
two arrays: `int` kinds in the object at `+0x1700` (count at `+0x1708`) and bytes in the object at `+0x1720` (count at
`+0x1728`). It then appends:

1. kind 0, always;
2. kind 2 when `0xf33560` is true: flag `0x1177` is set and `0x43d65a0(0x7b)` (in `.text1`, not followed) is above 1;
3. kind 1 when `0xf35fe0` is true: flag `0x3941` is clear.

Each append adds a byte to the second array. Last, when the byte at `+0x173c` is set, it sets the tab index `+0x1738`
to the last tab. Both test flags with `0x3b0ab0`. Its formula, bit `f & 31` of the dword at
`G + 0x22c5c + (f >> 5) × 4`, is assumed from `0x3c83a0`, which tests flag `id + 0x1121` that way inline
(`[NEEDS VERIFICATION]` for `0x3b0ab0` itself). So flag `0x1177` is bit 23 of `base+0x2cc9578` and flag `0x3941` bit 1
of `base+0x2cc9a74`.

So with all three tabs, on-screen tab 1 is index 0, kind 0, manager `+0x1740`; tab 2 is kind 2, `+0x1750` by
elimination; tab 3 is kind 1, `+0x1760`. Session 5 saw three tabs from Equipment and Status, which fits both flags
allowing them on that save. The tabs' names are `[NEEDS VERIFICATION]`: the session names them by ear or by eye.

`offset 0x1738` lists other writers (`0xd79170`, `0xd7ad90`, `0xd89c00`, `0xd98f80`, `0xd99c40`, `0xd9a5e0`), the
likely RB and LB handlers of this and sibling layouts; which is this screen's is `[NEEDS VERIFICATION]`.

### The choice and where it lives

`StatusMenuClassDetailChoice@ope@ui@app` (vtable `0x21e3e68`) is `0x7620` bytes: `0xd54e60` allocates that much, builds
it with `0xdf60c0` and wraps it in a `shared_ptr`. `0xd54e60` is a case of the choice factory `0xd5c6b0`.
`EquipClassDetailChoice` has vtable `0x21dc988`. The factory's one readable caller, `0xd6a5d0`, stores the new choice at
`+0x28` of its own object (the factory case is at `+0x1c`), and is reached only from data near `0x14c9b8ac`, outside
the analysed code. `global 0x21e3e68` finds only the constructor and destructor, so there's no static pointer.

The Status menu's scene is reachable: `0xfd69f0` takes key 4 from the menus' scene map (the tree note's walk) and casts
it to `fclCampStatusMenu@fcl@app`. `fclEquipMenu` and `fclCampSkillMenu` have no cast in analysed code (`global` on
their type descriptors finds none), so details opened from Equipment or Skill has no scene lead. The choice, or a
holder whose `+0x28` is the choice, may hang off the Status scene `[NEEDS VERIFICATION]`. Confidence: low.

A found choice is checked against the log: each `cursor` record's `mgr=` is the manager moving, so on each tab the
manager pointer at the predicted offset must equal the `mgr=` logged while moving on it.

## Tonight

Stop 5 of #497, from Status first (the only entry with a scene lead), with the batch file:

```powershell
pwsh scripts/dev/session.ps1 inspect -Batch docs/sessions/session-2/stop-5-details.txt -WaitKey -SaveLog <out>
```

| Step | Command | The answer that confirms |
|---|---|---|
| 1 | The batch's first section, on tab 1 | `read <mask address> 8`, then `read <node>+0x10 4` is `04 00 00 00` and `rtti <scene>` is `fclCampStatusMenu@fcl@app`. The flag dwords: bit 23 of the first set and bit 1 of the second clear predict three tabs |
| 2 | `read <scene> 0x1000`, `rtti` each heap pointer; for one with no match, `follow <scene+k> 0x28 0x0` (`k` its offset in the dump) then `rtti` | A pointer naming `StatusMenuClassDetailChoice@ope@ui@app` is the choice (rejects the lead if none, at either depth) |
| 3 | `read <choice>+0x1700 0x40` | The count at `+0x1708` is 3, and three `int`s after it read 0, 2, 1 in that order (2 and `0, 1` when kind 2 is locked). Where the array's data starts is `[NEEDS VERIFICATION]` |
| 4 | `read <choice>+0x1738 8`, `read <choice>+0x1740 0x30`, on each tab after RB | `+0x1738` reads 0, 1, 2; the manager pointer at `+0x1740`, `+0x1750`, `+0x1760` in turn equals the log's `mgr=` on that tab |
| 5 | Each tab, by eye | Note the tab's name: that names kinds 0, 2 and 1 for #219 and #160 |

If step 2 fails, the log alone still answers the tab order: three distinct `mgr=` values in RB order, the first being
the one the screen opens on.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project built by `pwsh scripts/dev/ghidra.ps1 build` on
the build above, Windows 11 x64, 2026-11-19. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says
otherwise. Type descriptors were found as the tree note did: the decorated name's file offset in `.debug`, plus
`0x2000`, minus `0x10`.

1. `offset 0x1738`, `offset 0x1700`; `decompile 0xd926e0` (the appends near its end), `0xf33560`, `0xf35fe0`.
2. `vtable app::ui::ope::StatusMenuClassDetailChoice`, `vtable app::ui::ope::EquipClassDetailChoice`;
   `global 0x21e3e68`; `callers 0xdf60c0`; `decompile 0xd54e60`; `callers 0xd5c6b0`; `decompile 0xd6a5d0`;
   `rtti 0xd6a5d0` (none); `callers 0xd6a5d0` (data only).
3. PowerShell over the exe's bytes for the `fcl` class names; `global 0x2c02210` (`fclCampStatusMenu`, one cast, in
   `0xfd69f0`), `global 0x2c009b8` (`fclEquipMenu`) and `0x2c01ba0` (`fclCampSkillMenu`), both none; `decompile 0xfd69f0`.

Nothing in `.text1` was decompiled; `0x43d65a0` was noted and not followed. The class names are the exe's own; no game
text is quoted.
