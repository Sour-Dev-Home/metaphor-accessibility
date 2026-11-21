# The Last of Us Part II's accessibility options, and which of them this mod can have

The maintainer asked (2026-11-21) which of The Last of Us Part II's (TLOU2) accessibility options, blindness first, this
mod could build without changing the game itself. This page explains the answer option by option. TLOU2's options come
from PlayStation's and Naughty Dog's own pages, saved in
[`tlou2-accessibility-options-2026-11-21.md`](../../raw-sources/tlou2-accessibility-options-2026-11-21.md). What
Metaphor allows comes from this repository's findings, ADRs, issues and open PRs, each linked or numbered (numbers are
issues and PRs on `Sour-Dev-Home/metaphor-accessibility`). Nothing about Metaphor here is new evidence: where the
repository doesn't know, the row says `[NEEDS VERIFICATION]`.

Classes:

- **a**: done or in progress (an open issue or PR builds it).
- **b**: feasible with read-only hooks or reads plus our own speech and sound.
- **c**: feasible, but needs writing game state or adding input, which makes it a behaviour-changing hook under
  [ADR 0007](../../adr/0007-behaviour-changing-hooks.md): its own security review, never riding along with other work.
- **d**: unreasonable without changing the game: new or edited assets, re-authored scenes, rendering changes, or logic
  only reachable in protected code ([ADR 0010](../../adr/0010-text1-hooks-stay-opt-in.md), and no hooks on `.ecode`
  jump stubs into `.text1`).
- **n/a**: no counterpart in Metaphor (turn-based battles; real-time field exploration with no shooting, stealth
  takedowns or climbing that the repository has met).

## Summary

| TLOU2 option | Class | Why, in short | Issue / PR |
|---|---|---|---|
| Text-to-speech of on-screen text and menus | a | The mod is a screen-reader bridge (Prism, ADR 0005) for dialogue, menus, settings, tutorials | #211, #96, #560, #118 / PR 630 |
| Status description on a key | a | "Where am I" in the field, the turn line in battle, day and time | #181 / PR 643, #252, #192 |
| Vision preset | b | Ship the mod's own settings on by default; changing the game's settings for the player would be c | none |
| Enhanced listen mode (scan for items and enemies) | a | Nearby list by kind, nearest first, then sonar | #181 / PR 643, #182 |
| Listen mode's pitch for height | b | The nearby code already reads each unit's height; say or pitch "above" and "below" | none |
| Scan range and time | b | The list's radius is fixed at 30 steps today; a setting is plain library code | none |
| Navigation assistance: guidance toward a target | a | Spoken or tone guidance to a picked target, with a "no path" cue | #183 |
| Navigation assistance: turn the player toward the target (L3) | c | Needs added stick input; the camera's and player's facing are read-only to us today | none (#184 is the walk) |
| Navigation assistance toward the story objective | b | Needs where the objective is; the objectives spike hasn't found positions | #191 |
| Auto-walk (not in TLOU2; the same need) | c | Holds the game's own movement input, never writes position | #184 |
| Traversal cues: things in reach (doors, chests) | a | Each gimmick's reach test is readable `.ecode`; the mod repeats it | #644 / PR 645 |
| Traversal cues: walls, edges, gaps | b | Navmesh cells from the `.NAV` files; a ledge sounds like a wall | #185 |
| Ledge guard: warn | b | Same wall tone; whether the field has falls that matter is `[NEEDS VERIFICATION]` | #185 |
| Ledge guard: stop the fall | d | Movement and collision run in the engine; stopping a step means swallowing input blind to physics | none |
| Combat audio cues (hits, incoming attacks) | a | Battle hits and outcomes spoken; the mod's own sound cues | #247 / PR 631, #636 / PR 638, #254 |
| Combat cues in the field (an enemy strikes, from which side) | b | Enemy positions and AI state are readable | #186 (in part) |
| Awareness indicators (an enemy noticed you) | b | An enemy's AI state is a readable index; a discovery sets a bit on the world | none (#186 in part) |
| Separate volume for accessibility cues and speech | a | `soundCueVolume` in settings; speech volume is the screen reader's | #636 / PR 641 |
| Audio cue glossary | b | A key or menu that plays each cue with its name; our sounds, our data | none |
| Lock-on and auto-target, in battle | a | Battle targets are a cursor; the mod speaks the target | #253 |
| Lock-on in the field (face an enemy to strike) | c | Turning means added input; telling when a strike lines up is b | #186 |
| Hints when lost | a | Gallica's corner advice, the objectives | #587, #191, #192 |
| Pick-up notifications | a | The loot and item-get banner | #542 |
| Subtitles with speaker names | a | Every line spoken with the speaker on change | #81, #539, #57 |
| Speaker direction arrow | b | The nearby list's positions give a speaker's side; low value | none |
| Cinematic audio descriptions | b | Our own written descriptions, spoken per event scene; a large authoring job. Mixed into the game's audio it would be d | none |
| Skip puzzle | d | Means setting the game's own progress flags; whether Metaphor has blocking puzzles is `[NEEDS VERIFICATION]`. Spoken solutions are b | none |
| Input remap | a | The game has its own key configuration; the mod speaks it | #589, #563, #564 |
| Hold-to-toggle, repeated presses as a hold | c | Means holding or repeating input for the player; which Metaphor inputs need it is `[NEEDS VERIFICATION]` | none |
| Auto pick-up | c | Means pressing interact for the player; #184 rules that out for the walk | none |
| Camera assist | c | Means added camera input; little value for a player who can't see the view | none |
| Difficulty levels | a | The game's five levels, spoken | #540 (closed) |
| Custom difficulty sliders, reduced perception, invisible while prone | d | Rewriting combat and AI tuning the game doesn't expose; a write to an enemy's AI state is c at best and untested | none |
| High contrast display | d | Rendering change | none |
| Screen magnifier | d | Rendering change in-game; Windows Magnifier works outside the mod | none |
| HUD scale, colour, background, colourblind, flashing | d | New or edited UI assets | none |
| Translation prompts (in-world text) | b | If signs come from message files the mod already reads; `[NEEDS VERIFICATION]` | none |
| Speech to vibrations, combat and guitar vibration cues | b | Rumble from our own process through the pad API; the game drives the same motors. For deaf players, low priority here | none |
| Motion sickness options (shake, blur, field of view, centre dot) | d | Rendering and camera changes; little value for blind players | none |
| Slow motion | n/a | Battles are turn-based; field speed is the engine's | none |
| Enemies don't flank, hostages, allies grabbed, enhanced dodge, weapon sway, arc-throw, auto weapon swap | n/a | No shooting, grabs or cover in Metaphor's field | none |
| Traversal assistance, infinite breath, rope, boat, guitar, motion-sensor aiming, film grain | n/a | No such traversal or minigames met so far `[NEEDS VERIFICATION]`; film grain is visual | none |

Counts: **a** 13, **b** 13, **c** 6, **d** 7, **n/a** 3 (rows; a row can group several options).

## Suggested new issues

The five worth most, all class b, none tracked yet:

1. **Field: cue when an enemy notices you, and when it gives up.** TLOU2's awareness indicators by sound. The
   [enemy awareness note](../../findings/field-enemy-awareness-static.md) found the AI state index and the discovery
   bit; its "what would settle it" reads come first.
2. **Field: height in the nearby list and sonar.** TLOU2's listen-mode pitch. PR 643 already reads each unit's
   height; this adds "above" or "below" past a threshold, or a pitch offset once sonar (#182) exists.
3. **Sound cues: a glossary.** A key, or a list on the help key (#618), that plays each cue and says its name, so
   a player can learn them before needing them.
4. **Navigation: guidance to the current objective.** TLOU2's navigation assistance points at the story path. Needs a
   position for the objective, which #191 hasn't found; a hand-written waypoint per chapter step (our data, chapter by
   chapter, ADR 0016) is the fallback.
5. **Cinematic descriptions, spoken.** Short written descriptions of the non-interactive scenes, keyed to the event
   scene and spoken between lines. Starts with chapter 1's opening (the
   [chapter 1 inventory](../../raw-sources/chapter1-interaction-inventory-2026-11-20.md) row 1: "Dark animated scenes
   with subtitles only"). Needs a spike on how to know which scene is playing and when.

Smaller ones: a setting for the nearby list's radius; a vision preset (every speech and cue setting on, documented as
the default, so no player types a switch); spoken speaker direction.

## Detail

### Speech: text-to-speech, status on a key, subtitles, hints (a)

TLOU2's text-to-speech reads on-screen text and menus, and a touchpad swipe gives a status description (source 2 of
the raw source). This is the mod's core: Prism speaks through the player's screen reader
([ADR 0005](../../adr/0005-prism-speech-backend.md)) and the features speak dialogue, menus, settings, tutorial pop-ups
and name entry. Status on a key maps to "where am I" (#181, PR 643), battle's turn line (#252) and the day and time
(#192). Subtitles with speaker names map to dialogue with the speaker said only on change (#539). TLOU2's "story and
combat dialogue" subtitles, which include enemies' barks, have a battle counterpart in battle voice lines; whether those
carry text the mod can read is `[NEEDS VERIFICATION]`. Hints map to Gallica's corner advice (#587) and to the
objectives (#191, #192).

### Scanning and listen mode (a, plus a b for height and range)

Enhanced listen mode scans for items and enemies within 10-30 m, and gives height by pitch. PR 643's nearby list (up to
eight units within 30 steps, nearest first, by kind) is the spoken counterpart, and sonar (#182) the sound one. The
[units](../../findings/field-units-names-exits-static.md) and
[position](../../findings/field-position-facing-map-static.md) notes give each unit's position, including height, so
"above" and "below" need no new read. The 30-step radius is the maintainer's "about 30 m" in PR 643; a setting for it is
library code.

### Navigation and traversal (a and b; turning and walking are c)

- **Guidance** (#183) and **wall tones** (#185) need only reads: the player's position and facing, a target's position,
  and the map's navmesh cells read from its `.NAV` file in `base.cpk` without calling the game
  ([navmesh files note](../../findings/field-navmesh-files-tile-cache-static.md)). The same note says a ledge reads as
  a wall, since the cells mark where an agent can stand, and that a file read misses run-time obstacles such as a
  closed gate. That covers TLOU2's ledge, gap and crouch cues as far as Metaphor has them.
- **Things in reach** (TLOU2's pickup and glass cues): the
  [interaction prompt note](../../findings/field-interaction-prompt-static.md) found each gimmick's reach test in
  readable `.ecode`, while the prompt's target is picked in protected code. #644 (PR 645) repeats the test from our side.
- **Turning toward a target** (TLOU2's L3) and **walking to it** (#184) both move the player, so both add input. The
  [navigation survey](../../raw-sources/field-navigation-accessibility-mods-2026-11-16.md) (section 5) recommends
  holding the game's own movement input over writing position, and lists teleporting and position writes under "avoid
  first". Adding input keeps collision and animation native and stays within ground rule 5: it's single-player, and it
  touches no protection. The risks are the ones #184 lists: it must stop on any player input and on dialogue, a menu, a
  battle or a map change, and it must never press interact. Calling the game's own path query (`0x13f19b0`) would need
  a rule for calling game functions that doesn't exist yet
  ([navmesh files note](../../findings/field-navmesh-files-tile-cache-static.md)).
- **Stopping a fall** (ledge guard's second half) and **traversal assistance** would mean deciding, inside the engine's
  movement, when a step is fatal. The mod sees cells, not the physics body (the same note: the real body is in the
  PhysX scene, in `.text1`), so swallowing a step would be guesswork. Class d.

### Combat (battle a; field b and c)

Battles are turn-based, so TLOU2's aim options mostly vanish: picking a target is a cursor the mod speaks (#253), hits
and HP left are spoken (#247, PR 631), and the enemy's turn and outcomes are #254 and #626. TLOU2's combat cues ("enemy
grabs, incoming melee, arrows, aiming, hits") become the mod's own sound cues (#636, PR 638; overlapping channels and
stereo, #637, PR 641, which also adds `soundCueVolume`).

The field is real-time: the player strikes or engages enemies to start a fight (#186, #626's pre-emptive banner). The
[enemy awareness note](../../findings/field-enemy-awareness-static.md) found a `u16` AI state index on each enemy's
controller and a discovery bit on the world, so an "enemy noticed you" cue and a "gave up" cue are reads (b). Lining up
a strike can be told from positions and facing (b); turning to face the enemy is added input (c). TLOU2's "reduced
enemy perception" and "invisible while prone" would mean writing that AI state or the discovery bit every frame against
logic we can't see: untested, likely to desync the AI, and not something ADR 0007 has been asked to cover. The rest of
TLOU2's combat options (flanking, grabs, dodge, weapon sway, throwables) have no Metaphor counterpart.

### Difficulty (a for levels; d for sliders)

Metaphor offers five levels, Storyteller to Regicide
([difficulty note](../../findings/difficulty-select-shared-cursor.md)), and the mod speaks the list (#540). TLOU2's
separate sliders for damage, enemies, allies, stealth and resources would mean rewriting the game's tuning. The
[settings inventory](../../findings/settings-screens-inventory-static.md) lists a Difficulty section; whether it holds
more than the level is `[NEEDS VERIFICATION]`, and anything it does hold is the game's, for the mod to speak (#560).
Sliders the game lacks are class d.

### Controls (a for remap; c for holds, toggles and automation)

Metaphor has its own key configuration (keyboard, mouse, controller tabs in the settings inventory), so remapping is the
game's, and the mod's job is speaking it (#589, #563, #564). Turning holds into toggles, repeated presses into a hold,
auto pick-up and camera assist all mean the mod presses or holds inputs for the player: class c, each its own
behaviour-changing hook. Which Metaphor inputs need a hold or repeated presses is `[NEEDS VERIFICATION]`; build none of
these until a by-ear run finds one that blocks a player.

### Cinematic descriptions (b, large)

TLOU Part I's director called audio descriptions for cinematics "the biggest new feature" (source 4), and Part II
Remastered added them in 2024 (source 5), only in languages with audio. The studio recorded them into the game. The mod
can't add to the game's audio (d), but it can speak its own written descriptions between a scene's lines, which changes
nothing in the game: our text, our data, shipped like any other file of ours (ground rule 4 covers game content, not
descriptions we write). The costs are authoring every scene, keeping spoilers to what's on screen, and timing: the mod
must know which scene and which moment is playing. The [event scene lines note](../../findings/event-scene-lines-skip-msgrequest.md)
shows some scenes bypass the usual line function, so the timing route is its own spike. Pre-rendered scenes, if any,
would have no lines to hang descriptions on, and whether chapter 1's opening is one is `[NEEDS VERIFICATION]`.

### Visual and low-vision options (d)

High contrast, the magnifier, HUD scale, colour, background, colourblind palettes, flashing and the motion sickness
options all change what the engine draws, or need new UI textures. The mod has no rendering hooks and ships no game
assets (ground rule 4; edited game textures would be derived game content). Low-vision players can use Windows
Magnifier and high-contrast settings outside the game; whether they reach a full-screen game is outside this mod.

### Skip puzzle (d; hints are b)

TLOU2 recommends skipping puzzles for blind players. Skipping means setting the game's progress flags so the script
moves on, which is writing game state in a script system the repository has only begun to read. Whether Metaphor has
puzzles that block a blind player at all is `[NEEDS VERIFICATION]`; the chapter inventories should say. If one does, a
spoken walkthrough of that puzzle, written by us, is the b route.

### Presets (b)

TLOU2's vision preset turns on the recommended options. The mod's equivalent is its defaults: every speech and cue
feature on out of the box, so no player has to find a setting or type a switch. Having the mod change the game's own
settings for the player (say, subtitles on, auto-advance) would write game state: c, and not needed yet.

## Not covered

- No GDC talk on TLOU2's accessibility was found, so nothing here says how Naughty Dog built any option (raw source,
  "Not reached").
- Feasibility is judged from what the repository records on Steam build 18330018 (the findings' build); nothing was run
  in the game for this page.
