# Arcana (Persona 3 Reload) at v0.5.0: combat and navigation, compared with ours

- **Source:** GitHub REST API (`gh api`, `gh release view`) on `ChipsAhoiMcCoy/arcana` (Zlib): repository metadata,
  releases `v0.4.0` and `v0.5.0`, and files at `main` HEAD `ed3b005f86b1cde27e30ab299ae91af3d974c988`
  (2026-11-21T12:49:48Z). Latest release: **Arcana 0.5.0**, tag `v0.5.0` (annotated tag object `4074f391`, pointing at
  commit `eae8dbd69e08cfd12973fdfd24c01a3dd09aa1a1`, 2026-11-21T02:38:59Z; pre-release). `main` was 6 commits ahead of
  the tag when read. Nothing was downloaded as a binary, built or run; no code copied.
  - Release pages: <https://github.com/ChipsAhoiMcCoy/arcana/releases/tag/v0.5.0>,
    <https://github.com/ChipsAhoiMcCoy/arcana/releases/tag/v0.4.0>.
  - Files read at `ed3b005` (browse as `https://github.com/ChipsAhoiMcCoy/arcana/blob/ed3b005f86b1cde27e30ab299ae91af3d974c988/<path>`):
    `README.md`, `CHANGELOG.md`, `KNOWN_ISSUES.md`, `CREDITS.md`; combat: `src/screens/battle.cpp`,
    `src/screens/battle_menus.cpp`, `src/screens/battle_details.cpp`, `src/screens/battle_result.cpp` (head),
    `src/core/battle_text.cpp` (head); navigation: `src/nav.cpp`, `src/sonar.cpp`, `src/shadow.cpp`, `src/sight.cpp`,
    `src/world.cpp`, `src/core/poi_text.cpp` (by search).
  - Ours, read the same day: `CHANGELOG.md` (this branch and `origin/main` `48f6ec1`),
    `docs/players/how-to/install-the-mod.md`, open issues
    (`gh issue list -R Sour-Dev-Home/metaphor-accessibility --state open --limit 200`: 145 open), PRs #631, #646, #667
    and merged #642 (bodies read as data, rule 8), `docs/raw-sources/battle-inventory-2026-11-17.md`.
- **Saved:** 2026-11-22.
- **Game build:** not applicable (Persona 3 Reload mod). Arcana's class names, offsets and engine calls are for P3R on
  Unreal Engine 4.27 and say nothing about Metaphor: ReFantazio.

Extends `arcana-v0.1.0-alpha.3-2026-11-16.md` (alpha.3, `c7b8a90`), `field-navigation-accessibility-mods-2026-11-16.md`
(Arcana `v0.3.0` `5060d08` and `main` `306007c`), `arcana-p3r-calendar-shop-quest-screens-2026-11-19.md` (`0649b9b`) and
`persona-4-golden-access-2026-11-16.md`. Those stay as they are.

## What changed since our earlier notes

- **Releases:** `v0.4.0` (2026-11-18) and `v0.5.0` (2026-11-21) are new since the `v0.3.0` read. The repo still reports
  `open_issues_count` 0, no issues in the API listing (`issues?state=all` returned 0 items) and Discussions off. The
  record of player feedback remains the author's dated entries in `CHANGELOG.md` and code comments ("the user's call,
  2026-11-05"). The release notes now ask players to "open an issue".
- **Credits file:** Arcana now has `CREDITS.md`: Grim Dark (Austin Hicks) for "tools and approach", the sonar sounds and
  the harmful-ground sounds used for Shadow sight; Dark Vision (Bradley Renshaw) "where the navigator and the wall tones
  began"; Steam Audio (Valve). Our `CREDITS.md` row still cites `v0.1.0-alpha.3`; a row update is due if an idea
  below is adopted (rule 7).
- **Combat, 0.4.0** (`CHANGELOG.md` "0.4.0 / Battle"): Analyze became a page (name and health, then arcana, level, max
  HP/SP, one line per affinity, skills and attack type, drops; Tab between sections; hides what the game hides);
  Light/Dark affinity swap fixed; a physical skill's HP cost no longer read as damage. New Semicolon key describes a
  Persona or Shadow's look, "written from the game's own 3D models" (release notes 0.4.0).
- **Combat, 0.5.0** (`CHANGELOG.md` "0.5.0 / Battle"): the in-turn situation banner is read (boss charge warnings,
  stat changes, an enemy's wait or flight, "Insufficient SP!"); a fled enemy is no longer "defeated"; A/D target
  change in Analyze restored.
- **Navigation, 0.4.0** (`CHANGELOG.md` "Wall tones", "Sound timing", "Field"): wall tones gain Continuous mode and a
  range setting (1 to 10 m, 5 default) and are computed from the next frame's position (25 to 45 ms sooner); sonar
  beats are sample-timed; top-down mode turns off the game's scene cameras and hand-turns two rooms ("Keep your heading
  through passages"); auto-walk fixes (stairs landing, "goes round to another side instead of saying Stuck").
- **Navigation, 0.5.0** (`CHANGELOG.md` "0.5.0 / Field"): number keys 1 to 7 pick a category directly; Comma no longer
  says the category; **Shadow sight** zones (below); strike-reach ring on any side with Strike assist; more named NPCs;
  "Unknown Arcana" for unmet Social Link partners. Unreleased on `main`: all field sounds go quiet "whenever the game
  takes the hero from you", so "silence means wait".
- **Settings:** mod keys can be remapped (0.5.0); the settings menu pauses the game while exploring (0.5.0).
- **Known issues** (`KNOWN_ISSUES.md`, release notes 0.5.0): top-down mode "has had little play"; the right-stick scan
  has no keyboard key; nobody has finished the game at a player's pace; "A game update can break the mod."

## Combat: how Arcana does it (code)

- **State source:** reflection. `src/screens/battle.cpp` L1-13: GUI state from `ABtlGuiStateManager.nowState`
  (TOP 1, SKILL 2, ITEM 3, TARGET 4, ...), whose turn and the wheel's target from `BP_BtlTargetManager_C`, names and
  numbers from the game's battle database (`UBtlDataBaseComponent: GetUnitName, GetUnitHP, GetSkillName,
  GetSkillCost, GatAnalizeAttr`), events from `UBtlCoreComponent` (PlayerList, EnemyList, CurrentPhase, CurrentAction).
- **No command list of its own:** "The wheel is played with the game's own keys (the player's call, 2026-11-01: no
  command list); F1 names the open ones" (`battle.cpp` L10-11). Target position "1 of 2" comes from the game's sorted
  enemy list (L11-13); targets do not wrap (README "Battles").
- **Lists are mod menus that move the game's cursor:** `src/screens/battle_menus.cpp` L1-18: skills, items, targets,
  Survey, Theurgy, Shift, Tactics each read from their Blueprint's list and index; the mod moves the game's cursor
  (`UBtlGuiListComponent.SetCursor`, or the list's own move events for targets and Survey).
- **What happened:** every unit's HP is watched; the skill-name banner (`BP_BtlSkillNamePanel_C`) is said once per
  panel with the actor's name (`battle.cpp` L107-124); hit words come from the damage popup's type (5 WEAK, 6 CRITICAL,
  7 RESIST, 8 MISS, 9 BLOCK, L138-143) because "A miss or a block changes no HP". The situation banner
  (`BP_BtlGuiJyokyoHelp_C`) is rendered from the game's own BMD page with its variables filled (L172-262).
- **Own choices not read back:** the next banner of a member the player commanded is skipped, and damage to the one
  target the player chose is the number alone (L127-133; "the user's call", 2026-11-05 and 2026-11-10).
- **Details buffer:** right stick or Ctrl+Up/Down walks a line list for whoever is in focus (name, HP, SP, ailments and
  buffs with turns left, affinities found, Persona and stats); "the Say the Spire model" (`battle_details.cpp` L1-7).
- **Advantage and pacing:** "S.E.E.S. advantage"/"Shadow advantage" from `FBtlEncountParam.Preemptive` once per battle
  (`battle.cpp` L352-355); a countdown battle speaks time left at set intervals (README "Battles").
- **After battle:** Shuffle Time cards, Overstock, a victory screen read as lines you can arrow back over (README
  "After battles"; `battle_result.cpp`).
- **Field side of combat (Tartarus):** `src/shadow.cpp` header: Shadows from `AFldEnemyManager.Enemies`, "noticed"
  from the exclamation mark and `TrackingTarget`; the strike-reach ring uses the hero's own attack box
  (`UFldPlayerDungeonComp.AttackArea`, measured 2.7 m ahead, 1.6 m each side), so "the strike connects the moment any
  of the Shadow's body touches the box". Strike assist turns the hero to a Shadow in reach as the swing starts (README
  "Sounds"). **Shadow sight** (`src/sight.cpp` L1-31): models the game's own noticing test (distance, yaw within the
  mood's cones, a line trace that walls block, Jamming), and sizzles from the nearest point of the wall-cut zone;
  "After Grim Dark's harmful ground (GD Access, by Austin Hicks)".

## Navigation: how Arcana does it (code)

- **Points of interest** from the game's own hit and NPC actors and name tables (`field-navigation-...-2026-11-16.md`
  §1, unchanged in kind). Spoken as "name, distance, clock direction, n of m"; 12 o'clock "is where pushing forward
  walks; in top-down mode, the compass" (README "Points of interest"). No spoken height: a search of
  `src/core/poi_text.cpp` for above/below/height found only floor names in place names. Up and down are heard only
  through the optional Steam Audio HRTF mode (README "Sounds"); other floors are listed only when reachable by stairs.
- **Path source:** the engine's navmesh at runtime: `src/world.cpp` L447-471 calls
  `UNavigationSystemV1::FindPathToLocationSynchronously` and `K2_ProjectPointToNavigation` through reflection.
- **Route ping** (credited to Grim Dark, `src/sonar.cpp` L229-235): one sound per focus: "straight" when the navmesh
  path is no longer than the straight line × 1.2 + 1 m and the thing is in sight, "round" otherwise, "unreachable" with
  no complete path. It says the kind of route, not which way to go.
- **No self-steered path guidance.** The continuous beacon was removed in 0.2.0 as redundant with the sonar and
  auto-walk (`field-navigation-...-2026-11-16.md`, `CHANGELOG.md` 0.2.0). The only way to follow a path is
  **auto-walk**: `src/nav.cpp` L89-94 steers with `AddMovementInput` along the path line ("any angle ... no camera stands
  between"), falling back to the 8-way movement keys; waypoints dropped within 70 cm (L65, L501-503); stuck handling
  with side-steps, circling, an A* over capsule-swept floor points for rooms whose navmesh is cut into islands
  (L266-270), and doorways (L163-171). Shift+Period walks and presses Space once the prompt is up (L117-128).
- **Camera frame:** default is **top-down mode**, which replaces the game camera with a north-up one while exploring,
  turns off scene cameras, and hand-turns rooms whose entrance would flip "up" (README "Top-down exploration";
  `CHANGELOG.md` 0.4.0 "Field"). With the mode off, sound and directions follow the game camera.
- **Wall tones, sonar, area text, where-am-I** as in our 2026-11-16 note, with the 0.4.0 timing and range changes above.

## 1. Feature table

"Have" = on `main` or in this branch's `CHANGELOG.md`; "Building" = an open PR; "Planned" = open issue only;
"Missing" = no issue found in the 145 open issues.

### Combat

| Feature | Arcana 0.5.0 | Ours |
|---|---|---|
| Battle start, advantage / ambush | Have | Planned (#251, #626) |
| Whose turn, with HP/SP | Have | Planned (#252; state spike #287) |
| Command wheel keys on request | Have (F1) | Planned (#252, R3) |
| Target name, health, place "1 of n", affinities | Have | Planned (#253, #164); PR #631: "No target name: I found no safe way to read it" |
| Skill and item lists with cost and description | Have | Building (PR #631, PR #646 fix) |
| Damage, healing, defeats | Have | Building (PR #631 "35 damage, 89 HP left") |
| Hit words: Weak, Critical, Resist, Miss, Block | Have | Building (PR #631 hit sounds, #636); words Planned (#247) |
| Enemy and auto actions (skill banner) | Have | Planned (#247, #251) |
| Situation banner (boss warnings, stat changes, "Insufficient SP") | Have (0.5.0) | Missing as its own item; closest #254 |
| Details of the unit in focus (buffs with turns, ailments) | Have | Planned (#164) |
| Analyze as a page | Have | Planned (#255) |
| Tactics / Rush | Have | Planned (#257) |
| One More / Shift, All-Out | Have | Missing (Metaphor's equivalents [NEEDS VERIFICATION]: no issue names press-turn icons beyond #287) |
| Theurgy ready | Have | Planned for Metaphor's Synthesis (#260) |
| Escape, Pass, Retry, Assist, Formation, Party swap | n/a or partial | Planned (#256, #261, #262, #263, #259, #258) |
| Results screen | Have | Planned (#166) |
| Describe an enemy's look | Have (0.4.0) | Missing |
| Field: enemy noticed / lost | Have (game's mark) | Planned (#649) |
| Field: strike in reach, strike assist | Have | Planned (#186) |
| Field: sight zones before you are seen | Have (0.5.0) | Missing |
| Field kills without a command battle | not in P3R | Missing (Metaphor has them: `battle-inventory-2026-11-17.md` "Real-time field combat") |

### Navigation

| Feature | Arcana 0.5.0 | Ours |
|---|---|---|
| Nearby list, nearest first, distance and direction | Have, 7 categories | Have (#181, `N`, 8 things, rough steps) |
| Category filters / direct category keys | Have (brackets, 1 to 7) | Missing |
| Camera-relative directions | Have (camera, or compass in top-down) | Planned (#686: list still character-relative; beacon already camera-relative) |
| Where am I (area, objective) | Have | Have (place name, #181); mission Building (PR #665, #658) |
| Area name on arrival | Have | Planned (#586 map region) |
| Interaction prompts / "in reach" | Have | Building (PR #645, #644) |
| Straight-line beacon to a target | Removed in 0.2.0 | Building (PR #667, #183) |
| Path guidance the player follows | Missing (auto-walk only) | Planned (#690, on #642's walk graph) |
| "No path" / route-kind cue | Have (route ping, 3 sounds) | Planned (#183 follow-up) |
| Navmesh / path data | Have (engine call) | Have offline reader (#642 merged; #179 open) |
| Sonar of nearby things | Have | Planned (#182) |
| Wall tones | Have | Planned (#185); tone engine Have (#637) |
| Auto-walk, walk-and-use | Have | Planned (#184, never presses interact) |
| Objective guidance | Missing | Planned (#652) |
| Spoken height | Missing (HRTF only) | Planned (#650) |
| Describe the area (hand-written) | Have | Missing |
| Top-down / north-up mode | Have (default) | Missing |
| Right-stick scan by direction | Have (no keyboard key) | Missing |
| Town map / travel list | Have | Planned (#545, #586) |
| Field sounds silent when control is taken | Have (unreleased) | Missing as a rule |
| Named talking objects | Have (names tables) | Planned (#687) |

### Other areas (one line each)

- Dialogue and voiced lines: both have; Arcana adds cutscene audio description (Kokoro narrator) and Persona/Shadow
  descriptions; ours has none and no issue for audio description (#659 is only a subtitles spike).
- Menus: Arcana reads most screens (`docs/menu-coverage.md`); ours reads title, main menu, choices and a growing list
  (#155 and the "Menus:" issues).
- Settings: Arcana has an in-game spoken settings menu and key remapping; ours plans one (#662, #668 toggles, #664
  verbosity).
- Help: Arcana's F1 per screen; ours plans a welcome naming the help key (#617) and player docs (#596).
- Install: ours is a spoken installer with pinned, hashed downloads and a signed zip (`CHANGELOG.md`); Arcana is a
  manual copy of `dsound.dll` and a folder with a `.sha256`.

## 2. Gaps to close to match the feature count

Ordered by what blocks play first.

1. **Target name and affinity in battle** (#253, #164, behind #287). Without it a turn cannot be played on purpose;
   PR #631 records that no safe read was found yet.
2. **Whose turn, start-of-battle and advantage lines, enemy action banners** (#251, #252, #626, #247).
3. **Hit words and the situation banner** ("Weak", "Resist", boss charge warnings, "Insufficient SP"): words under #247;
   the banner has **no issue** (#254 is the nearest).
4. **Sonar and wall tones** (#182, #185): Arcana's two always-on field sounds; our cue engine (#637) is ready for them.
5. **Field enemy cues: noticed, in reach, strike assist, and sight zones** (#649, #186; sight zones **no issue**).
6. **Auto-walk and walk-and-use** (#184). Our issue forbids pressing interact; Arcana's Shift+Period does press it
   once the prompt shows. Matching the count means either accepting that or counting arrival-at-prompt as the match.
7. **Route-kind / "no path" cue** (#183 follow-up to PR #667).
8. **Category keys for the nearby list** (**no issue**; #181 has one undivided list).
9. **Analyze page, details of the unit in focus, results** (#255, #164, #166).
10. **Describe the area, describe an enemy's look, and cutscene audio description** (**no issue** for any). These are
    author-written content in Arcana, high effort per area or model.
11. **In-game settings menu and key remapping** (#662; remapping of mod keys **no issue**; #563/#564 are the game's own
    remap screens).

## 3. Where we can do better

1. **Guidance the player steers, along a real path (#690).** Arcana's only way to follow a route is to hand control
   to auto-walk; its route ping says straight, round or none, never which way (`src/sonar.cpp` L229-235). Its
   `nav.cpp` comments are a list of stuck cases (people on the floor, desks splitting the navmesh, doors, things in
   walls). A beep that points at the next corner of a navmesh path keeps the player in control and degrades gracefully
   where the mesh lies: the player walks round the person. Our path data is read offline from the map's `.NAV`
   (Recast tile cache, PR #642: 374/374 files parsed, 88 tests on made-up files), so it needs no engine call on the game
   thread and is unit-testable, where Arcana's comes from `FindPathToLocationSynchronously` through reflection
   (`src/world.cpp` L447). Caution grounded in their history: they removed a beacon as "one more sound" once sonar and
   auto-walk existed, so ours should go quiet or merge with the sonar's selected-thing sound when both exist.
2. **Camera-relative frame without taking the camera (#686).** Arcana's default replaces the game camera with north-up
   and then needs per-room fixes (scene cameras off, two rooms hand-turned) and still lists the mode as having "had
   little play" (`KNOWN_ISSUES.md`, release notes 0.5.0). Ours reads the game's camera and speaks and pans relative to
   it, with no camera writes, so every map works the same with no hand-authored turns. A compass option can come later
   if players ask.
3. **Say height and use Metaphor's own combat shape.** Arcana speaks no height and relies on optional HRTF for
   up/down; #650 makes height a spoken word that works in plain stereo. And Metaphor's field strikes can kill weak
   enemies with a Result box and no command battle (`battle-inventory-2026-11-17.md`); speaking those results and the
   Squad-prompt-in-reach (#186) is a Metaphor-specific feature Arcana has no analogue for. Per-hook build checks (ground
   rule 2) also avoid Arcana's admitted "A game update can break the mod".

Credit: none of the above copies code. If an idea is adopted (the route-kind cue, the sight-zone model, the
details-buffer pattern, "own choices not read back", "silence means wait"), credit Arcana, `ChipsAhoiMcCoy/arcana`,
Zlib, the commit above and the file named, in the code and in `CREDITS.md` (rule 7); the route ping and sight sounds
are Grim Dark's ideas in turn (Austin Hicks, zlib), and Arcana's details buffer names "Say the Spire" as its model
([NEEDS VERIFICATION]: that project's repository and licence were not read).
