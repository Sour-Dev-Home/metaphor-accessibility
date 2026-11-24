# The results screen and Analyse fill their numbers through protected code; money and MAG are read from the purse

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on
  [`money-follower-rank-and-shop-static.md`](money-follower-rank-and-shop-static.md),
  [`menu-values-party-unit-static.md`](menu-values-party-unit-static.md) and
  [`enemy-stun-break-and-battle-condition-static.md`](enemy-stun-break-and-battle-condition-static.md)
- **Functions:**
  - BattleResultDataDelete | 0x20b060 | .ecode | Battle | known only | `app::btl::ResultData::vf0`, frees a 0x18-byte object.
  - BattleResultUpdate | 0xf1ee00 | .ecode | Battle | known only | `UUIC_Battle_Result::vf13`, the results component's state machine.
  - AffinityTableParts | 0xbde390 | .ecode | Battle | known only | `AffinityTable::vf17`, binds the nine `parts_resist_table_status_n` parts.

For #166, #164 and #255, a static spike: no `.ecode` code was found that holds a battle's EXP, money or spoils, or an
enemy's affinities, in a field the mod could read. So the mod reads the party's money and MAG at the battle's start and
after "Victory" and says the rise (`speech.battleRewards`, testing); EXP, spoils, affinities and Analyse wait for a live
read. Static only.

## Evidence

- **Results.** `app::btl::ResultData` (vtable `0x17d7240`) is 0x18 bytes (its deleting destructor `0x20b060` frees
  0x18), and nothing in analysed code but that destructor names the vtable, so it is built in protected code and is too
  small to hold the rewards itself `[NEEDS VERIFICATION]`. The results component `UUIC_Battle_Result` (vtable
  `0x224f938`) runs a state machine in `vf13` (`0xf1ee00`) whose data calls go through other objects' vtables; the
  related layouts are `ResultLayout`, `ResultBgLayout`, `ResultLevelUpLayout`, `ResultGrowParamLayout`,
  `ResultSkillGetLayout` and `RewardLine@result@layout@ui@app` (RTTI names in the exe). The 2026-11-23 review on #166
  found no text pane for the numbers either.
- **The purse.** Money is the game data block's `int` at `+0x22c58` (`GetMoney`, checked against the shop in session 5)
  and MAG the `int` at `+0x233fc`; both notes rate them high. Whether the game adds a battle's money before the results
  screen or as it closes is `[NEEDS VERIFICATION]`: the mod watches for 60 s after "Victory" either way.
- **Affinities.** `AffinityTable@common@layout@ui@app`'s `vf17` binds nine parts, `parts_resist_table_status_1` to `_9`,
  one per affinity, as the spec's nine. Its `vf16` and `AnalyzeInfoLayout`'s `vf16` and `vf20` (`0xb01bd0`, `0xae9e60`)
  pass the values on through calls into protected code (`0x153b69960`, `0x14acaae60`) or through vtables, so where
  an enemy's affinities and the player's knowledge of them live wasn't found `[NEEDS VERIFICATION]`. `AnalyzeInfoLayout`
  `vf20` hands ids `0xb41` to `0xb61` to `0xa700a0`; what they name is `[NEEDS VERIFICATION]`.
- **Conditions.** A hit row's `+0xC` and a unit's `+0x1c` share the condition bit space (the stun note), but
  `BadStatus_Message_1.msg`'s positions don't follow the bits: position 12 is KO, which is bit 19. Which bit is which
  condition is `[NEEDS VERIFICATION]`.

## For the next session

A Debug build now logs, per `BattleHitResult`, the row's condition bits (`cond=`) and the unit's (`unitCond=`); per
enemy in `BtlPackageUpdate`'s list, the record's first 0x60 bytes (`head=`), to look for its enemy id (the banner's
`EnemyName.msg` serial less 122) and its conditions; and each change of the turn icons read (`BattleTurnIcons`), for
#254's unsaid mid-turn gain. Inflict poison and a stun, open Analyse on an enemy, and win a battle with the results on
screen for a few seconds.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1 batch`), build above, 2026-11-23; RTTI names by a byte search of
the exe for `.?AV` strings with `Result`, `Analy`, `Reward`, `Affin` or `Weak`.

1. `vtable app::btl::ResultData`, `app::ui::UUIC_Battle_Result`, `app::ui::UUIC_Battle_Analyze`,
   `app::ui::AnalyzeInfoLayout`, `app::ui::ope::BTL_AnalyzeChoice`, `app::ui::layout::common::AffinityTable`,
   `app::ui::ResultLayout`.
2. `global 0x17d7240`; `decompile 0x20b060`, `0xf1ee00`, `0xf1de70`, `0xae9e60`, `0xaea910`, `0xbf2b40`, `0xbde390`,
   `0xb0cb20`, `0xb01bd0`, `0xaedf30`, `0xd8f9c0`, `0xd93100`.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
