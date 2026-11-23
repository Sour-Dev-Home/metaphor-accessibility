# The battle package's enemy count is the length of its enemy list, and a hit's unit record finds its place in it

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** [`battle-state-session6-2026-11-17.md`](../raw-sources/battle-state-session6-2026-11-17.md); builds on
  [`battle-hit-result-hp-left-static.md`](battle-hit-result-hp-left-static.md) and
  [`battle-turn-icons-and-ui-components-static.md`](battle-turn-icons-and-ui-components-static.md)
- **Functions:**
  - BattlePackageInit | 0x20b3c0 | .ecode | Battle | known only | Builds `Package@btl` and its four unit lists.
  - BattleAddEnemy | 0x2100a0 | .ecode | Battle | known only | Adds an action to the enemy list once.
  - SmartPointerCopy | 0x144100 | .ecode | Battle | known only | Links one intrusive smart pointer to another's object.
  - PointerListAppend | 0x144340 | .ecode | Battle | known only | Appends a smart pointer at a list's tail.

For #750. Package `+0x140`, the enemy count session 6 logged, is the count of a linked list at package `+0x128`. Each
node holds an enemy's action, the action holds its `btl::Unit`, and the unit's `+0x8` is the unit record
`BattleHitResult` gives each hit (`*(*(target+0x18)+0x8)`). So walking the list at battle start gives each enemy's
record in list order, and a hit's ordinal is its record's place there. Static only: that the list order is the order on
screen, and that the mod can reach the package at battle start, are `[NEEDS VERIFICATION]`.

## Evidence

- **Four lists in the package.** `0x20b3c0` stores the vtable `gfw::PointerList<gfw::SmartPointer<btl::Action>>` at
  package `+0x108`, `+0x128`, `+0x148` and `+0x168`, zeroes the two words after each, and zeroes the `int` at `+0x18`
  of the first three. Session 6 read the counts at `+0x120` (party, 4), `+0x140` (enemies, 3, 1 and 2 at start, one
  less per defeat) and `+0x160` (all units): those are the `+0x18` of the first three lists. The vtable name may be
  shared with other element types by identical-code folding, so the name alone doesn't fix the element type.
- **The list's shape.** A list is `vtable +0x0, head +0x8, tail +0x10, count +0x18`. A node is `next +0x0`,
  `prev +0x8`, then a smart pointer at `+0x10`. `AdvantageousTransition::vf1` (`0x22c580`) walks the enemy list:
  `MOV RDI,[R15+0x130]`, then per node `LEA RDX,[RDI+0x10]` and `MOV RDI,[RDI]` until null (`0x22d2f2`–`0x22d3aa`),
  and the party list the same from `+0x110` (`0x22d43c`).
- **The smart pointer.** `0x144100` shows the layout: `vtable +0x0, prev +0x8, next +0x10, object +0x18`, an intrusive
  list of the pointers to one object. So a node's object is at node `+0x28`.
- **Enemy to unit to record.** `0x2100a0` walks the list from `[package+0x130]` and stops if a node's `+0x28` equals
  the new action's object, so no enemy is listed twice; otherwise it takes the action's
  `gfw::SmartPointer<btl::Unit>` at action `+0x18` (object at `+0x30`). From that unit it copies the
  `gfw::SmartPointer<gfw::Actor>` at unit `+0x10`, then appends through `0x144340(package+0x128, …)`, and adds the
  same action to the all-units list at `+0x148` unless it's there. `0x144340` allocates a node through the list's
  vtable slot 1, copies the smart pointer to node `+0x10`, links it after the tail and adds one to the count, so the
  list keeps the order enemies were added in. In the
  hit path, `0x326720` builds a `gfw::SmartPointer<btl::Unit>` on the stack and passes it as `BattleHitResult`'s
  `target`, whose `+0x18` is the unit and whose unit `+0x8` is the record (the hit note). The record of enemy `i` is
  therefore `*(*(*(node_i + 0x28) + 0x30) + 0x8)`.
- **Defeats.** `+0x140` drops by one per defeat (session 6), so a defeated enemy leaves the list and later indices
  shift.
  The ordinals must come from a snapshot taken at battle start, not a fresh walk per hit.
- **Reaching the package.** No global holds it (turn-icons note). Each phase's update gets it as its second argument:
  `AdvantageousTransition::vf1(this, package)` reads `[RDX+0x150]` at `0x22c5ff` and the lists above. Session 6's
  Debug listener got it from `Package::Update`. `btl::phase::Beginning::vf1` (`0x250c50`) only returns 2. Which hook
  the mod should use to take one snapshot when the fight opens is `[NEEDS VERIFICATION]`.
- **The target cursor.** `BattleTargetCursorFocus` takes a slot index (target-cursor note); that it indexes this list
  is a guess `[NEEDS VERIFICATION]`.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-22. The game wasn't run.

1. `offset 0x140`, filtered to `dword` reads and writes: `0x20b3c0`, `AdvantageousTransition::vf1`, `Series::vf1`.
2. `decompile 0x20b3c0`, searched for `param_1[0x21]` to `param_1[0x2e]`: the four list vtables.
3. `disasm 0x22c580` around `0x22d2f2` and `0x22d406`: the enemy and party list walks.
4. `decompile 0x144100`: the smart pointer layout.
5. `offset 0x128`, filtered to `LEA` in battle code; `decompile 0x2100a0` and `0x144340`: the enemy append.
6. `decompile 0x326720`: the `target` argument passed to `BattleHitResult` (`0x3231a0`).
7. `vtable btl::phase::Beginning`, `decompile 0x250c50`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.

## What would settle it

An inspector or Debug-build check, then by ear:

1. In a Debug build, at the first `Fight` phase update, log the package's `+0x140` and, walking from `+0x130`, each
   node's `+0x28`, that action's `+0x30` and the unit's `+0x8`.
2. In a battle with three enemies, hit each one in turn, left to right on screen. Each `BattleHitResult` record's
   `record=` should equal one logged record. Note which list index each on-screen enemy has.
3. Defeat one enemy and log the list again: it should be one shorter, and the others' records unchanged.
4. Repeat once with an ambush and once with an advantage start (session 6 captured neither).
5. By ear, after #750 is built: the first hit in a three-enemy fight says "First enemy" (or the right ordinal), and a
   one-enemy fight says "Enemy".
