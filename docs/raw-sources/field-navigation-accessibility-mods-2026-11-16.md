# Field navigation in screen-reader accessibility mods: Arcana and the Final Fantasy mods

- **Source:** GitHub REST API (`gh api`, `gh search`-style repository search through `gh api search/repositories`) of
  the repositories listed in "Sources": README files, changelogs, known-issue lists and a few source files, read
  2026-11-16. Nothing was downloaded, built or run. Quotes are short and from those files.
- **Saved:** 2026-11-16
- **Game build:** none. These are other people's mods; nothing here is verified against a Metaphor: ReFantazio build.
  Section 5 is a suggestion list, not a finding about Metaphor.

Feeds issue #144 (field navigation). Extends `arcana-v0.1.0-alpha.3-2026-11-16.md` (alpha.3, `c7b8a90`) and
`arcana-research-credits-and-pace-2026-11-16.md`. Credit rule: any idea or code taken from these goes into the code
comment, `CREDITS.md` and the commit, naming author, repo, licence, commit and file.

## Sources

`gh api` reads, 2026-11-16. "HEAD" = default-branch tip at read time. Licence = the API's `license` field plus whether
a `LICENSE` file showed in the repo root listing; "none declared" means the field was null and no licence file was
seen (default copyright applies: nothing may be copied without asking the author).

| Project (game) | Author | Licence | Repo | Commit read |
|---|---|---|---|---|
| Arcana (Persona 3 Reload) | ChipsAhoiMcCoy | Zlib | <https://github.com/ChipsAhoiMcCoy/arcana> | `main` HEAD `306007c065c694689a2ad605bd51d6a5b323d32f` (2026-11-16T19:48:04Z); latest tag `v0.3.0` = `5060d0834df9a623458ce67571b2c40bd2491c5f` (release 2026-11-16T05:00:06Z; lightweight tag; `main` is 11 commits ahead of it); the tag our issue cites, `v0.1.0-alpha.3` = `c7b8a908eb6882382a348cbf94a945e79486df10` |
| Grim Dark / GD Access (Grim Dawn), Arcana's ancestor | Austin Hicks | zlib (copy in Arcana `LICENSES/grim-dark-zlib.txt`) | <https://github.com/ahicks92/grimdark> | not read directly; known only through Arcana's `THIRD_PARTY_NOTICES.md` |
| Dark Vision / WrathAccess (Pathfinder: Wrath of the Righteous), Grim Dark's ancestor | Bradley Renshaw | zlib (per Arcana notices) | <https://github.com/bradjrenshaw/darkvision-wotr> | not read directly; as above |
| FF6ScreenReader (FFVI Pixel Remaster) | BlindGuyNW (ZKline) | none declared | <https://github.com/BlindGuyNW/FF6ScreenReader> | `dev` HEAD `40a3427140c3fc481dab1609cd2f1f33df9b4617` (2026-03-17); release `v2.0` |
| FFPRCore-Screen-Reader (FF I-VI Pixel Remaster) | bladestorm360 | none declared | <https://github.com/bladestorm360/FFPRCore-Screen-Reader> | `4399119bf2eb8d2dc665310f455d1017033420b4` (2026-11-11); release `v1.0` |
| FF1-Screen-Reader-Enhanced (FF Pixel Remaster) | atreides825 | none declared | <https://github.com/atreides825/FF1-Screen-Reader-Enhanced> | `4bae9a57ac8d284ef4c71a4a78f58900db8a8f9d` (2026-06-26) |
| FFX-X2-Screen-Reader (FFX HD Remaster) | bladestorm360 | none declared (its issue #2 is titled "chore: add MIT license"; no licence file at the root at read time) | <https://github.com/bladestorm360/FFX-X2-Screen-Reader> | `d63277653437682234ccfe1f569a4b13dd558ace` (2026-11-04); v1.0.2 |
| Blind Soldier (FFVII, PC original) | buu420 | none declared | <https://github.com/buu420/blind-soldier> | `367155bcc6f78d8d1610b96b1c1ba2046ddfde16` (2026-11-16); release `v0.10.17` |
| Echos of Gaia (FFIX) | buu420, on BlindGuyNW's Memoria fork and Albeoris's Memoria | MIT (keeps Memoria's licence) | <https://github.com/buu420/echos-of-gaia> | `6a5aab8d3e46fc459584cd4e34b8e3bbba53e2f8` (2026-11-16); v0.1.20 beta |
| FFVIII-Accessibility-Mod (FFVIII Steam 2013) | ampage87 | MIT | <https://github.com/ampage87/FFVIII-Accessibility-Mod> | `3610ef15b5446f3f06f4aa777160467186f3dd3a` (2026-08-30); v0.12.25 |
| ffrs-accessibility (Final Fantasy Resonance, Unreal) | derbruedi | none declared (README: UE4SS MIT, Prism MPL-2.0 for the parts it ships) | <https://github.com/derbruedi/ffrs-accessibility> | `396dadc13771677554a2f32b07d1dc19e6b5ffa3` (2026-11-15) |
| blackmoon-prophecy-accessibility (FF Blackmoon Prophecy Remake, an RPG Maker MV fan game) | bladestorm360 | none declared | <https://github.com/bladestorm360/blackmoon-prophecy-accessibility> | `4cae8f43a608c406c2aae0332fe1f21db34f4455` (2026-11-12) |

Search done (repository search, 2026-11-16): "final fantasy accessibility screen reader", "pixel remaster screen
reader", "final fantasy blind accessible mod", "NVDA final fantasy", "final fantasy blind", plus "FFXIV ... dalamud",
"final fantasy tactics / XII / IV / V" variants. Every hit above is listed. FFXIV returned only
`chrisalleng/ColorBlindAid` (AGPL-3.0), a colour recolouring plugin with no navigation; FF Tactics, FFXII, FFXIII and
Final Fantasy Remake/Rebirth returned nothing. Other FF mods may exist outside GitHub (Discord, Nexus):
[NEEDS VERIFICATION]. Several repos name each other: FFPRCore credits "ZKline / BlindGuyNW: original FINAL FANTASY VI
screen reader and permission to include its work"; FF1-Screen-Reader-Enhanced credits ZKline's mod; FFVIII credits
FF6ScreenReader as "the accessibility mod ... that inspired this project"; Echos of Gaia says its "navigation and
sound work draws on Blind Soldier and Foresight" (Foresight not located: [NEEDS VERIFICATION]); FFX says its
"controller and navigation-sound design draw on the user's Bravely Default mod".

Several of these (FF6, FFPR, FFX, FF1, Blind Soldier) say they were built with AI coding help (FF6: "Thanks to Claude
Code for assistance"; Blind Soldier: "development assistance from Codex"), so read their test claims as the authors
state them, not as independent verification.

## 1. Arcana: what it does for the field

Read at `main` `306007c` and tag `v0.3.0` `5060d08`. Paths are `ChipsAhoiMcCoy/arcana`. Engine: Unreal 4.27, so the
code reaches the game through Unreal's reflection, which Metaphor (GFD-family engine, per
`arcana-research-credits-and-pace-2026-11-16.md`) does not have.

### Features (README "Sounds", "Keys", "On a controller"; `CHANGELOG.md`)

- **Wall tones:** "four looping tones for how close walls are: ahead and behind (centered, different pitches), right
  and left. Louder is closer; silence is open space." They play only while the player pushes a direction, reach 3 m in
  the top-down compass mode, and measure "how far you could actually go that way, so a wall you would slide along
  stays quiet" (`src/walltones.cpp`: a capsule swept in steps, sliding along walls, with a floor line trace per step
  and a low line trace for knee-high blockers; stairs' risers are excluded after "Stairs no longer sound like a wall",
  `CHANGELOG.md` 0.2.0).
- **Sonar:** "every point of interest in range (10 m by default) repeats its own sound from where it is, faster and
  louder as you get closer: about once a second at 10 m, nearly twice at 5 m, nearly seven times at 1 m." Per kind a
  distinct sound (exits, doors, people, shops, objects, events, fragments); "at most four things sound at once, two of
  a kind"; the sound comes from the thing's part nearest the player; things on another floor or behind a closed door are
  left out until reachable; silent in menus, conversations, scenes and battles. Rhythm: `src/core/sonar_field.h`
  `FieldParams` (0.15 s period at 1 m up to 1.90 s at 20 m, a power law in distance, independent per-thing clocks).
  The history in that header comment records that the author changed it from a single queue to per-thing clocks, and
  made it faster because "being close to a thing was not heard" (test feedback, 2026-11-11 and 2026-11-12).
- **Points-of-interest list:** categories Exits, Doors, People, Shops, Objects, Events, Shadows; keys step through
  nearest first; read as "Iwatodai Station, 6 meters, 2 o'clock, 1 of 3"; people carry the icon state ("talked to",
  "Social Link"). Only the reachable part of the map is listed (your floor, floors up/down stairs, not past a closed
  door).
- **Focus ping ("route ping"):** when something is selected a ping plays from it, one sound if you can see it and
  walk straight there, another if the way goes round something, a third if there is no way from here (README
  "Sounds"). Credited to Grim Dark.
- **Auto-walk:** to any listed point; sets off silently; stops on stick push, key press, dialogue, menu; "stairs and
  doors end it on the far side"; at the end it faces the target so the prompt appears, and can press Space to use it
  (`src/nav.cpp` `use_tick`).
- **Right stick scan (top-down mode):** push toward a direction, snap to the nearest point that way; name and distance
  spoken, its sound plays alone while held.
- **Where am I / describe the area / objective / area name on arrival:** `Slash` reads area, date, money and objective;
  `Describe the area` reads hand-written text per area (`src/core/area_text.cpp`, `area_entries.inc`), following time
  of day, season and story state. The area name comes "from the game's hit-name table" (`src/poi.h` `area_name`).
- **Top-down mode:** the game's camera is replaced by a fixed north-up frame for sound and directions ("heard from
  where you stand with north ahead"); compass directions in the list.

### How points of interest are found (game data, not geometry)

`src/poi.cpp` header and `src/poi.h`: points come from "the game's own hit actors (AFldHitCore: mType_, mCheckIcon_)
and NPC actors, filtered to the visible streamed levels". Kind comes from the actor's enum (`mType_` Normal /
AreaChange) and class, name from the game's own name tables (`DT_FldDungeonHitName` etc., `mNameIndex_`). The data are
the actors' reflected properties and their box/sphere/capsule components (`src/world.h`: `trigger_box`,
`capsule_of`, `read_box`). No geometry scan is used to discover objects; geometry (traces) is used for wall tones, for
"can I walk straight there", and for the auto-walk. Some special cases are hand-listed (a placeholder NPC with no
model is skipped; a few spots named by hand, `poi.cpp` L190-196, L252).

### How the sound is positioned

Three modes, setting "Sound placement" (README line 142): "stereo, 3D for headphones, plain left and right".
- **Stereo (default):** the model ported from Dark Vision (Bradley Renshaw, zlib): constant-power pan with a 12 dB
  level cap, interaural time difference up to 0.66 ms (Woodworth head), far-ear high-shelf cut, and a high-shelf
  darkening for things behind (down to -10 dB due south). `src/core/spatializer.h`; engine-free.
- **3D:** "uses Steam Audio's HRTF" (Valve, Apache-2.0, `phonon.dll`), which "also gives above and below"; falls back
  to stereo if `phonon.dll` is absent (README line 300; `src/paths.h` lists `phonon.dll` beside the mod DLL).
- **Plain:** left and right pan only. Playback goes through miniaudio. A "lower pitch behind you" option exists.
- Placed sounds "stay where they are while they play, so they shift as you move" (README), listener frame is the camera
  or, in top-down mode, north.

### How auto-walk finds its way and avoids getting stuck

`src/world.cpp`: the engine's own navmesh through `UNavigationSystemV1::FindPathToLocationSynchronously` and
`K2_ProjectPointToNavigation` (L385-409), traces through `UKismetSystemLibrary` via `ProcessEvent`, movement through
`APawn::AddMovementInput` (`world::add_move_input`) or by holding the 8-way movement keys (`src/nav.cpp`
`steer_keys`). `src/nav.cpp` records the failure modes the author met, each with a date, and the answer:
- Target stands off the navmesh (a terminal, 2026-10-28): path to the nearest navmesh point, then straight in; search
  widened to 12 m for a trigger floating 7 m over its floor (2026-11-11).
- Target inside something solid (teleporter between pillars): test which sides the body can enter from (capsule
  sweeps), walk to the nearest, `src/core/approach.h`.
- The navmesh "knows the walls and nothing of who stands on the floor": a hallway student blocked the walk
  (2026-11-11). The hero's body is swept ahead and people are treated as moving obstacles.
- Blocked anyway: try a step to either side (three tries with no progress, then say "Stuck, stopping"); then circle
  the target and try other sides (six tries a sixth of a turn apart); `nav.cpp` L700-740.
- A room whose desks cut the navmesh into islands (classroom 2-F): the author wrote an A* over a grid of floor
  points using the engine's capsule sweep as the "can the body move here" test (`src/core/body_path.h`), used once per
  walk when the navmesh walk is blocked.
- A path that "runs on under" a closed door (L322) and ends against it; doors are tested as rectangles
  (`src/core/doorway.h`).
- Stuck detection compares progress toward the target over time (`progress_at`, `progress_pos`, `restart_progress`).
- Auto-walk is cancelled by player input or any dialogue/menu, and only runs while "exploring" (field state read from
  `AFldOperator`: Free/Camp/Battle/Event, `src/world.h`).

### What is configurable (README "Mod settings", about 30 rows)

Wall tones on/off and volume; sonar on/off, scope (everything, category only, selected only), range (5 to 40 m, 10
default), per-kind switches (exits, doors, people, shops, objects, events, fragments), volume; sound placement;
lower pitch behind; top-down camera on/off; strike assist; a sound test that plays and names each sound. Settings
live in an in-game accessible menu and `settings.ini`; "Changes are saved at once".

### What is removed or admitted as unsolved

- Removed in 0.2.0: "The beacon (the chirp toward the selected point of interest) ... The sonar already marks the
  selected point, and the auto-walk takes you there; the beacon was one more sound on top of that"; the "nearby
  chimes" ("The sonar's sounds are what tell you something is near"); the short "selection tick" among sonar sounds;
  the "Sonar rhythm" setting (a sweep rhythm was dropped; pulse only). `CHANGELOG.md` 0.2.0.
- Known issues (`KNOWN_ISSUES.md`, at `5060d08`/`main`): English only; audio description of 3D story scenes unfinished;
  "No full late-game playthrough"; "Screen readers other than the ones used day to day" untested; "a game update can
  break some or all of it" because code is found by byte shape; only the Steam Windows build tested. The list says
  nothing specific about navigation failures.
- What players reported: none found. At `main`, `open_issues_count` is 0, there are no issues in the API listing and
  Discussions are off (the same as alpha.3's note). The record of what broke is the author's own dated entries in the
  changelog and `nav.cpp` comments; most carry a date and a place ("the Gym Hallway, 2026-11-11"). Whether feedback
  came through another channel (Discord, mail) [NEEDS VERIFICATION].
- Stated gaps in the auto-walk from the changelog: it had said "Stuck" on things set into walls (the radiator and the
  mirror in the player's room, fixed in 0.3.0), steered by the camera while the hero walked by the compass in top-down
  mode (fixed in 0.3.0), and pushed into walkers (fixed in 0.2.0).

### Lineage and what Arcana itself credits

Arcana builds on Grim Dark (Austin Hicks, zlib; "Grim Dark's navigator and related parts were ported from Dark
Vision") and Dark Vision (Bradley Renshaw, zlib): stereo model, wall-tone files and the sonar sound files are theirs
(`THIRD_PARTY_NOTICES.md`). Wall tones, sonar and route ping are therefore a three-mod lineage, Pathfinder: Wrath of
the Righteous (2D/3D top-down), Grim Dawn (isometric), then Persona 3 Reload.

## 2. Final Fantasy mods

### FF6ScreenReader (FFVI Pixel Remaster), BlindGuyNW; MelonLoader + Tolk; no licence declared

The origin of most FF Pixel Remaster accessibility. README "Field Map Hotkeys":
- Cycle entities `J`/`L` (previous/next) by category (Chests, NPCs, Map Exits, Events, Vehicles), `K` repeat; `P`
  "Announce path to current entity. If you hear 'no path', the entity is not currently reachable"; then walk the
  announced path by arrow keys. A pathfinding filter hides entities with no valid path.
- Waypoints: add at your position with a typed name, cycle, pathfind to them, per map.
- Audio: wall tones, wall bumps, footsteps, audio beacons, "each with on/off toggle and volume control"; `M` announces
  the map name "(useful for orientation)"; a map-exit filter shows only the closest exit per destination.
- Teleport: `Ctrl+Arrow` "Teleport one tile in the direction of the arrow relative to the selected entity".
- How the data is got: the game's own tile map via MelonLoader/IL2CPP. `FFVI_Mod/Field/FieldNavigationHelper.cs` calls
  "the game's pathfinding system": `Il2Cpp.MapRouteSearcher.Search(mapHandle, startCell, destCell, playerCollisionState)`
  with the map's collision layer size, trying several layers, and "Don't fall back to collision=false - if we can't
  find a valid path, report failure (collision=false would route through walls, which is misleading)".
- Known issues (README): "Pathfinding occasionally suggests paths that aren't available due to gameplay restrictions,
  or says they aren't available when they are"; "Teleport is very occasionally necessary"; "certain areas are
  confusing without a guide and/or teleportation". GitHub issues and PRs (14 listed) include
  "Imperial Capitol (Vector) doesn't show up in navigator" (#14), "Implement audio beacon cadence configurability"
  (#10) and "Can't move when first entering overworld" (#8); #1 is a PR for entity caching with gradual refresh "to
  allow for dramatically fast..." refreshes (title truncated by the listing). [NEEDS VERIFICATION] for contents.

### FFPRCore-Screen-Reader (FF I-VI Pixel Remaster), bladestorm360; own hook layer; no licence declared

A single mod for all six games, which "credits ZKline / BlindGuyNW ... and permission to include its work". Loads as
`hid.dll` (proxy), MinHook, SDL3, Prism 0.18.3 (speech and braille), OpenAL Soft with HRTF data. README (v1.0):
- Destinations by category (with filters for exits and event groups), route directions, optional Autowalk (`P` or
  Backslash, "press again to stop"), world-map and vehicle (ship, airship) routes, waypoints, `Shift+G` puzzle-guide
  step, `M` current map; scope cycle "Current floor / Current map with vehicles / Current map / World map".
- Route beacon: "Follow route" (default; guides each leg) or "Object location" ("pings the selected entity ... even
  through walls"); "Route beacons now keep pinging while you walk, with full HRTF bearings and rear pitch cues only
  in stereo fallback".
- **Soundscape** (default off): "Each nearby entity has its own repeating cue, independent of the selected destination
  and beacon. Only entities already eligible for the pathfinder participate; entries on another floor, opened chests
  and ToLayer transitions are silent." Cues: NPC chirp, chest chime (contents not revealed), exit rising triple tone,
  exit-door knock, event double beep, trap lower double beep that "repeats faster within two steps". Distance is
  straight-line tile distance; range 5 to 25; HRTF is a separate default-off headphone setting with "map north ahead,
  south behind", fixed when the character turns; without HRTF "the southern half of the map uses a lower pitch".
  A crowd is handled by deferring starts, not selecting a few.
- Footsteps (one per tile), wall tones and wall bump, landing pings for water craft.
- Teleport shortcuts: "Ctrl+arrow: teleport north, south, west or east of the selected destination" (default off);
  "A blocked, unknown or stale landing leaves you in place."
- Data: map data files shipped per game (`data/ff1..6/maps.bin`, `map_titles.json`) plus the live native state;
  `docs/re/AUTOWALK.md` states "No game-thread wait, sleep, scene scan, script execution, transform write, direct
  MoveTo call or new diagnostic key is introduced": routes are planned on a worker thread, movement is injected as the
  game's own input, "native collision, speed, animation, encounters ... remain native", with waits of at most 0.75 s
  and a 15 s ceiling on a turn.
- Known problems (README "Known Issues", v1.0): "Intermittent stalls are still under investigation"; airship
  boarding is disabled ("board and take the wheel manually"); some listening checks and game screens "still need gameplay confirmation". The
  October 14 checkpoint records "Own FF2 directions waited 1.177-1.181 seconds despite ~33ms route stage" (a latency
  problem). Its docs record detours around NPCs that block a doorway (`docs/re/NPC_BLOCKERS.md`, referenced from
  `AUTOWALK.md`).
- Only 3 issues on GitHub; none read.

### FF1-Screen-Reader-Enhanced (FF Pixel Remaster), atreides825; MelonLoader (C#, Tolk); no licence declared

Same hotkeys as FF6ScreenReader (`J`/`L` or brackets cycle destinations, `P` pathfind, `;` wall tones, `F6` audio
beacons, waypoints, a pathfinding filter). Credits ZKline's FF6 mod. Known issues (README): "NPCs in shops are blocked
by counters, but interacting with the counter opens the shop"; "Secret passages, even when opened, do not show
properly on the pathfinder. Can use wall bumps and estimation to find".

### FFX-X2-Screen-Reader (FFX HD Remaster), bladestorm360; installer + native DLL (Prism); no licence declared

A 3D-field Final Fantasy with live collision. `Docs/controls.md`:
- Browse by category (All, Exit, Event, Save Sphere, Treasure Chest, Shop, NPC, Object) with distance and direction
  ("North means ahead relative to the camera"); backtick rescans ("Every navigation key refreshes nearby NPCs, shops,
  save spheres, treasure, exits and events from the current native state").
- Backslash: "route into the selected entity's actual interaction zone, including the reachable side of a counter.
  Routes use live native collision and retain short final movements. Once in range, Backslash says which way to face,
  such as Face west." Turn-by-turn route readout (`Speak auto walk path` setting).
- Auto walk (default off): "Movement input or Escape cancels immediately. Dialogue, scripted scenes, menus, battle,
  lost focus and a map change stop it"; "Blocked routes get bounded recovery attempts and stop if progress stalls";
  at arrival it faces the interactible, the player confirms.
- Beacon and wall tones (default off, F6): wall tones "indicate nearby floor boundaries relative to the camera".
- Hide unreachable destinations; stable exit numbering across floors; `Y` current map name.
- Its issue #7 "Explain Auto walk stops and keep the destination list stable" shows stop reasons were opaque to
  players at first (title only; PR content not read).

### Blind Soldier (FFVII, PC original), buu420; Reloaded-II + Prism; no licence declared

Notable because it runs on **Reloaded-II with Reloaded Shared Hooks and Prism**, as this repo does (README: "portable
ZIP bundles Reloaded-II, Reloaded Shared Hooks, Prism, and private .NET"). Pre-release. README "How routes work":
- Field categories Exits, Story, NPCs, Objects; world-map categories Locations, Story, Transportation, Events, Chocobo
  Tracks. "Story" is driven by "objectives tied to the game's progress state and field scripts".
- "In fields, the mod reads the game's native walkmesh: the same triangles, boundaries, ladders, jumps, and
  elevations that control where Cloud can walk. ... A route is built from the player's live X, Y, and Z position to
  the chosen target." Spoken legs merge straight segments ("up 20" not "up 4" repeated); recalculates when the
  player strays or a live obstacle blocks; pauses across battles.
- **Progress readout:** "Route progress uses a native Windows progress control" so each screen reader announces it
  (0 to 100 percent, every 5 percent by default); "Progress is based on your position along the whole connected
  route, including elevation. If you backtrack ... the value goes down; this is intentional". Reason given:
  "Directions can sound circular in stairs, multi-level rooms, winding passages".
- Auto walk (`P`): holds the directional keys for the current segment only; "never presses the action or confirm
  button". Footsteps (`FieldFootstepCadence.cs`) and "positional sounds" are listed; details not read
  [NEEDS VERIFICATION].
- "Known gaps": "No complete playthrough has been done"; auto-walk improvements "proved by offline route and movement
  fixtures, not by play"; "Walking while holding the Run button, and a milder back-and-forth dither at some corners,
  still need testing". Also "Press `I` once to stop and again to rebuild the route". It names "navigation failures
  worked around manually" in tester logs.

### Echos of Gaia (FFIX), buu420, MIT (Memoria); Memoria engine fork; Steam Audio

README: field categories Story, Exits, People, Objects, Items; spoken route legs "left 2", "up 5" at turns; optional
automatic walking "using normal movement and collision checks"; "Distinct NPC, interactable and exit proximity sounds
using Steam Audio HRTF. Headphones provide directional and elevation information." Auto-walk aims inside the selected
exit trigger and uses "approach points" for exits serving several floors (0.1.12 notes); route searches "run in the
background, and unchanged walkable geometry is reused". Self-described: "Field narration and
navigation still need broader gameplay" testing; "Some interactions still have generic labels".

### FFVIII-Accessibility-Mod (FFVIII Steam 2013), ampage87, MIT; FFNx hooks, SAPI speech

"Parses walkmesh data from field archive files, builds a deduplicated triangle mesh, and runs A* pathfinding with SSFA
(Simple Stupid Funnel Algorithm) for smooth paths. Analog steering is achieved by hooking the keyboard state reader
and injecting a fake gamepad device for true 360-degree movement." Entity catalog for NPCs, exits, objects; "INF
gateway exit compass directions may be slightly inaccurate on some fields". Known issues: "Auto-drive on field maps
can get stuck or take suboptimal paths on certain field layouts"; "Some interactive objects ... are labeled generically
('Interaction 1')". README advises manual navigation over auto-drive. Note: uses Windows SAPI, not a screen reader.

### ffrs-accessibility (Final Fantasy Resonance, Unreal), derbruedi; UE4SS Lua + Prism pipe; no licence declared

The only other Unreal-based one. `mod/FFRSAccess/Scripts/main.lua` (7,410 lines). README (German): a pathfinder "der
die Figur zu Personen, Schaetzen, Ausgaengen" walks to targets; categories quest goal, story events, people, enemies,
treasure, exits, doors, save points, objects, "Erkunden" (explore). Key details from `main.lua` and `installer/Liesmich.txt`:
- **Own walk map ("Wegekarte") built by collision queries:** it "scans everything walkable from the player's position by
  collision query, spread over many frames, and saves the result per map"; grid cell 75 cm, max rise/drop 85 cm,
  scan radius 45 cm "narrower than the figure, otherwise the grid misses narrow passages"; shipped maps as
  `karte_*.txt` files so players need not scan; offline tools in `research/extract/` generate names and walk maps from
  game data (game data not in repo).
- Movement by injecting the game's input action (`InjectInputVectorForAction` on `IA_Map_UnitMove`) every frame
  ("an injected input counts for one frame"); detours at angles 60, 100, 140 degrees both sides; progress step 100 cm;
  gives up after 15 s; arrival within 200 cm.
- "Questreise": walks quest goals section by section and resumes after scenes, battles and map changes; the player
  presses Enter. `Ctrl+E` jumps to the next story trigger, a few metres before it facing it.
- Beta; no issues or sound beacons read about. No licence file seen.

### blackmoon-prophecy-accessibility (RPG Maker MV fan game), bladestorm360

Footsteps, wall tones and a target beacon plus spoken step-by-step directions and categories (adds "Traps" and
"Crossings" categories excluded from "All"). A 2D tile game inside NW.js; little carries over except the category design.
Known issue first in its list: NVDA must be put in focus mode (a browser-window problem).

## 3. Comparison table

"Cost" = the author's stated or evident effort and runtime risk. "Data" = where the mod gets it.

| Technique | Used by | How it gets the data | Cost | Known problems |
|---|---|---|---|---|
| Categorised list of nearby things, cycled with keys, name + distance + direction | all of them (Arcana, FF6, FFPR, FF1, FFX, FFVII, FFIX, FFVIII, FFRS) | the game's live actor/entity lists and name tables; FFPR ships per-game name files and map data | medium; names are the long tail ("generic labels", "Interaction 1") | generic names; entries missing (FF6 #14, FFRS/FFX "Object" fallbacks); stale lists after collected items (FFX rescans each key) |
| "Where am I": map/area name, current objective | Arcana (`Slash`, area names from the game's hit-name table, hand-written area text), FF6/FFPR/FFX (`M`, `Y`), FFRS (`End` quest goal) | game strings; Arcana also hand-writes descriptions | low for the name, high for descriptions | text must be authored per area; spoilers |
| Repeating positional sonar per point of interest, rhythm by distance | Arcana (stereo or Steam Audio HRTF), FFPR "Soundscape", Echos of Gaia (Steam Audio HRTF) | positions of the same entities as the list | medium; needs audio engine (miniaudio/OpenAL/Steam Audio) and per-kind sounds | crowds (Arcana caps 4 at once and rewrote the scheduler twice); "being close was not heard" until period was shortened; north-up vs camera-relative frame confusion; behind vs ahead in plain stereo (fixed with pitch or shelf cut) |
| Distance-only rhythm (power law) | Arcana (`sonar_field.h`), FFPR ("repeats faster within two steps" for traps) | player-to-thing distance | low | the author changed it three times after tests |
| Wall tones (four directions) | Arcana, FF6, FFPR, FF1, FFX, Blackmoon (lineage: Dark Vision, Grim Dark) | Arcana: capsule sweeps and line traces in the engine; Pixel Remaster: tile collision data [NEEDS VERIFICATION] | low to medium | silent while standing still by design; stairs read as walls; "behind your walk" direction gave constant noise in compass mode; walls you would slide along must stay quiet |
| Wall bump / footstep sounds | FF6, FFPR, FF1, Blackmoon, FFVII | movement events and tile steps | low | none stated |
| Select a destination, then a spoken route ("up 20", "left 2", "north 3") | FF6, FFPR, FF1, FFX, FFVII, FFIX, FFVIII, FFRS | game tile pathfinder (FF6 `MapRouteSearcher`), walkmesh read from memory/files and own A* (FFVII, FFVIII, FFIX), own collision grid (FFRS) | medium to high | wrong "no path" or wrong path (FF6 README); paths through one-way or gated areas; "circular" directions on stairs (Blind Soldier added a progress percentage); 1.2 s latency (FFPR) |
| Auto-walk to the chosen target | Arcana, FFPR, FFX, FFVII, FFIX, FFVIII, FFRS | engine navmesh (Arcana) or the same route as above, then inject input (key/stick/pad/`AddMovementInput`) | high; the stuck cases dominate the work | stuck on NPCs, doors, counters, floating triggers, furniture islands, corners; "can get stuck" (FFVIII); "dither at some corners" (FFVII); stalls (FFPR); always cancelled by input and by dialogue/menus/battles |
| Auto-walk never presses the action key (or presses it once on prompt) | FFVII ("never"), FFX (player confirms), FFRS (player presses Enter), Arcana (optional Space) | design choice | none | none stated |
| Beacon / ping from the selected target | FF6, FF1, FFPR ("Follow route" or "Object location"), FFX, Arcana (focus "route ping": straight-line / needs detour / no way) | selected target + path check | low once the path check exists | Arcana removed its continuous beacon in 0.2.0 as redundant with the sonar and auto-walk |
| Route progress as percentage | FFVII | position along the computed route | low | none stated |
| Waypoints the player sets | FF6, FFPR, FF1, FFX, Blackmoon | the player's own position, saved per map | low | none stated |
| Teleport-style shortcuts (move one tile next to a target) | FF6 (`Ctrl+Arrow`), FFPR (default off), FFRS (`Ctrl+E` to story trigger) | writes position (FF6) or a validated landing | high risk | FF6: "if used recklessly can cause unexpected issues"; FFPR: a stale landing is refused |
| Compass/top-down mode | Arcana (north-up with a fixed frame) | game camera yaw replaced by a mod frame | medium | autowalk steered by the wrong frame (fixed 0.3.0) |
| Hand-written area descriptions | Arcana | author text per area | high effort, one-off | coverage; seasonal variants |
| Story/quest guidance as a category | FFVII, FFRS (quest journey), FFX, FF6 puzzle guide | game progress variables and scripts | high | coverage by chapter; spoilers |

## 4. Observations that cut across the mods

- Everyone starts with the list (name, distance, bearing) and a route to an item, then adds sound. Auto-walk is
  consistently the last and costliest piece and the one with the most admitted failures (FFVIII, FFVII, FFPR, Arcana's
  dated list of fixes).
- All auto-walks stop on any player input and on any dialogue, menu, battle or map change, and none presses the
  interaction key unprompted except Arcana's optional "use".
- Every mod that works on a mesh admits the mesh lies: Arcana's navmesh ignores people and closed doors; FF6's tile
  search says "no path" wrongly; FFRS built its own collision grid (its comment: a grid wider than the figure misses narrow passages).
- Sound placement has converged on stereo as default and Steam Audio/OpenAL HRTF as an opt-in for headphones
  (Arcana, FFPR, Echos of Gaia). FFPR and Arcana both add a pitch cue for behind when HRTF is off.
- A "no path" cue (Arcana's third route-ping sound, FFVII's "direction unavailable", FF6's "no path") is a recurring
  and valued state.

## 5. Fit for Metaphor: what suits it and what a hook-based approach must find

Suggestions, not decisions. Metaphor's field: 3D towns and dungeons on a GFD-family engine
(`arcana-research-credits-and-pace-2026-11-16.md`), so no Unreal reflection; our mod is a Reloaded-II hook mod
with Prism speech (ADR 0002; Blind Soldier is the one found precedent on the same loader, FFVII, no licence).

### Techniques worth considering, roughly by value for cost

1. **Categorised nearby list with name, distance and clock/compass direction, plus "where am I"** (Arcana, FFX, FF6).
   Cheapest, and the base for everything else. Needs from the game: the player's position and camera yaw; a list of
   field actors with position, kind and a display name (NPCs, exits/area changes, interactables, shops); whether
   an actor is in the current map (not streamed-out); the current area's name. [NEEDS VERIFICATION] whether any of
   these live in readable tables rather than only in scripts.
2. **Per-thing sonar with distance rhythm and stereo placement** (Arcana's `sonar_field` + Dark Vision's spatializer;
   FFPR's soundscape). Needs the same positions as (1), the listener frame (camera yaw), and our own audio path
   (Prism is speech only: [NEEDS VERIFICATION] whether Prism can play non-speech sound; otherwise an audio library
   with its own licence and a pinned version is required, ADR 0005). Stereo placement is pure math and could be
   ported (Zlib: keep the notice, credit Renshaw, Hicks and ChipsAhoiMcCoy).
3. **Auto-walk to a selected target** (Arcana, FFX, Blind Soldier). Needs a path source and a way to move the player.
   Movement injection is cheaper than writing position: hold the game's own input (key chords, a pad axis) as Arcana
   (`steer_keys`), FFPR and FFVIII do; this keeps collision and animation native. The path source is the open
   question below. Cancel on any player input and on dialogue, menu, battle or scene (all of them do).
4. **Wall tones** (Dark Vision lineage). Needs a collision query along four directions (sweep or ray) usable from a
   hook thread; high value in tight dungeon corridors, lower in open towns. Arcana's lessons: play only while the
   player pushes, ignore slide-along walls, treat stairs specially.
5. **Route progress and a "no way / way round / straight" cue** (Blind Soldier's percentage; Arcana's focus ping).
   Cheap once a path exists; helpful when spoken legs are circular.
6. **Hand-written area descriptions** and a **waypoint set per map** are optional extras after the base is stable.
7. **Avoid first:** teleport/position writes (FF6 warns, FFPR refuses stale), a mod-owned top-down camera (needs full
   camera ownership), and exhaustive story guidance (coverage and spoiler load).

### What the hook-based approach must find in the game (all [NEEDS VERIFICATION]; none is known for Metaphor)

- **Player position and facing** every frame, and the camera yaw (to turn world offsets into "ahead/right"). The
  Persona 5-era modding community's structure notes (e.g. rirurin's OpenGFD) may help; unverified.
- **Entity enumeration:** a table or a script-side list of NPCs, doors, exits (map transitions), shops, chests, event
  triggers, with position and kind, plus the field's own names for them. Arcana's data came from the engine's actor
  classes and name tables; Pixel Remaster mods needed per-game name files; Blind Soldier used field scripts for
  objectives. For Metaphor the likely sources are the field/script data and the instance list the engine keeps while
  a field is loaded.
- **Collision and path data:** either (a) a navmesh the game already uses for NPCs and the follower (Arcana's route
  source), (b) a walkmesh or collision mesh readable from the game's data (FFVII/FFVIII), or (c) a physics query
  function to call (Arcana's sweeps, FFRS's collision-query grid). Option (c) needs a callable engine function and its
  signature pinned to a build (ground rule 2); option (b) needs a data format; option (a) may not exist.
- **Player input path:** where the game reads the movement stick/keys, so auto-walk can inject (FFRS used the game's
  input action; Arcana hooks the window procedure and `XInputGetState`).
- **Game state gates:** "field, free to move" versus dialogue, menu, battle, event, loading (Arcana reads the
  `AFldOperator` state: Free 1, Camp 6, Battle 11, Event 12).
- **Map identity:** the current map's id or area name, for "where am I" and for resetting caches (FFRS saves its grid
  per map; FFPR keys by map and floor).
- **Floors and doors:** which things are on other floors or past closed doors (Arcana leaves them out until reachable).

### Risks seen elsewhere that would apply

- Doors, people and furniture are not in a navmesh; plan for blocked-path recovery (side step, detour, body sweep).
- Sound crowding in towns: cap simultaneous sounds and per-kind (Arcana: 4 at once, 2 per kind).
- Game updates break byte signatures; Arcana's whole mod stops working on another build and says so (ground rule 2
  already covers this).
- Threading: FFPR plans routes on a worker thread and never touches the game from it; Arcana touches game objects
  only on the game thread and never holds raw object pointers across frames. A hook mod needs a similar rule.
- Credit: Arcana, Grim Dark, Dark Vision are Zlib (copying allowed with the notice and "altered" marking; Arcana's
  altered files carry the comment "This is not the original software"). FF6ScreenReader, FFPRCore, FFX, FF1, Blind
  Soldier, ffrs, Blackmoon declare no licence: ideas may be cited, code must not be copied. FFVIII-Accessibility-Mod and
  Echos of Gaia are MIT.

## 6. Not done or unconfirmed

- No code outside the files named was read; behaviour in the games was not observed.
- Arcana's `src/sonar.cpp`, `src/doors.cpp`, `src/area.cpp` and `src/field_area.cpp` were not read beyond a few lines.
  "Describe the area" text comes from `src/core/area_text.cpp` / `area_entries.inc`; the way it selects an entry was
  not read.
- FFPR, Blind Soldier and Echos of Gaia source was only sampled by file and doc names; their route and sound
  algorithms are described from their own READMEs and docs, not code.
- Whether any of these mods has had a blind player complete a game: Blind Soldier says no; FFPR and FF6 say "it is
  possible to beat the game" (FF6 README, by the author) [NEEDS VERIFICATION] independently.
- The licence status of mods listed as "none declared" is the absence of a licence field and root file at read time.
