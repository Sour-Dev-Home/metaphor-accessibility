# The Followers list is `CooperationMenuChoice`, named by the follower and archetype it fetches; no logged session has passed a rank-up or the Archetype Tree

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do (the
  message logs of earlier play sessions, read only)
- **Sources:** [`archetype-screen-classes-session5-2026-11-17.md`](../raw-sources/archetype-screen-classes-session5-2026-11-17.md),
  [`archetype-tree-class-session6-2026-11-17.md`](../raw-sources/archetype-tree-class-session6-2026-11-17.md)
- **Functions:** none

For #199 and #227. The exe's type descriptors name every `ope` choice class; the ones a Followers screen can be are
`CooperationMenuChoice@ope@ui@app`, `CooperationDetailChoice@ope@ui@app` and `CooperationSkillMenuChoice@ope@ui@app`
(the camp layouts beside them are `CampCooperation*`). No `Follower*` choice class exists. Of the 11 Debug message
logs kept from 2026-11-21 to 2026-11-23, one opened the Followers list; none has a cursor on an `Academia*` choice or
any record of the rank-up notice.

## Evidence

- **Followers list.** In the log of 2026-11-21 22.50, the main menu's `MainMenu_Follower_01` (serial 1451) was
  followed by 14 pairs of resolves: a `CharaName.msg` serial (21, 2, 4, 7, 8, 11, 16, 23, 34, 36, 40, 48, 44, 57) with
  `flags=8`, then an `ATCategoryName.msg` serial (9441 to 9457) with `flags=6`; then the first settled cursor,
  `screen=CooperationMenuChoice@ope@ui@app count=14`. The cursor stayed on row 0 for the visit, so that the rows are
  in fetch order, and whether a move fetches again, is `[NEEDS VERIFICATION]`. No message was fetched for a rank: the
  rank is drawn from data (`G+0x24674 + id × 4`, the [money note](money-follower-rank-and-shop-static.md)), which a
  feature can't read without a new hook-layer reader. `CooperationDetailChoice` wasn't opened.
- **Rank-up notice.** The notice is the UI part `UUIC_Common_FollowerRankUp` (the
  [shop and rank-up note](shop-rows-quantity-and-rank-up-notice-static.md)); its setter stores the follower ID and rank
  and fetches no message the logs show. The 11 logs have no `UUIC_Common_FollowerRankUp`, `evtFollowerRankSequence` or
  `FollowerRankupLayout` record besides the boot-time script registrations, so no logged session passed a rank-up. The
  by-ear "silent rank-up" of 2026-11-22 (#199, mark 25) falls after a battle whose level-up screen fetched
  `System_LVUP_` labels at 1 361 057 ms: whether mark 25 was a follower rank-up at all is `[NEEDS VERIFICATION]`.
  Speaking it needs a hook on the setter (`0xf5ba80`) or a read of the part's `+0x30` and `+0x38`, plus the follower
  name's serial range: not built.
- **Archetype Tree.** The `Academia` choice classes are `AcademiaTopChoice`, `AcademiaClassGetCharaSel`,
  `AcademiaClassSelectChoice`, `AcademiaClassDetailChoice`, `AcademiaClassDetailLearnChoice`,
  `AcademiaClassChangeConfirm`, `AcademiaCustomSkillCharaSel`, `AcademiaCustomSkillChoice`,
  `AcademiaSelectCustomSlot`, `AcademiaShopTopChoice`, `AcademiaShopChoicePersona`/`Summon`,
  `AcademiaShopConfirmChoicePersona`/`Summon` and `AcademiaPurchaseNumSelectChoicePersona` (each with a `_Data` twin).
  Sessions 5 and 6 logged `AcademiaClassGetCharaSel` (the character pick) and `AcademiaClassDetailChoice` (details
  from the tree), and no cursor on the tree itself. The
  [tree note](archetype-tree-scene-and-choice-static.md) names `AcademiaClassSelectChoice` the likely choice; that the
  tree's cursor never reaches the plain getter (session 6) still leaves it `[NEEDS VERIFICATION]`.
- **Archetype details** are named (session 5): `EquipClassDetailChoice`, `StatusMenuClassDetailChoice`,
  `AcademiaClassDetailChoice`, one per entry point. Which manager (`+0x1740`, `+0x1750`, `+0x1760`) is which tab is in
  the [details tabs note](archetype-details-tabs-static.md).

## What the next session must check

1. Main menu, Follower: with `followerList` on, move over three followers and say each row aloud as the screen shows
   it; open one and note the `cursor` records' `screen=` and the `resolve` records before each settled cursor.
2. A bond scene that ranks a follower up: mark the moment the notice shows; check for `pane` and `ui-anim` records
   and any `resolve` of a `FollowerName_` label.
3. Archetype Tree: with the inspector up, `read` the scene map's key `0x11` while the tree is on screen, and look for
   an `AcademiaClassSelectChoice` record.

## Steps

On the exe of the build above, 2026-11-23:

1. `grep -aoE '\.\?AV[A-Za-z0-9_]*(Coop|Follower|Academia|Camp)[A-Za-z0-9_]*@[a-z@]*app@@' <exe> | sort -u`: the
   type descriptor names.
2. For each Debug `message-log.txt` from 2026-11-21 to 2026-11-23 (11 logs): `grep -c` for
   `FollowerRankUp|evtFollowerRank|RankUp@` and for `Academia[A-Za-z]*@ope`, all 0, and for `Cooperation[A-Za-z]*@ope`,
   9 in the log of 2026-11-21 22.50 only; then the `resolve` records between its `MainMenu_Follower_01` and first
   `CooperationMenuChoice` cursor.
3. In the by-ear session of 2026-11-22's `report-log.txt`, mark 25 is at 1 370 430 ms; the `resolve` records in the
   20 s before it in its `message-log.txt`.
