# In-game menu screens: fields and cursor behaviour (issue #154)

- **Source:** one run on the maintainer's machine through Reloaded-II 1.31.0, mod build `3dc9b59` (`hooks 19/19`), with
  NVDA running. The maintainer walked the in-game menu with a controller, narrating, and recorded it with OBS (13 min
  56 s). The recording was read locally with the `watch` skill (frames by ffmpeg 9.0.2, narration by WhisperX small) and
  stays local. The text-pane, message-label and cursor listeners were **not** running (their opt-ins were absent, and
  the cursor listener isn't in this build), so everything below is from the video and the narration only. Only field
  names, labels, counts and timestamps are kept; no game text.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. The game ran from a save late in the story (party of 8 rows: 7 members and a guide).
2. The maintainer opened the in-game menu and visited each entry in order, then the Archetype Tree, then returned to the
   title screen and its menu.
3. Frames were taken at the times the narration names a screen and checked against it. Timestamps are into the video.

## Results

### Screens in scope for #145

| Screen | Time | Fields shown | What changes as the cursor moves |
|---|---|---|---|
| Main menu | 0:10 | 9 entries: Skill, Item, Equipment, Party, Follower, Quest, Calendar, Memorandum, System; a short subtitle under the focused entry. Money and Mag totals. Y: Archetype Tree. | Focused entry and its subtitle. |
| Skill | 0:30 | A prompt; 8 character rows (name, HP and MP bars with numbers, role tag: Leader, Party or Guide). Right: tabs Archetype and Synthesis (LB/RB); per skill an icon, name, cost (MP or HP) and, on focus, a description. | Focused character changes the skill list; focused skill shows its description. |
| Item | 1:35 | A prompt; 8 category tabs (first labelled Use; the other 7 shown as icons only), switched with the triggers. Per item: icon, name, count; on focus a description. Party rows (name, HP, MP, role tag) on the right as the target list. | Focused item's description. An item that can't be used plays a sound only, no text (1:26). |
| Equipment | 2:05, 2:35 | Character rows, then 5 slots: archetype, weapon, armour, gear, accessory. Header: name, ATK, DEF, archetype, rank. List rows: name, DEF and EVA with an up/down arrow against the equipped item, a count, and an Effect line. A sort key (LB/RB; DEF shown). | Stat change is shown by arrows only. The equipped item has an E marker. Choosing an item another character wears swaps it between them (2:40). |
| Archetype details (Y) | 3:08 | Name, archetype, rank and EXP to next rank, max HP and MP. A 9-element affinity grid (`!` = weak, a shield = resists). St, Ma, En, Ag, Lk, each with a bonus. Tabs Archetype, Inherited, Synthesis (LB/RB). Synthesis rows add a star cost and requirements. A Next Skill row with its needed rank. Buttons: R Rotate, X Swap Skills, Y Archetype Explanation, B Back. | Focused skill's description. X reorders skills (3:51). |
| Party | 4:50 | A prompt; rows: name, HP, MP, level, role tag. A formation strip of 4 slots, each Front or Back. Buttons: LB/RB Formation, X Tactics, Y Details, A Change Party, B Back. | Front/Back shown by an icon only. Adding to a full party (4) plays a sound only. |
| Tactics (Party, X) | 5:42 | Title Auto Battle Tactics; 6 options, each with a description; the current setting for all party members. | Focused option's description. |
| Status (Party, Y) | 5:50 | Name, level, EXP to next level; ATK, DEF; St, Ma, En, Ag, Lk with bonuses; HP and MP as current/max. LB/RB Switch Character, Y Archetype, B Back. | Switching character changes the whole panel. |

### Screens found outside #145's list, with the maintainer's yes or no (2026-11-17)

| Screen | Time | Fields shown | Decision |
|---|---|---|---|
| Follower | 6:10, 6:40 | List of followers; per follower name, archetype, rank (number), an ability list with descriptions and needed rank. LB/RB switches follower. | Already tracked: #147, #199 |
| Quest | 7:25 | 4 tabs (LB/RB, third labelled Ended); per quest status, type (Hunt, Bounty), name, a rank meter of 10 diamonds. On select: distance and details. | Yes; already in #146's scope |
| Calendar, Travellers' Voices | 9:20 | Other players' aggregate choices as 4 percentages with activity lists; date and time of day; LB/RB for other days. | No: online feature (ground rule 5) |
| System: save, load, settings | 10:15 | Save slots; settings. | Settings tracked in #96 |
| Archetype Tree (Y on main menu) | 11:53 | Navigated with the left stick; A selects; highlights the associated character. | Yes; added to #145 |
| Memorandum | not seen | — | No |

### Problems noted in the narration

- **Repeat key fires in menus (3:51).** Pressing the button bound to the dialogue repeat key while in Archetype details
  re-spoke the last dialogue line. The dialogue-window hooks were skipped (`opt-in-dialogue-window.txt` absent), and
  `DialogueControls` acts at any time until those hooks report the window.
- **Money isn't focusable (12:40).** The total is on the main menu only, with no cursor stop.
- **Sound-only feedback:** item can't be used; party full.
- **Visual-only state:** equipment stat arrows, Front/Back icons, the affinity grid, quest rank meters.
