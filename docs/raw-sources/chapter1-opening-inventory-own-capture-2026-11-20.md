# Chapter 1 opening interaction inventory from the maintainer's own capture of game session 1

- **Source:** the maintainer's own OBS capture of game session 1 (#496), recorded 2026-11-20, 45 min, 1920x1080 at 60
  fps, kept in the maintainer's captures folder outside the repository. It covers the title screen, the Settings pages,
  a new game (the opening ink-style scene, name entry, difficulty), the first desert dialogue and free movement, the
  first tutorials, arrival in the capital and the main menu, then a return to the title. The opening was played twice;
  the second pass starts at about 28:34. Timestamps below are video time from the burned-in stamp.
- **Saved:** 2026-11-20
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018). Mod build
  `e77eeab`.
- **Status:** first inventory from our own play, for the stretch [the pilot inventory](chapter1-interaction-inventory-2026-11-20.md)
  covers in its first minutes. It lists what the screen shows and what the player does; no engine claim is made.
- **Method:** [the chapter scrub](../contributors/how-to/scrub-a-chapter.md) (`scripts/dev/scrub.ps1` at repository
  commit `c35199b`) with FFmpeg 9.0.2 (gyan.dev full build), PowerShell 7.7.0 and Ollama 0.40.16 with `qwen2.5vl:7b`
  for labels; no captions, so the whole video was one segment. Two runs: the defaults (31 frames on 4 sheets), then
  `-MaxGap 10 -HashDistance 2 -SceneThreshold 0.2` (98 frames on 11 sheets), because the defaults left gaps of up to
  4.5 minutes. The rows come from both runs' sheets. The capture's spoken narration was not transcribed or quoted.
  Scenes are described in the author's words; no frame, audio or game text is kept, and the work folder was deleted
  after the table was written. Readings the frames could not settle are marked [NEEDS VERIFICATION].
- **Play:** every row is `confirmed-by-play`: the maintainer played it in this capture. At the time of writing #496 held
  no session results yet (no ticked boxes, one comment), so rows link to its stop numbers rather than to its notes.

## Interaction table

Frequency: once, few (2 to 5), many. Confidence is for the reading of the screen, not for what a blind player needs.

| # | Time | Activity | Screen shows | Player does | Player relies on | A blind player needs | Freq | Conf | Play |
|---|------|----------|--------------|-------------|------------------|----------------------|------|------|------|
| 1 | 0:20 | Intro animation (attract) | A silhouette animation, then the logo builds | Waits or presses a button | Image only | A cue that a button skips to the title | few | high | confirmed-by-play (#496 stop 2) |
| 2 | 0:30 | Title screen, press any button | Logo over a city scene; a press-any-button line | Presses a button | Prompt | "Press any button" spoken | few | high | confirmed-by-play (#496 stop 2) |
| 3 | 1:30 | Title menu | A horizontal title menu under the logo, the focused item on a red brush stroke; two items in view (Settings, Credits); a licences hint | Moves left and right, confirms | Highlight | Item name, position and count, how to move [NEEDS VERIFICATION: the third item was off the sampled frames] | few | high | confirmed-by-play (#496 stop 2) |
| 4 | 20:30 | Settings: category list | Five categories (System, Sound, Graphics, Display, Key Configuration) in a vertical list | Moves, opens one, backs out | Highlight | Category name and position | few | high | confirmed-by-play (#496 stop 3) |
| 5 | 2:30 | Settings: option rows | Rows with a name, a value strip (on/off pair, a three-way choice, a slider), a one-line help text under the focused row, an OK button and Default/Back hints | Moves rows, changes values left and right, confirms with OK | Highlight, value strip, help line | Row name, current value, the help line, how to change and apply | many | high | confirmed-by-play (#496 stop 3) |
| 6 | 40:02 | Settings: sound page and voice language | Volume sliders and a Voice Selection row with Japanese and English | Changes the voice language | Highlighted value | Row, value, whether a restart is needed [NEEDS VERIFICATION: no restart prompt seen] | once | high | confirmed-by-play (#496 stops 3 and 8) |
| 7 | 39:42 | Text language list | A plain list of twelve text languages on a black screen with Confirm and Back | Moves, confirms or backs out | Highlight | Language name and position [NEEDS VERIFICATION: which page opens it] | once | med | confirmed-by-play (#496 stop 3) |
| 8 | 9:30 | Key configuration | Tabs for Keyboard, Mouse and Controller, then sub-tabs (Common, Event, Field, Dungeon, Battle); two columns of action and key; a prompt to press the new key | Switches tabs, moves in a two-column grid, rebinds | Tab highlight, key glyphs | Tab names, action and its key, column, the rebind prompt | many | high | confirmed-by-play (#496 stop 3) |
| 9 | 21:04 | New game: parchment transition | A blank parchment screen | Waits | Image only | A cue that the new game is starting | few | high | confirmed-by-play (#496 stop 4) |
| 10 | 21:13 | Opening dialogue in an ink illustration | A black-and-white ink scene; a box with the role name "Scheming Man" and his line; Fast-Forward, Log and Auto hints | Advances | Name tag, line | Speaker (a role name, not a proper name) and line | few | high | confirmed-by-play (#496 stop 5) |
| 11 | 21:53 | Name entry | A torn-paper banner asking who will guide the protagonist, a text field, a decorative script backdrop; a Confirm hint | Types a name, confirms | Prompt, typed letters | The prompt, each typed letter, the field's contents, how to confirm [NEEDS VERIFICATION: whether this name is the player's or the protagonist's] | once | high | confirmed-by-play (#496 stop 4) |
| 12 | 22:23 | Opening dialogue reacting to the name | The same ink scene; the role-named speaker comments on the entered name | Advances | Name tag, line | Speaker and line | once | high | confirmed-by-play (#496 stop 5) |
| 13 | 24:05 | Difficulty select | A five-item vertical list (Storyteller to Regicide) titled Select Difficulty, the focused item on a red stroke, a two-line description below | Moves, confirms | Highlight, description | Item, position, its description [NEEDS VERIFICATION: a confirm box after it was not in the sampled frames] | once | high | confirmed-by-play (#496 stop 5) |
| 14 | 24:28 | Animated cutscene | Anime-style scenes (a carriage interior, a close-up of the hero) | Waits | Image, voice | Voice plays or subtitles spoken; a skip cue | few | high | confirmed-by-play (#496 stop 6) |
| 15 | 24:40 | Field dialogue with portrait | A 3D scene with a large portrait at the left; a name tag (the fairy companion) and a line; Skip, Log, Memorandum and Auto hints at the right | Advances | Name tag, line, hints | Speaker and line; what the hints do | many | high | confirmed-by-play (#496 stop 6) |
| 16 | 25:50 | Dialogue log | A scrolling list of earlier lines, each with its speaker name; a vertical title; a Back hint | Scrolls, backs out | List, speaker labels | Each entry's speaker and line, position in the list | few | high | confirmed-by-play (#496 stop 6) |
| 17 | 31:14 | Dialogue choice | Two short answer lines stacked at the right, the focused one on a red stroke, over the companion's line; Log, Memorandum and Confirm hints | Picks one of two answers | Highlight | The question, both options in order, the focused one | few | high | confirmed-by-play (#496 stop 6) |
| 18 | 32:24 | Hero speaks under a role name | The hero's portrait with the name tag "Travelling Boy" | Advances | Name tag | Speaker named as shown; a later rename noticed | few | high | confirmed-by-play (#496 stop 5) |
| 19 | 37:33 | Inner monologue box | A box with no name tag, centred low, over the hero | Advances | Line | Line, marked as the hero's thought | few | med | confirmed-by-play (#496 stop 6) |
| 20 | 33:03 | Tutorial pop-up (paged) | An ink-splash panel titled Tutorials, a category (Dungeon) and a topic (Basic Controls), body text with key glyphs and red keywords, a page counter (1 of 3), an inset image on later pages; Skip and Next hints | Pages with Next, or skips | Text, glyphs, page counter | Category, topic, body with key names spoken, page n of m, how to skip | many | high | confirmed-by-play (#496 stop 7) |
| 21 | 35:53 | Field movement (first, desert) | Third-person hero in a sandstorm; minimap disc; area name card (desert, sub-area); a Mission box at the top right; HP and MP with the hero's portrait; a new Memorandum notice; Items, Memorandum, Menu and Map hints | Moves, dashes, uses the camera | Minimap, mission text, notices | Area and sub-area names, mission, heading, the new-entry cue | many | high | confirmed-by-play (#496 stop 6) |
| 22 | 36:53 | Companion's ambient line | A small box at the top left with the companion's face and a line, while the player keeps moving | Keeps moving | Top-left line | Speak ambient lines, distinct from blocking dialogue | many | high | confirmed-by-play (#496 stop 6) |
| 23 | 36:03 | Main menu in the field | A vertical menu (Item, Load, Save greyed, Tutorials, Story, Memorandum, Settings, Return to Title) on a teal splash, a one-line description under the focused item, money and a second currency at the bottom right | Moves, opens an item | Highlight, description | Item, position, disabled state, description, both currencies | many | high | confirmed-by-play (#496 stop 7) |
| 24 | 36:33 | Fae Sight | A green-tinted view; an enemy marked with an "Unknown" card and a name | Holds or toggles the sight, looks around | Tint, enemy card | That the sight is on; nearby enemies with names and direction | many | med | confirmed-by-play (#496 stop 7) |
| 25 | 38:03 | Field hit (enemy damages the hero) | Small desert creatures; a damage number over the hero; HP in the HUD drops | Dodges or keeps running | Damage number, HP strip | Hit taken, damage, HP left | few | med | confirmed-by-play (#496 stop 6) |
| 26 | 38:31 | Flashback still | A grey torn-edged picture over the scene | Waits | Image | A cue that this is a memory | few | med | confirmed-by-play (#496 stop 6) |
| 27 | 38:41 | Field movement (capital street) | City street with banners and crowds; the fairy beside the hero | Moves, approaches people | Crowd, banners | Location name, nearby talkable people | many | high | confirmed-by-play (#496 stop 6) |
| 28 | 38:51 | Town tutorial | The same tutorial panel, category Cities, about the local map and asking the companion for ideas | Pages, closes | Text, glyphs | As row 20 | few | high | confirmed-by-play (#496 stop 7) |
| 29 | 39:01 | Save screen | Numbered slots, each with place, in-game date, weekday, time of day, difficulty, playtime, a character name and level | Moves through slots, confirms | Highlight, slot details | Slot number, place, date and time of day, playtime, level; overwrite confirm | few | high | confirmed-by-play (#496 stop 8) |

## Comparison with the pilot inventory

The pilot read one frame a minute from a public video; for this stretch it is its rows 1 to 6.

- **Right:** speaker name tags on field dialogue with Skip, Log, Memorandum and Auto hints (pilot row 6, here 15);
  role names instead of proper names (pilot note to rows 2 and 4; here "Scheming Man" and the hero as "Travelling
  Boy"); the first desert movement HUD with minimap, mission box, HP and MP and a new-entry notice (pilot row 5, here
  21); a two-line styled choice early on (pilot row 14, here 17, earlier than the pilot's first at its 0:25).
- **Wrong or missing:**
  - The pilot's row 3 was a two-button difficulty confirm "before the opening finishes". Here difficulty is a
    five-item list with a description (row 13), after name entry and the role-named speaker's reaction to the name, and
    before the first animated cutscene. A confirm box may follow it; the frames don't settle it.
  - The pilot has no name entry row. Its row 2 ("a question, then a confirm box", in an ink-style scene) fits this
    capture's name entry inside the opening ink scene (rows 10 to 12): the opening dialogue comes before and after
    the name prompt, not after it.
  - The pilot says the first tutorial comes at its 1:50 (the Archetype Tree). Here paged tutorial pop-ups come with the
    first free movement (Basic Controls, 3 pages; Fae Sight, 2 pages) and on reaching the capital (row 28), so #496
    stop 7 falls inside session 1's route.
  - Missing from the pilot because the video starts at the new game: the title, the Settings pages and key
    configuration, the dialogue log, the main menu, Fae Sight, the inner-monologue box, the save screen.

## Helper numbers and rough edges

| Run | Picked | Near-duplicates dropped | Loading dropped | Kept | Sheets | Labels (kept) | Wall time |
|-----|--------|-------------------------|-----------------|------|--------|---------------|-----------|
| Defaults | 56 | 23 | 2 | 31 | 4 | menu 12, cutscene 9, dialogue 4, field 4, choice 1, tutorial 1 | 1 min 47 s |
| `-MaxGap 10 -HashDistance 2 -SceneThreshold 0.2` | 282 | 182 | 2 | 98 | 11 | menu 45, cutscene 33, field 7, tutorial 6, dialogue 5, choice 1, battle-action 1 | 3 min 39 s |

- Ollama took about 1.7 s per new frame (five fresh frames timed by hand), so labels were most of each run's time; the
  helper does not report label time.
- With the defaults the kept frames were up to 4.5 minutes apart (27:30 to 31:57), although `-MaxGap` is 60 s: the
  near-duplicate pass compares each frame with every frame kept so far, so a max-gap frame that looks like any earlier
  one is dropped. That missed the difficulty list, the opening's ink-scene dialogue, Fae Sight, the town tutorial and
  the save screen.
- Labels mixed up dialogue and cutscene: of the 53 frames on sheets 6 to 11 of the dense run, about 9 were mislabelled
  by the author's reading, mostly dialogue boxes called cutscene; a choice and the log were called dialogue.
- The capture-side note: once the game switched to windowed mode the picture filled only part of the OBS canvas, so
  later frames carry a black margin. A capture setting, not the helper.
