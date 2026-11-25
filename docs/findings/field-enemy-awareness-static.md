# A field enemy's AI state is a `u16` index into 40 named states on its controller, and a discovery sets a bit on `fldWorld`

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`field-nearby-unit-names-static.md`](field-nearby-unit-names-static.md),
  [`field-nearby-session6-2026-11-17.md`](../raw-sources/field-nearby-session6-2026-11-17.md),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)
- **Functions:**
  - fldCharaCtrl_Enemy::vf3 | 0x6567b0 | .ecode | Field | known only | Builds the enemy controller's 40 AI state objects on its stack. | finding
  - fldCharaCtrl_Enemy::vf4 | 0x658bc0 | .ecode | Field | known only | Reads the current state index and calls that state's callback. | finding
  - FldEnemySetAIState_Discover | 0x8dcb10 | .ecode | Field | known only | The discovery request: finds the enemy and sets its discover state. | finding
  - FldPlayerGetDegToEnemy | 0x8e5070 | .ecode | Field | known only | Script function giving the angle from the player to an enemy. | finding
  - FldEnemyGetDegToPlayer | 0x8dbe70 | .ecode | Field | known only | Script function giving the angle from an enemy to the player. | finding
  - FldEnemyIsDamageBackAttack | 0x8dc1b0 | .ecode | Field | known only | Script function that tells a back attack on an enemy. | finding

For #186 through #178. Each `fldEnemy` (list `M+0x18`/`+0x20`) points to its controller `C = [enemy+0x3110]`, a
`fldCharaCtrl_Enemy` whose `+0x50` points back to the enemy. The controller's setup builds 40 states of `0x100` bytes
into a vector at `C+0x2d0`; the update reads the current one as `[C+0x2d0] + u16[C+0x2e8] × 0x100`. A state change is
requested by writing the next index to `u16 C+0x2ec` and setting the byte `C+0x2ee`. The exe holds 40 state names
together (`Idle`, `Wait`, `Discover`, `Approach`, … `Lost`, `LostWarn`, `LostHome`, … `Vigilance`, …), so reading
`C+0x2e8` on each enemy should tell patrolling from chasing. Which index is which name is not settled: the names'
order doesn't fit the one index seen (below). Not read in a running game: every value is `[NEEDS VERIFICATION]`.

| Value | Where | Confidence |
|---|---|---|
| Controller | `C = [enemy+0x3110]`, RTTI `fldCharaCtrl_Enemy` (vtable `0x18072a0`); `[C+0x50]` = the enemy | High in code |
| Current state | `u16` at `C+0x2e8`; 40 entries of `0x100` bytes from `[C+0x2d0]` | High in code |
| Requested state | `u16` at `C+0x2ec`, pending flag byte `C+0x2ee` | Medium |
| State names | 40 strings from `0x18073f4` (after `StandardInitialize`), in `.udata` | High that they exist, low for their order |
| "Discovered" request | `FldEnemySetAIState_Discover` requests index 30 (`0x1e`) when `int C+0x58` is 3 and `C+0xb0` is 0 | Medium |
| A discovery anywhere | bit `0x80` of the `u32` at `[base+0x2ee7510]+0x8`, set by the same request | Medium; when it clears is not found |
| Enemy update | `fldCharaCtrl_Enemy::vf4` `0x658bc0` (reads the current state) | `.ecode`, matches once |

## Evidence

- **The state table.** `fldCharaCtrl_Enemy::vf3` (`0x6567b0`) builds 40 `0x100`-byte state objects on its stack, each
  from three or four `std::function` callbacks of `fldCharaCtrl_Enemy` (a thunk into `.text1` at `0x750bd60` packs each),
  and pushes them one by one into the vector at `C+0x2d0` (`lea rbx,[rdi+0x2d0]` at `0x6582af`, a loop of `0x28`).
  `vf3` tests `u16 C+0x2e8` against 0 before one branch, and its end sets `C+0x2ec = 0xf` and `C+0x2ee = 1` when an
  enemy-data flag is set.
- **The current state.** `fldCharaCtrl_Enemy::vf4` (`0x658bc0`) reads `movzx ecx, word [rdi+0x2e8]`, shifts it left by
  8 and adds `[rdi+0x2d0]` (`0x6591b5`–`0x6591c0`), then calls the entry's callback at `+0xf8`.
- **The discovery request.** `FldEnemySetAIState_Discover` (`0x8dcb10`) finds the enemy by `+0x9f8` and calls
  `0x6622f0(enemy, 1)`, which takes `C = [enemy+0x3110]`, returns unless `int C+0x58` is 3 and `C+0xb0` is 0, then
  (in `0x656230`) sets `C+0x346`, clears `C+0x10`, `+0x18` and `+0x30`, sets bit `0x80` of `[fldWorld]+0x8`, sets
  `C+0x2ee` to 1 and `C+0x2ec` to `0x1e`, and with argument 1 sets `C+0x344`. What `C+0x58` = 3 means is not known.
- **The names.** A string block in `.udata` (`0x18073e0`, file offset `0x18060e0`) holds `StandardInitialize` and then
  40 names: `Idle Wait Discover Approach Navi Attack AttackWait AttackWalk Patrol PatrolWait CirclePatrol
  CirclePatrolWait Standby StandbyAnim Lost LostWarn LostHome Attract Escape SearchTurn AttackDiscover Avoidance
  Vigilance Warp Backstep WarpDuring SwordEnd Noise InNavi Dive Appear SwordStart Witness Vanish VanishPatrolWait Gaze
  PersonalAction ForceAttack InitTurn NoiseAttack`. Forty matches the loop count. Taken in this order, the discovery's
  index 30 would be `Appear`, which doesn't fit; so either the order differs or `0x1e` is a state that leads into
  `Discover`. `vf3` passes `StandardInitialize` to a `.text1` thunk, but no analysed code references `Idle` (`0x18073f4`)
  or `Discover` (`0x1807408`), so how the names map to indices is `[NEEDS VERIFICATION]`.
- **For #186.** `FldPlayerGetDegToEnemy` (`0x8e5070`, already in `field-probes.json`), `FldEnemyGetDegToPlayer`
  (`0x8dbe70`) and `FldEnemyIsDamageBackAttack` (`0x8dc1b0`) name the facing and back-attack ideas #186 needs; not
  read here.

### Signatures

Each matches once with `tools/Metaphor.SignatureCheck --probes` (a scratch probes file, not committed):

| Name | RVA | Pattern |
|---|---|---|
| `fldCharaCtrl_Enemy::vf4` | `0x658bc0` | `48 8B C4 48 89 58 08 56 57 41 56 48 81 EC D0 01 00 00 C5 F8 29 70 D8 C5 F8 29 78 C8 C5 78 29 40 B8 C5 78 28 C1 48 8B F9 48 8B 05 ?? ?? ?? ?? F6` |
| discovery gate | `0x6622f0` | `48 8B 89 10 31 00 00 83 79 58 03 75 0D 83 B9 B0 00 00 00 00 0F 84 ?? ?? ?? ?? C3` |

Reading `C+0x2e8` for each enemy in the list needs neither; they are there if a per-enemy event hook is ever wanted.

## What would settle it

With the inspector (`pwsh scripts/dev/session.ps1 inspect`), in the first dungeon: `follow base+0x2ee7510 0x20 0x18 0x0
0x3110 0x0` for the first enemy's controller `C`, `rtti <C>` (`fldCharaCtrl_Enemy`), then `read <C>+0x2e8 8` while
the enemy patrols, after it notices the player, while it chases and after it gives up. Note each index beside what the
screen showed; four or more distinct values that repeat with the behaviour settle the order. `follow base+0x2ee7510
0x0` gives the world; `read <world>+0x8 4` before and after a discovery shows the `0x80` bit and when it clears.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project of the build above, Windows 11 x64,
2026-11-21. Each line is one `pwsh scripts/dev/ghidra.ps1` query unless it says otherwise.

1. `decompile 0x8dcb10`, `decompile 0x6622f0`; `offset 0x346` (the writers in `0x6584f0`, `0x656230` and others);
   `decompile 0x6584f0`.
2. `vtable app::fld::fldCharaCtrl_Enemy`; `decompile 0x6567b0` (the state loop and its end); `offset 0x2d0`;
   `disasm 0x658bc0`.
3. A byte search of the exe for state-like words (`Discover`, `Patrol`, `Lost`, `Caution`, …), then the printable
   strings around the cluster at file offset `0x18060e0`; RVA from the section table. `global 0x18073f4`,
   `global 0x1807408` (none found).
4. Bytes: `tools/Metaphor.SignatureCheck --bytes <exe> 0x6622f0 0x30` and `0x658bc0`; the patterns with `--probes`.

Nothing in `.text1` was decompiled; the thunks were noted as calls and not followed. No decompiled output is quoted.
