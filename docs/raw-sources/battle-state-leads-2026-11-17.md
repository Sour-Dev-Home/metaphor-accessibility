# Where the game holds battle state: static leads and the battle listener (#162)

- **Source:** our reading of the game exe on disk, as shipped (ADR 0002: nothing patched, unpacked or run). Tools: a
  throwaway .NET 9.0 reader on Iced 1.21.0 (MSVC run-time type information to vtables, disassembly, rip-relative
  references, call sites, the exception directory's function bounds), not committed; and
  `tools/Metaphor.SignatureCheck --probes` with `battle-probes.json`. Starting points: the battle screens in
  `battle-inventory-2026-11-17.md` and the script functions in `squirrel-script-functions-all-2026-11-16.md`.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

Class names (the game's own, from its type information), RVAs and offsets only (ground rule 4). Function names
without a class are ours. All code below is in `.ecode`; nothing in `.text1` was read or is hooked (ground rule 5).
Nothing here was checked in a running game: every meaning is a lead until capture evening 2 (#250) logs it.

## Findings

### The battle package and its phases

- **`Package@btl` is the battle object.** Its constructor (`0x20b3c0`) lays out smart pointers (object at `+0x18` of
  each) and pointer lists (first node at `+8`): phase at `+0x60`, `Order@btl` at `+0x80`, `Formation@btl` at `+0xA0`,
  the action list `PointerList<SmartPointer<Action@btl>>` from `+0x108`, `Object@btl` list from `+0x1E8`, `Gui@btl` at
  `+0x358`, and a lone `SmartPointer<Action@btl>` at `+0x6F0` (object at `+0x708`; that it is the current action is
  `[NEEDS VERIFICATION]`). A list node holds the next node at `+0` and the object at `+0x28` (read off the `Fight`
  phase's walk of the action list at `0x2281ce`).
- **`Package::Update` (`0x21e1c0`, our name), `void (Package*, float seconds)`,** has one caller (`0xc8cbe`). Each
  frame it updates the GUI (`0x326720`), then calls the phase's third virtual function with `(phase, &next, package,
  scaled seconds)` and stores the phase it returns in the smart pointer at `+0x48` (`0x20ecb0`).
- **Phases are `Phase@btl` subclasses in `phase@btl`.** The second virtual function of each returns its ID
  (`mov eax,imm; ret`); the third runs it and builds the next phase inline. IDs: `Launcher` 0, `Load` 1, `Beginning` 2,
  `Support_TAKARA` 4, `Fight` 5, `Reinforcement` 6, `PinchEscape` 7, `PinchTransition` 8, `AdvantageousEscape` 9,
  `AdvantageousTransition` 0xA, `HoldUpTransition` 0xB, `IntimidationTransition` 0xC, `AllyEscape` 0xD,
  `RefugeInShelter` 0xE, `Series` 0xF, `Victory` 0x10, `Annihilation` 0x11, `Interruption` 0x12, `Rewind` 0x13,
  `Finalize` 0x14. No class returns 3. Reading by name: advantage is `AdvantageousTransition`, an ambush likely
  `PinchTransition`, the end `Victory`, `Annihilation` or an escape; all `[NEEDS VERIFICATION]`.
- **`Beginning` runs at the start of each turn, not of the battle**: its run function (`0x227b60`) calls
  `0xc78f0`, which adds one to the elapsed player turns (below).
- **`Fight` (`0x228160`) walks the action list** and calls each action's third virtual function (`0x1a9770` on
  `Action@btl` and `Participate@action@btl`: true when the action has finished).

### The global battle info

A 0x20-byte global at RVA `0x27293b0`, passed as `this` to tiny setters: `+0x08` the battle result (read by
`BtlGetResult`, `0xc91d0`; set by `0xc7c80` from the `Beginning`, `AdvantageousTransition` and `Fight` phases), `+0x0C`
an `int` read by `0xc91c0` and set by `0xc7c70` (from `0x232b80`, in `Victory`'s second vtable), a bit set at `+0x10`
(`0xc7a10`), and `+0x14` the elapsed player turns (`BtlGetElapsedPlayerTurn`, `0xc91b0`; `0xc78f0` adds one). The
battle package's constructor zeroes it (`0xc7520`). The three getters at `0xc91b0`, `0xc91c0` and `0xc91d0` sit in a
row, so the listener locates the first and reads the global from its `mov eax,[rip+disp32]`.

### Leads not used yet

- **GUI components.** `UUIC_Battle_Command` (the command wheel), `UUIC_Battle_TargetCursor` (its argument holds a list
  of up to 10 entries), `UUIC_Battle_PressTurnIcon` (turn icons), `UUIC_Battle_Damage`, `UUIC_Battle_Message`,
  `UUIC_Battle_PartyPanel`, `UUIC_Battle_Result`, `UUIC_Battle_ResultLevelUp`, `UUIC_Battle_ResultSkillGet` and more,
  each a small wrapper around a layout at `+0x18`. Their sixth virtual function receives a message (`std::any`-like:
  type at `+0x38`, a has-value byte at `+0x40`); `UUIC_Battle_PressTurnIcon`'s (`0xf1b9f0`) handles a
  `CloseArg@UUIC_Battle_PressTurnIcon`. Messages are sent from many call sites (`0xfd7868` and others), not one
  dispatcher, so hooking them means one hook per component.
- **Battle screens with a cursor** already named by the screen-class listener have `_Data` classes too
  (`BTLCommandSelectChoice_Data@ope@ui@app` and others); `BTL_CommandSelectChoice` is the wheel's own choice class.
- **`ResultData@btl@app`** (vtable `0x17d7240`) for results; **`ProcessSelectCommand@gui@btl`** for command selection.
- **Unit checks.** `0x1a9790` and `0x1a97b0` compare a unit's 219th virtual function (`[vtable+0x6D0]`) with 0x19
  and 0x1A: a unit kind or ID.
- **Party units** are the `0x16A8`-byte units of `menu-value-getters-2026-11-17.md`.

## The listener (Debug, opt-in)

With `opt-in-battle.txt` in the mod's folder, `BattlePackageUpdateListener` hooks `Package::Update` (signature
`BtlPackageUpdate`) and observes each call before it runs; it locates `BtlInfoGetters` and `GetPlayerUnit` without
hooking them. It reads, never writes or calls, and logs `battle <kind> …` records: addresses, offsets, numbers and class
names only. A 32-bit field counts only when it holds a small integer (magnitude up to 10,000,000) before and after; a
field that changes on 8 compares in a row is muted and named once.

| Record | When | What it means |
|---|---|---|
| `start pkg=0x…` | a new package | A battle starts. |
| `phase pkg= phase= class=…` | the phase's vtable changes | The phase by class name (IDs above): start, advantage or ambush, each turn's `Beginning`, `Fight`, the end. |
| `fields of=package\|order\|formation\|gui\|info …` | first frame, then each change | First: the object's address, class and non-zero small fields. Then `changes=+0xOFF:old>new,…` and `muted=…`. Turn icons, whose turn, the wheel and the target are expected among these. |
| `refs of=… refs=+0xOFF->0x…` | when it changes | Which 8-byte fields point at a battle object or action: the acting member and the targeted enemy are expected here. |
| `object obj= class= values=` | at a phase change, new objects | Each `Object@btl` in the list, with its non-zero small fields: enemy count and IDs. |
| `object-fields obj= changes=` | at a phase change | What changed in a known object since the last phase: HP, defeats, statuses. |
| `object-gone obj=` | at a phase change | An object left the list. |
| `action act= class= values=` | a new action in the list or current | Each action's class and non-zero small fields: attacker, action ID, targets. |
| `action-gone act= changes=` | the action leaves | What changed in it meanwhile: per-target results. The memory may already be reused. |
| `party index= unit= changes=` | at a phase change | What changed in each of the first 8 party units: HP, EXP, levels (growth). |

## Steps

1. Index the vtables by class name from the type information; read `Package@btl`'s constructor, find its phase smart
   pointer and the one function that runs the phase (`0x21e1c0`), and read each phase class's first three virtual
   functions.
2. Follow the script functions `BtlGetElapsedPlayerTurn` (`0x9039d0`) and `BtlGetResult` (`0x903a00`) to the global
   at `0x27293b0`, and find its other users by rip-relative references.
3. `--probes <exe> tools/Metaphor.SignatureCheck/battle-probes.json`: 3 of 3 matched once. The getters' three
   one-line functions alone match 9 times; the pattern keeps the function after them.
