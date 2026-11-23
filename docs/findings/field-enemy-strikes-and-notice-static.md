# A field enemy's HP, damage handler, defeat and discovery script all sit in readable code; the notice cue's state is not pinned

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  reading the mod's own Debug message log from our sessions
- **Sources:** [`field-enemy-awareness-static.md`](field-enemy-awareness-static.md),
  [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md) ("Real-time field combat"),
  [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md)

For #186, #649 and #696. In code: an enemy's HP is the `u32` at `[enemy+0x34e0]+0xc`, `fldEnemy::vf58` (`0x65d7a0`)
is its field-damage handler, it is defeated when that HP reaches 0, a stagger count in it requests AI state `0x24`,
and the engine runs a level's "enemy discovered" script (`FldEntryEnemyDiscoverScript_Register`, kind 7) from the
enemy's perception test. Which AI state index means "noticed you" or "gave up" is still not settled, so #649's cue is
not built. The Result box and the "Battle!" banner are named components whose text is set in protected code. Nothing
here is read in a running game: every value is `[NEEDS VERIFICATION]` until the checklist below is done.

| Value | Where | Confidence |
|---|---|---|
| Current HP | `u32` at `[enemy+0x34e0]+0xc`: `fldChara::vf14` (`0x645110`) loads `+0x34e0`, `0x3e60f0` returns `+0xc` | High in code |
| Max HP, set HP | `fldChara::vf12` (`0x6453f0`), `fldEnemy::vf13` (`0x662310`) on the same block | Medium |
| Dead | `FldEnemyIsDead` (`0x8dc240`): `vf14` returns 0 | High in code |
| Field damage | `fldEnemy::vf58` (`0x65d7a0`, `.text`): clamps the damage to HP, writes it, posts `fldReportJob::OnDamaging` | High in code |
| Defeat | `0x661210`, once per enemy (byte `+0x36f9`), from `vf58` when HP is 0, `vf41` (`0x65e680`) and `0x6617d0` | Medium |
| Stagger | `vf58` adds to `int enemy+0x37d8`; at a per-enemy threshold it requests state `0x24` unless the state is 5 or 6 | Low: "stun" is a guess |
| Perception test | `0x650070` (controller): `0x64c7d0` and `0x64c560`, or `0x64c660` | Low: named by its use |
| Discover script | kind 7 entries (`0x6b78a0(2,7,..)`), run by `0x6b6c30(2,7,id)` from `0x64fdc0`, `0x64fff0`, `0x653770` | High in code |
| State table order | slot *i* of `C+0x2d0` is the *i*th of 40 calls to `0x64bb20` in `vf3` (`0x6567b0`), in stack order | High in code |
| Result box | `UUIC_Field_Reward` (vtable `0x22595f8`), `FieldRewardLayout` (`0x21a8600`); `vf5` jumps to `.text1` | Text route not found |
| "Battle!" | `UUIC_Common_BattleEncount` (`0x2250ab8`), `UUIC_Wipe_Movie_BattleEncount`, `fldCharaAct_Atk_Encount` | Names only |

## Evidence

- **HP.** `fldEnemy`'s vtable (`0x1807838`) keeps `fldChara`'s slots 12 and 14; both are `mov rcx,[rcx+0x34e0]` and a
  jump, and slot 14's target returns the `u32` at `+0xc`. `vf58` reads slot 14 and 12, clamps the damage, writes the
  new HP with slot 13, and sets its "killed" flag when slot 14 then returns 0. `FldEnemyIsDead` finds the enemy by
  `+0x9f8` in the list at `[[fldWorld]+0x20]+0x18` and returns slot 14 = 0.
- **Defeat.** On "killed" `vf58` calls `0x65f490`, `0x6612c0` (byte `+0x36f8`, a once-only report),
  `0x661210` and the group code `0x6632c0` for each enemy sharing the row's group. `0x661210` sets `+0x36f9` and calls
  `0x6b8bd0`, which counts kind 1 and kind 6 entry scripts for the enemy's id: a level's defeat counters.
- **Stagger.** In `vf58`, on some hits (the conditions, `enemy+0x3618`, `0x6605d0` and a flag, aren't read), it adds to
  `int enemy+0x37d8`; when that reaches the `u16` at `+0x64` of `0x6613b0`'s row it resets it and, unless
  `u16 C+0x2e8` is 5 or 6, writes `C+0x2ec = 0x24`, `C+0x2ee = 1`. In the names' order (below) 5 and 6 are `Attack`
  and `AttackWait`, which fits; `0x24` would be `PersonalAction`, which doesn't fit a stun.
- **Discovery.** `FldEntryEnemyDiscoverScript_Register` (`0x8dd830`) and `..._DamageScript_Register` (`0x8dd800`)
  call `0x6b78a0` with kind 7 and 8 into the `fldEntryScript.cpp` vector at `0x2ee79b8`. `0x6b6c30(type, kind, id)` runs
  the matching entry. `0x650070`, under a test on a float from `0x5f4390`, asks `0x6b71d0(7, id)` whether such a
  script exists, then tests the controller's flags and two sight-like checks or a third; `0x64fff0` runs the script
  when that test passes and is called from the first callback of states 3, 4, 8, 10, 15, 16 and 26 among others;
  state 38's first callback `0x653770` runs it too. `FldEnemySetAIState_Discover` requests state 30, whose fourth
  callback `0x658ac0` sets controller `+0x30` to 31.
- **Companion lines.** `fldReportJob` has `OnBeingFound` (vtable `0x180c3a8`, job id 4), `OnBeingLost` (`0x180c3d8`),
  `OnDamaging`, `OnBeingDamaged`, `OnFinishingBattle` and `OnGettingItem`: the party's field remarks the battle
  inventory heard. They are posted through `0x6e48d0` into a time line at `[0x2ee77b8]+0x778`; `OnBeingFound`'s post
  site is not in analysed code. A hook on it would hear the game's own "found" moment, but it isn't found.
- **Sessions.** The four Debug message logs of 2026-11-21 (kept local) show no enemy in any `field nearby` record,
  no field strike and no Result box. They do show `FastBattle_Message.msg` compiled and the script functions above
  registered at the RVAs given. `UUIC_Field_FastBattle` may be the party "Squad Battle" a skill text names, not the
  Result box: `[NEEDS VERIFICATION]`.
- **Not looked at.** The strike-reach test (#186's tick): leads are `FldPlayerGetDegToEnemy` (`0x8e5070`),
  `FldEnemyIsDamageBackAttack` (`0x8dc1b0`) and `UUIC_Field_TargetCursor`. `[NEEDS VERIFICATION]`

The state table's slot order, by the callbacks it was built from (slot: update callback):
0 `0x652710`, 3 `0x64cf80`, 4 `0x653ea0`, 8 `0x655010`, 10 `0x650f30`, 15 `0x6533c0`, 16 `0x64e8a0`, 26 `0x654410`,
30 `0x658b10`, 36 `0x651f50`, 38 `0x653770`. The 40 names in `.udata` (in
[`field-enemy-awareness-static.md`](field-enemy-awareness-static.md)) put `Patrol` at 8 and `CirclePatrol` at 10,
which check for the player, but also `Approach` at 3 and `LostHome` at 16; whether the names run in slot order is
still open.

## What the mod now logs

A Debug build's `field nearby` record for an enemy adds `aiState=<u16 C+0x2e8> aiNext=<u16 C+0x2ec>
aiPending=<byte C+0x2ee> hp=<u32>` (`src/Metaphor.Core/FieldEnemyState.cs`). It reads only, on the `N` press.

## Capture checklist (next session, first dungeon, Debug build)

1. Stand where an enemy patrols, unseen: press `N`. Note `aiState` and `hp`.
2. Let it notice you (the "!" and sound): press `N` at once, then while it chases. Note each `aiState`.
3. Break line of sight until it gives up: press `N` as it turns back and once it patrols again.
4. Strike a weak enemy once without killing it if you can: `N` before and after; `hp` should drop.
5. Strike an enemy until it staggers: `N` while it's stunned. Is `aiState` 36?
6. Kill one in the field and let the Result box show; with #703's opt-ins, note any `ui-anim` or `ui-open` class.
7. Strike a strong enemy into a command battle; note the `ui-anim` classes as "Battle!" shows.
8. With the inspector: `follow base+0x2ee7510 0x0`, then `read <world>+0x8 4` before and after a discovery (the `0x80`
   bit), and once the enemy gives up.

Four or more `aiState` values that repeat with the behaviour settle the noticed and gave-up states for #649.

## Steps

Ghidra 12.2 (`ghidra_12.2_PUBLIC`) with JDK 25.0.5+8, the shared project of the build above, Windows 11 x64,
2026-11-22. Each line is one `pwsh scripts/dev/ghidra.ps1` query.

1. `decompile` `0x8dd830`, `0x8dd800`, `0x6b78a0`; `global 0x2ee79b8`; `callers` of each reader it lists;
   `decompile` `0x6b8bd0`, `0x6b6c30`, `0x6b70e0`, `0x661210`, `0x6612c0`, `0x6617d0`, `0x662df0`.
2. `callers 0x661210`; `decompile` `0x65e680` and `0x65d7a0`; `vtable app::fld::fldEnemy`; `disasm` `0x645110`,
   `0x6453f0`; `decompile` `0x3e60f0`, `0x662310`, `0x8dc240`.
3. `decompile` `0x64fdc0`, `0x64fff0`, `0x653770`, `0x650070`; `callers` of each; `disasm 0x6567b0`, listing each
   `lea` of a function before each of the 40 `call 0x64bb20` and the `lea rcx,[rbp+X]` it builds into;
   `decompile` `0x658b10`, `0x658af0`, `0x658b00`, `0x658ac0`.
4. A byte search of the exe for `.?AV` names with `fldReportJob`, `UUIC_Field_`, `Reward`, `Encount`, `FastBattle`;
   `vtable` of `fldReportJob::OnBeingFound` and its siblings, `UUIC_Field_Reward`, `UUIC_Common_BattleEncount`,
   `UUIC_Field_FastBattle`, `FieldRewardLayout`; `global 0x180c3a8`; `decompile 0x6f1ac0`, `0x6f3390`, `0x6eeec0`,
   `0x6e48d0`, `0x6ef620`, `0xfcb860`, `0xfcba20`, `0xf441b0`.
5. The session logs: counts of record kinds, the `field nearby` enemy counts, `compile` paths and `register` names.

Nothing in `.text1`, `.xcode` or an `.ecode` jump stub was decompiled; thunks into them were noted and not followed.
No decompiled output is quoted.
