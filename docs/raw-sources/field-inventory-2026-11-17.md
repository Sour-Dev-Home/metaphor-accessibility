# Field: what a sighted player sees and uses while moving (issue #176)

- **Source:** capture evening 1 (#229), session 5, on the maintainer's machine through Reloaded-II, Debug mod build
  `219282c` (branch `capture-1`) with the opt-ins in #229's checklist (screen class, menu values, voice, dialogue
  window and its flag, UI text panes), Steam Input on, controller. Recorded with OBS (33 min 50 s) and narrated; the
  narration was transcribed locally with the `watch` skill (WhisperX small). Frames were taken with ffmpeg 9.0.2 at the
  times the narration names and read by eye. The mod's `message-log.txt` (35 min) was counted by record kind
  (`screen`, `cursor`, `compile`, `msgwindow`, `input`, `pane`); no text was printed into this note. The recording,
  the log and the frames stay local. Only field names, on-screen labels, screen class names, message file name
  patterns, counts and timestamps are kept; no game text. Video time = log time − 51 s, to within a second or two.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. From late-game saves the maintainer walked a town district (15:09–19:15), opened the large map and the quest
   screen (19:16–21:10), travelled to the party's travelling base and walked it (21:30–23:40), walked a dungeon with
   enemies (24:07–29:50), then a second town district with two shops (30:48–33:50).
2. Screen names are the owner classes the screen-class listener logged. Message files are the `compile` records'
   paths (files the game compiled as they were needed), grouped by name pattern.

## Results

### The field HUD

| Item | Where | Town | Dungeon | Notes |
|---|---|---|---|---|
| Deadline countdown and objective | top right | yes | yes | Mission label, "Days Until" a goal, the number of days, the current objective line under it. |
| Date | right | yes | no | Month/day in large figures, weekday, time of day (Afternoon, Night). |
| Weather | under the date | yes | no | Temperature in °C with a weather icon. |
| Minimap | bottom left | yes | yes | Round; the player's arrow; icons for shops, quests and doors; the dungeon's explored rooms. |
| Area name | under the minimap | yes | yes | Two lines: the region or dungeon, then the district or sub-area. |
| Party panel | bottom right | leader only (archetype, HP, MP) | 4 rows (3 small, the leader large) | |
| Button hints | bottom | 9: Follower, Save, Requests, Memorandum (D-pad), Travellers' Voices, Menu, Popularity Ranking, Map, Royal Virtues (X) | 7: Items, Auto Recovery, Skills, Memorandum (D-pad), Travellers' Voices, Menu, Map (RB) | Narration 32:28–32:49 gives the D-pad directions in town: left Follower, up Save, right Requests, down Memorandum. |
| Auto-save notice | bottom left | base only | | Shown on entering the travelling base (22:08). |

### Things the player finds while moving

| Thing | Seen | How a sighted player finds it |
|---|---|---|
| People to talk to | 16:00, 17:12 | A speech-bubble icon over the head from a distance; close by, a prompt "A Talk" with the person's name. |
| Crowd chatter | 15:15, 17:12 | Floating lines near passers-by, each tagged with a call word; the same people show a bubble icon. The narration (15:43) suggests these stay silent by default. |
| Objects to check | 22:08 | Close by, a prompt "A Check" with the object's name (a fixture in the base). |
| Places of interest with a choice | 16:20 | A two-option prompt (go there, not now) with a line; `CommonTalkChoice`. |
| Doors | 24:12–24:20 | The player walks up and pushes; an animation, then the next room. Doors between areas fade to black. |
| Enemies | 24:39–29:04 | Visible on the field; companion lines when they spot or lose the party; a Squad prompt with Y when one is in reach (see `battle-inventory-2026-11-17.md`). |
| Field pickups and field kills | 28:30 | A Result box top-left (EXP, A.EXP, MAG, Money, each with a bonus) and a "New" item line with a count. |
| Companion lines | 24:07–29:46 | Portrait and line top-left while exploring. |
| Chests | not seen | The narration names a chest (24:10) but no frame showed one being opened. |

### Movement and camera

From the narration only: RT held sprints (8:04); B is a dash unlocked late in the game (18:23); X is an overhead
field strike (8:04); LT highlights items nearby (22:10), which the narration suggests as a cue for #182's sonar; the
right stick turns the camera (8:04). Not checked against the log.

### Large map and travel

| Screen | Class | Seen | Fields shown | What changes |
|---|---|---|---|---|
| Large map (RB or Map) | none (no `screen` record) | 19:55–21:00 | Region map; location icons; quest markers; per location a 3-day weather strip (Day 1 to Day 3). Requests, L Location, LT Zoom Out, Y Information or Simple Display, A Confirm, B Back. | A free cursor that snaps to nearby locations; L3 jumps to the player's location (20:44); Y shows the focused location's name and description. |
| Quest list from the map | `CampQuestChoice` | 19:16, 21:04 | As in the menu Quest screen (`menu-screens-inventory-2026-11-17.md`). | The second tab is for reporting finished quests (19:40). |
| Quest details | `CampQuestDetailChoice` | 19:17 | Type, name, requester, description, target and count, deadline, rank meter, reward; a map with the place and the days to get there. Travellers' Voices, To Prep Screen, Back. | — |
| Prep screen | `SortieMenuChoice` | 19:31 | Entries for quests and tactics `[NEEDS VERIFICATION]` (not framed). | — |
| Travel | none | 21:10–21:40 | A travel scene and the days it takes. | — |

### Spike checks (#177, #178, #180)

Not done in the game: the `capture-1` kit had no listener that reads the field world
(`field-position-2026-11-16.md`, `field-actors-2026-11-16.md`, `field-state-and-map-2026-11-16.md` list the reads to
make), so none of their `[NEEDS VERIFICATION]` items changed. What this run adds:

- **#180, state:** the cursor screens the session passed through in the field were `RootMenuChoice` (menu),
  `CommonTalkChoice` (choices), `CampQuestChoice`, `CampQuestDetailChoice`, `SortieMenuChoice`, `CookingChoice`,
  `ItemShopMenuChoice`, `CalendarChoice`, `CooperationMenuChoice` and the `BTL_*` battle screens. The large map,
  travel, doors and plain dialogue had no `screen` record, so a screen class alone can't say "free to move".
- **#180, map name:** the HUD shows the area as two lines (above). As the player entered an area the game compiled
  message files named `<kind>_<4-digit>_<3-digit>_<area code>_<nn>.msg`, where kind is `gaya` (crowd chatter; 9 files
  in the first district, 8 in the second), `npc` (2 and 3), `col`, `door` or `playspot` (the base), and the two
  numbers differ per district (`0001_002`, `0001_003`) and per place (`0030_001` the base, `0016_001`, `0011_007`).
  They look like a major and minor map id, a lead to check against `fldDatabase+0x48` `[NEEDS VERIFICATION]`.
- **#178, nearby things:** crowd chatter and follower field talk compiled one file at a time as the player walked
  past people (15:51–17:14, 23:26–23:31), so a compile is a proximity event the mod already sees
  `[NEEDS VERIFICATION]`. The kinds a player meets: people (talk), crowd (chatter only), objects (check), doors,
  places with a choice, enemies, pickups.
- **#177:** nothing new; the camera turns freely with the right stick, so clock directions need the camera's yaw, as
  that note says.

### Found outside #144's scope, for the maintainer's yes or no

| Screen or feature | Seen | Notes |
|---|---|---|
| Cooking at the base | 22:48 (`CookingChoice`, `CookingComfirmChoice`) | Choose a dish, pick a quantity, confirm. |
| Popularity Ranking (town, D-pad) | 32:49 | Not opened; may draw on other players' data like Travellers' Voices (ground rule 5) `[NEEDS VERIFICATION]`. |
| Royal Virtues (town, X) | 32:49 | Not opened; the protagonist's virtue ranks (narration 11:15). |
| Save from the field (town, D-pad up) | 30:39, 32:59 | Same slots as System's save: slot number, location, date, weekday, time of day, difficulty, playtime, leader, level. |
| Memorandum from the field (D-pad down; `GlossaryListChoice`) | 33:05 | Not described. The menu inventory has Memorandum as No; this is the same screen from the field. |
| Real-time field combat and enemy awareness | 24:39–29:04 | See `battle-inventory-2026-11-17.md`; related: #186. |

### Problems noted

- **Inputs eaten in the field (17:52–18:26).** The narration reports that RB (map) and X stopped working and the map
  wouldn't open. The window hooks logged an open at 17:14 (a follower's field talk) with no close, so their handle
  count stayed at one from then on, while the read-only flag reported no window (`current=0x0 state=6`). Pad reads
  in that window had `buttons=0x0800` (RB) handed on as `0x0000` 23 times. X (`0x8000`, 9 reads, 13:50–19:55), LB
  (`0x0400`, 12 reads, 13:40–32:52) and both bumpers (`0x0c00`, 10 reads, 13:53–20:00) were swallowed the same way. The masks are the dialogue controls' bindings
  (`src/Metaphor.Features.Dialogue/DialogueControls.cs`). This is evidence for #232 (flag against hooks) and #206.
- **Visual-only:** the minimap, the bubble icons over people, the weather strip on the map, the deadline number.
