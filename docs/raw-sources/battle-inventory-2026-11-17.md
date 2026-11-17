# Battle screens and events: what a sighted player sees and does (issue #161)

- **Source:** capture evening 1 (#229), session 5, on the maintainer's machine through Reloaded-II, Debug mod build
  `219282c` (branch `capture-1`) with the opt-ins in #229's checklist (screen class, menu values, voice, dialogue
  window and its flag, UI text panes), Steam Input on, controller. Recorded with OBS (33 min 50 s) and narrated; the
  narration was transcribed locally with the `watch` skill (WhisperX small). Frames were taken with ffmpeg 9.0.2 at the
  times the narration names and read by eye. The mod's `message-log.txt` (35 min) was counted by record kind
  (`screen`, `cursor`, `value`, `cue`, `pane`, `input`); no text was printed into this note. The recording, the log
  and the frames stay local. Only field names, on-screen labels, screen class names, counts and timestamps are kept;
  no game text. Video time = log time − 51 s, to within a second or two.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. The maintainer loaded a late-game save, entered a dungeon and opened five fights with a field strike: fight 1
   (2:45–8:00) played through to the results; fight 2 (about 8:10) escaped with Tactics/Escape; fights 3 to 5
   (25:00–28:30) used skills, an item and Auto Battle, then escaped.
2. Screen names below are the owner classes the screen-class listener logged (`screen` records, all owned by a
   `CursorManager`); times are the first entry in video time. Screens with no `screen` record are marked "none".

## Results

### Screens with a cursor (screen-class listener)

| Screen (class, without `@ope@ui@app`) | First seen | `screen` records | Fields shown | What changes as the cursor moves |
|---|---|---|---|---|
| Archetype skill list (`BTL_SkillSelect`) | 3:21 | 133 | Title Archetype Skill with the archetype's name; per skill an element icon, name, cost (MP or HP); a Weak tag before skills that hit the current target's weakness. A Confirm, B Back. Party panel stays. | Focused skill shows its description in a band. |
| Synthesis list (`BTL_SyentesisSkillList`) | 3:14 | 10 | Per Synthesis skill an icon, name, star cost (turn icons used) and HP/MP cost. | Focused skill's description. |
| Item (`BTL_ItemSelect`) | 4:27 | 42 | Title Item; 5 category tabs shown as icons (LB/RB), only the focused tab's category named under it; per item icon, name, count. A Confirm, B Back. | Focused item's description, with the amount it restores. |
| Analyse (`BTL_AnalyzeChoice`, LT) | 5:01 | 14 | Per enemy: race, name, level, HP and max HP (shown as `?` until known), a 9-element affinity grid (weak `!`, resist shield, `·` neutral, block/null icons), Spoils (Common, Rare), Skill list. LB/RB switches enemy. B Back. | Focused skill's description. |
| Tactics/Escape menu (`BTL_OrderTop`, LB) | 5:14 | 35 | Entries for Tactics, Party and Escape `[NEEDS VERIFICATION]` (the frame at 5:14 is a transition; entries inferred from what each led to). | — |
| Auto Battle Tactics (`BTL_OrderTactics`) | 5:16 | 31 | Title Tactics, subtitle Auto Battle Tactics; 6 options; bottom-left the current setting for all party members. A Confirm, B Back. | Focused option's description. |
| Party: active members (`BTL_OrderPartyBattleMember`) | 5:22 | 17 | Title Party, prompt; 4 member rows (archetype, HP, MP, role tag, a Front/Back marker `F`/`B`); a Reserve column of 3 rows; right: the focused member's Synthesis list (name, star cost, HP/MP cost). X Synthesis Requirements, Y Archetype Details, A Confirm, B Back. | Focused member changes the Synthesis panel. |
| Party: Synthesis requirements (`BTL_OrderPartyChgSynthesis`, X) | 5:26 | 5 | Synthesis list with LB/RB per member; a Synthesis Requirements box: Required Skill, Ally. | Focused skill's description and requirements. |
| Party: reserve (`BTL_OrderPartyReserve`) | 5:37 | 5 | Reserve rows; choosing one swaps it in. The swap shows as an action name banner (5:38). | — |

Cursor records matched to these screens: 125, 7, 38, 12, 28, 30, 16, 4, 4 in the table's order.

### Screens and events with no cursor screen

These had no `screen` record: the screen-class listener does not see them, so a later slice needs another source.

| Screen or event | Seen | Fields shown | What changes |
|---|---|---|---|
| Field strike that opens a fight | 2:50, 25:52 | In the field a Squad prompt with Y over the player when an enemy is in reach. | Fight starts on hit; an ambush or advantage state was not identified `[NEEDS VERIFICATION]`. |
| Command wheel (each member's turn) | 3:05, 6:00 | 6 commands with a button each: Archetype (Y), Melee (X), Item, Pass, Guard (B), Synthesis (A); Retry (L). Bottom bar: Formation and Target (D-pad), Status, Auto Battle (R), Analyse (LT), Assist (RT), Tactics/Escape (LB). The acting member's portrait is lifted out of the party panel. | Up/down on the D-pad changes formation; left/right changes the target. |
| Target | 3:05, 6:00 | Over the targeted enemy: its name and an affinity tag for the chosen command (Weak or Resist), with element icons under Resist. | Moves between enemies with the D-pad. |
| Turn icons | top centre, throughout | 4 icons for the party's turns; a hit on a weakness turns a full icon into a half icon (a free action); passes and misses cost more (narration 5:42–6:38). | Count and state change per action. |
| Party panel | bottom right, throughout | 4 rows: row number, archetype name, HP and MP numbers and bars, portrait; a Front or Back label on the acting member. | Numbers change on damage and healing. |
| Action name banner | 4:00, 5:38 | The name of the skill or swap being used, top centre; B Skip during animations. | One per action, enemy or ally. |
| Damage numbers | 7:40, 26:30 | A number per hit over the target. | — |
| Navigator and party lines | 25:30, 26:12 | A portrait and a line top-left: hints (exploit a weakness), ailments (an ally charmed), cheers. | Per event. |
| Auto Battle | 26:16–26:30 | A Tactics banner with the current tactic and B Stop Auto; turns play without the command wheel. | — |
| Escape | 8:10, 26:40, 28:22 | Fade to black, then the field with the enemy party still chasing (line from a companion). | — |
| Results | 7:53–7:58 | Title Result; EXP, A.EXP (archetype EXP), MAG, Money; Spoils (a list, or None); a party line top-left. A Confirm. | — |
| Growth | 7:59 | One row per member (7 rows): portrait, level, archetype icon, rank as current/20. A Confirm. | — |

### Listener counts during the fights (2:45–8:20 and 25:00–29:10)

| Record | Count |
|---|---|
| `value fn=UnitAffinity` | 643 (peaks of 72 in the 10 s of Analyse at 5:00 and 44 while targeting at 25:30) |
| `value fn=GetPlayerUnit` | 362 |
| `value fn=UnitGetParam` | 208 |
| `value fn=GetBagItem` | 140 |
| `value fn=GetMoney` | 5 |
| `cue` | 2963 |

So the affinity getter runs while targeting and in Analyse, which makes it a lead for #164's affinities
`[NEEDS VERIFICATION]` (which unit and element each call is for was not decoded).

### Advanced battle commands found (one slice each, for the maintainer's yes or no)

| Command | Seen | Notes |
|---|---|---|
| Synthesis (A) | 3:14, 6:49, 25:50 | Team-up skills costing several turn icons; needs named allies. |
| Retry (L) | on the wheel | Turns back the clock; not used. |
| Pass | on the wheel | Hands the turn on; narration 5:42. |
| Assist (RT) | on the bar | Not used in a command battle. |
| Auto Battle (R) and Tactics | 5:16, 26:16 | Auto Battle plays turns by the chosen tactic. |
| Formation (D-pad up/down) | 3:16 | Front and Back rows. |
| Party switch with the reserve | 5:22–5:38 | Swaps a member mid-fight. |
| Analyse (LT) | 5:01, 25:28 | Enemy affinities, spoils and skills. |
| Escape (Tactics/Escape) | 8:10 | Ends the fight, back to the field. |

### Found outside #143's scope (command battle), for the maintainer's yes or no

| Feature | Seen | Notes |
|---|---|---|
| Real-time field combat | 24:39–25:08, 27:43, 28:30 | Weak enemies die to field strikes without a command battle; a Result box top-left (EXP, A.EXP, MAG, Money, each with a bonus) and a new-item line. X attacks, RT assists (narration). Related: #186. |
| Enemy awareness in the field | 26:45, 27:13–29:04 | Companion lines when enemies spot the party, lose it, or roam; enemies chase after an escape. |

### Problems noted

- **Visual-only state:** the turn icons (full and half), Weak/Resist tags, the affinity grid, Front/Back markers,
  HP bars.
- **The command wheel, targeting and results are not cursor screens:** the screen-class listener logged nothing for
  them (above), so speaking them needs another hook (#162).
