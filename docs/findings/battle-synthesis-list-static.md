# Battle's Synthesis list works like the skill list: a classed cursor, names by serial, and costs in the skill tables

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do (the
  maintainer's local Debug logs, grep only)
- **Sources:** none, see Steps; builds on
  [`battle-skill-select-decide-and-ui-sound-static.md`](battle-skill-select-decide-and-ui-sound-static.md),
  [`skill-cost-from-skill-tables.md`](skill-cost-from-skill-tables.md) and
  [`battle-inventory-2026-11-17.md`](../raw-sources/battle-inventory-2026-11-17.md) (rows 29, 35, 77)
- **Functions:**
  - SynthesisDecide | 0xd7aae0 | .ecode | Battle | known only | `BTL_SyentesisSkillList`'s confirm handler: posts the focused row's skill id to the UI flow. | finding
  - BTL_SyentesisSkillList::vf14 | 0xd926c0 | .ecode | Battle | known only | Rebuilds the list's rows (`0xd84380`, then `0xd85580`). | finding

For #260. The Synthesis list is `BTL_SyentesisSkillList@ope@ui@app` (vtable `0x21cfea0`), and its cursor already
reaches the mod with that class (count 4 in the 2026-11-22 session). As it opens it fetches each row's skill name with
flags 9 and its description with flags 7, exactly as `BTL_SkillSelect` does, so `BattleListFocus` can name its rows
once the class is registered; nothing else is missing for names. The turn-icon cost and the HP or MP cost are in
`SkillNormal.TBL` (`+0xA`, and `+0x5` to `+0x8`), which the skill-cost note already reads by the name label's MD5.
The required allies are most likely in `Synthesis.TBL`, which the battle table loader reads beside it; its format
isn't known. Why a Synthesis can't be used is decided in the UI flow behind `.text1`, as for skills.

## Evidence

- **Classed, silent.** The maintainer's 2026-11-22 session (Debug, Steam 18330018; local log, grep only), F8 mark 13
  (#260's comment): `cursor` records for one manager with `screen=BTL_SyentesisSkillList@ope@ui@app count=4` at
  1 367 177, 1 368 409 and 1 386 332 ms. Just before the first, the game fetched four `SkillName_*` (flags 9) and four
  `SkillText_*` (flags 7) messages, one pair per row, and two serials with no known label (151 and 1087, flags 8).
  Moving fetched nothing new. As the list closed, `ui-anim class=SynthesisListLayout@ui@app anim=2981` (1 386 947 ms).
- **Rows.** `vf14` (`0xd926c0`) calls `0xd84380`, which empties the row array at `+0x13e8` (count `+0x13f0`) and fills
  it, and `0xd85580`. Slot 64 (`0xd969b0`) hands the list the lambda whose vtable is `0x21d16b0`; its call slot
  `0xd8d150` only calls `SynthesisDecide` (`0xd7aae0`).
- **Confirm.** `SynthesisDecide` reads the cursor (`+0x58` slot), takes that row from `+0x13e8` (16-byte rows), sets the
  UI flow's `SkillID` and `COMMON_VAL` (`0xf000000`) and posts the event through `0xd46610(component + 0x198, …)`: the
  same shape as `SkillSelectDecide` (`0xd77810`, 8-byte rows at `+0x4260`). What the flow does next is behind `.text1`
  thunks, so the unusable case can only be recognised as confirm followed by the buzzer, as #692 does for skills
  `[NEEDS VERIFICATION]`.
- **Tables.** The battle table loader `0x3dd7b0`, which loads `SkillNormal.TBL` (skill-cost note), also loads
  `COMMON/battle/table/Synthesis.TBL` (the `lea` at `0x3ddada`) and stores it at `+0x100` of its table block. That the
  file holds the required allies (inventory row 35: Required Skill, Ally) is a lead from its name
  `[NEEDS VERIFICATION]`; it wasn't read from `base.cpk`.
- **Order, Party, X.** `BTL_OrderPartyChgSynthesis` (`0x21d0a70`, with its own slot 64, `0xd96970`) is the
  requirements view the Party screen opens ([Party note](battle-party-swap-and-formation-static.md)); it never showed a
  cursor in the saved logs.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`,
[query-the-ghidra-project.md](../contributors/how-to/query-the-ghidra-project.md)), build above, 2026-11-23.

1. `vtable app::ui::ope::BTL_SyentesisSkillList`, `app::ui::SynthesisListLayout`, `app::ui::ope::BTL_OrderPartyChgSynthesis`.
2. `decompile` `0xd926c0`, `0xd84380`, `0xd85580`, `0xd969b0`, `0xd96970`; `disasm 0xd969b0` (the lambda's vtable);
   `vtable 0x21d16b0`; `decompile 0xd8d150`, `0xd7aae0`.
3. The file name's offset in the exe with `grep -aob`, its RVA with `tools/Metaphor.SignatureCheck --bytes` (in
   `.udata` the RVA is the file offset plus `0x1200`), then `global 0x17e8640` and `disasm 0x3ddada`.
4. Local log: `grep -a "BTL_SyentesisSkillList\|SynthesisListLayout" message-log.txt`, and the `resolve` lines before
   the first record.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.

## Build plan

1. **Names (no new hook).** In `BattleListFocus`, a third list:
   `Screen("Synthesis", "BTL_SyentesisSkillList@ope@ui@app", OutsideMenus: true)`, with the same name and description
   flags. Test: a fake opening of four names, a move, a reopening.
2. **Costs.** With `SkillCosts` from the skill-cost note's plan, say the turn icons from `SkillNormal.TBL` `+0xA` and
   the HP or MP cost after the name: "`<name>`, 2 icons, 12 MP". The shown form (icons, half icons) is checked by ear.
3. **Allies (spike first).** Read `Synthesis.TBL` from `base.cpk` with `CpkArchive` and match its records to skill ids,
   as the skill-cost note did for `Skill.TBL`; if it names the required characters, say them as "needs Strohl".
4. **Unusable.** Say "can't use" when a confirm on this list is followed by UI sound 3, reusing #692's recogniser; the
   reason waits for step 3 (a missing ally) or the cost (too few icons, MP).
5. **By ear, Debug.** Open Synthesis with at least one unusable skill; move over every row and compare names and costs
   with the screen; confirm the unusable one.
