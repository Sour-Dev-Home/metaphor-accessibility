# Keys and what the mod speaks

> **Draft for testers.** Written against the pre-release `v0.0.1-unstable.3` and the code after it. Lines marked
> `[NEEDS VERIFICATION]` have not been heard in the game with a screen reader yet.

Every key and button the mod adds, and what it speaks where. The mod reads no mouse input. A press counts when the key
goes down, not while it is held, and a key already held when a window opens does nothing. Every key and button can be
moved, and every part of the mod turned off, in the settings file: see [the settings reference](settings.md). If two
actions in the same place are bound to the same key or pad chord in `settings.json`, the mod says and logs the clash
once, keeps the first action bound, and leaves the second unbound until you change it. Where the mod uses a key it hides
that press from the game, only in the situation named; elsewhere the key does its usual job in the game.

Testing, with `padSpeechStop` on in `speech`: pressing a controller button stops whatever announcement is being read
before what that press brings up is said. Triggers (`LT` and `RT`) and held chord buttons (`LB` and `RB`) do not stop
speech. `[NEEDS VERIFICATION]` By ear.

## Keys and buttons

| What it does | Keyboard | Controller | When it works |
|---|---|---|---|
| Repeat the last dialogue line in full | `R` | `X` | A dialogue window is open |
| Stop the speech (testing) | `P` | none | A dialogue window is open |
| Say who is speaking, even with the reader off (testing) | `O` | none | A dialogue window is open |
| Say the line on screen, without its speaker, even with the reader off (testing) | `U` | none | A dialogue window is open |
| Turn the mod's speech on or off | `T` | `LB` and `RB` held together | A dialogue window or a menu is open |
| Say what is near, nearest first | `N` | none | Walking in the field |
| Step to the next thing on that list | `K` | `LB` held, then D-pad `Down` | Walking in the field |
| Step to the previous thing | `J` | `LB` held, then D-pad `Up` | Walking in the field |
| Say where you are | `L` | `LB` held, then D-pad `Left` | Walking in the field |
| Guide you to a nearby thing with a beep | `G` | `LB` held, then D-pad `Right` | Walking in the field |
| Next or previous nearby category | `]` and `[` | none yet | Walking in the field |
| Move the map cursor (testing) | `V` held, then the arrow keys | `LB` held, then the right stick | Walking in the field |
| With the map cursor out: bookmark its spot, jump to a bookmark, guide there (testing) | `B`; `K` and `J`; `G` | `R3`; D-pad `Down` and `Up`; D-pad `Right`, with `LB` held | Walking in the field |
| Read more of the focused menu entry | `F` | Right stick press (`R3`) | A menu is open |
| Step to the next enemy and say its HP | `K` | `LB` held, then D-pad `Down` | In a battle |
| Step to the previous enemy | `J` | `LB` held, then D-pad `Left` | In a battle |
| Say every enemy's HP in one line | `L` | `LB` held, then D-pad `Right` | In a battle |
| Say the party's HP and MP (testing) | `M` | none yet | In a battle |
| Say what the enemy looks like (testing) | `I` | none yet | In a battle |
| Say the command wheel's commands and their keys (testing) | `C` | `LB` held, then D-pad `Up` | In a battle |
| Note a moment for your report ([mark key](#mark-key-note-a-moment-for-your-report)) | `F8` | none | Anywhere in the game |
| Open or close the mod's settings ([settings menu](#settings-menu-change-a-setting-by-ear), testing) | `F2` | `LT` and `RT` held, then `View` | Anywhere in the game |
| Say the mod's keys that work here (testing) | `F1` | `LT` and `RT` held, then `Y` | Anywhere in the game |

The first rows are in the code on `main` and in `v0.0.1-unstable.3`. Coming: a key that reads back a name you typed, and
spoken details for menu entries. Until details arrive, the menu key says "No more details".
`[NEEDS VERIFICATION]` All of the table by ear in the game, with a supported game build.

### Mark key: note a moment for your report

`F8` on the keyboard, anywhere in the game: when something was wrong or the mod said nothing, press it. The mod plays
two short clicks and writes a numbered marker with the time to `report-log.txt`, such as `mark number=2
session=0:12:03`, so whoever reads your report finds that moment. The game never sees `F8` while the mark key is on.
There is no controller button yet. Whether the game uses `F8` for anything is `[NEEDS VERIFICATION]`; you can move the
mark key in the settings file.

The mod also lists in the same log each screen and each menu move it said nothing for, by the screen's name inside the
game's code, the position and the list's length, at most 200 a session. Both only write to the log; nothing is sent
anywhere.

### Settings menu: change a setting by ear

While it is being tested, turn it on once in the settings file with `{ "speech": { "settingsMenu": true } }`
([settings](settings.md)). Then press `F2`, or hold `LT` and `RT` and press `View`, on any screen. The mod says "Mod
settings", the section and the first setting. `Up` and `Down` move, and each setting says its name and value. `Left`
and `Right` change it, with `Shift` for bigger steps on a number, and the mod saves it to `settings.json` at once and
says "saved". `F` or `R3` says what the setting does. `Page Down` and `Page Up`, or `RB` and `LB`, jump to the next or
previous section. Keys are listed too, but are changed in the file for now. With `soundLibrary` on (testing), a Sound library section
at the end plays each mod sound cue with its name and what it means. `Escape` or `B` closes the menu. While it is
open, the game gets none of your keys or buttons. `[NEEDS VERIFICATION]` By ear in the game.

## What is spoken where

### Dialogue

- Each line is read when its window opens. Reader off silences dialogue lines only, not the keys, the load message or
  the settings message.
- `R` or `X` with no line yet says "No dialogue to repeat". `T` or `LB` and `RB` says "Dialogue reader on" or
  "Dialogue reader off".
- Testing, with `dialogueKeys` on: `P` stops whatever the mod is saying. `O` says the speaker's name, or "No speaker",
  and `U` the line without its speaker; with no line on screen, both say "No line on screen". Whether the game uses
  `P`, `O` or `U` in dialogue is `[NEEDS VERIFICATION]`; you can move them in the settings file.
- Testing, with `messageLog` on: opening the game's message log says "Message log, line 29 of 29", and each move the
  new line's position, such as "Line 28 of 29". The lines' words aren't read yet.
- Reply choices: when they appear, "Choice, N options" and not the options themselves, or with the `choiceCount` setting off, "Choose:" and the options. Moving the cursor speaks the option and its position, such
  as "Option, 2 of 3".
- Lines the voice actors speak follow your voiced-lines setting: the speaker only, the full line, or nothing. The default
  says the name for English voice and the full line for Japanese voice
  ([change how voiced lines are read](../how-to/change-how-voiced-lines-are-read.md)).

### In the field

- `N` says up to 8 people, doors, chests and enemies, each with its name or kind, then how far and which way, such as
  "Chest, 4 steps ahead-left". Ahead means the way the camera looks, so left and right are the screen's. "In reach"
  means you can probably use it from where you stand and face. With nothing near: "Nothing nearby".
- `K` and `J`, or the pad's `LB` and D-pad `Down` and `Up`, step through the same list, such as "2 of 5. Door, 9 steps
  behind"; after 10 seconds without a step it starts again at the nearest.
- `]` and `[` move the list to the next or previous category, and say it and how many are near, such as "Doors and
  exits, 2 nearby". The categories are Everything, People, Doors and exits, Chests and items, Enemies and Examine.
  `N`, the steps and `G` then keep to that category. The controller's `RB` with `Y` comes later.
- `L` says the place you are in, such as "Principality of Oceana, Forsaken Tower", or "Place not known yet", then the
  current main mission: "Mission:", its title and its current objective. The mod also says a new mission by itself
  once you're back in the field.
- `G`, or `LB` and D-pad `Right`, guides you to the thing you last stepped to, or to the nearest thing you aren't
  already at if you haven't stepped in the last 10 seconds. At it already: "Already next to", then the name. Otherwise
  "Guiding to", then the name, and a short beep repeats: from the left or right as the thing lies, higher ahead and
  lower behind, faster as you get closer. Walk so the beep is high and in the middle. "Reached", then the name, when
  you get there. Press again to stop: "Guidance off". A menu or a dialogue stops it too. The beep points at the next
  turn of a walking route around walls, and plans again if you stray or get stuck; with no route it points straight
  and says "No path found, pointing straight" once (#690). It plays only while sound cues are on.
- With no key: walking up to a door or chest says "Door, in reach" or "Chest, in reach" once, after anything being
  said. Move away or turn, then come back, and it says it again. People aren't said this way yet.
- Distances are rough steps: treat them as near or far.
- Testing, with `mapCursor` on: holding `V` says "Map cursor" and puts a cursor on you. Each arrow key moves it 5
  steps, up being ahead on screen, and says what is there, such as "Door, open, 5 steps ahead" or "Wall, 10 steps
  left": a bookmark, a map icon or the objective's marker, the nearest thing, and open floor or wall once the area's
  walking map has loaded. Let go and it is back on you. Bookmarks are kept per area in `map-bookmarks.json`, beside
  the settings file. `[NEEDS VERIFICATION]` By ear, and whether the game uses `V`, `B` or the arrows in the field.

### In a battle

- The enemy HP keys `K`, `J` and `L` are on (`battleEnemyKeys`), and so are the pad's `LB` chords
  (`battleEnemyPad`, [settings](settings.md)). `LB` tapped alone opens Tactics when you let go. They know only the
  enemies hit so far in this battle, numbered by the order they were first hit.
- `K` and `J`, or `LB` with D-pad `Down` for the next, step to the next or previous enemy and say it, such as "Second
  enemy, 50 of 300", or "50 left" when its full HP isn't known. With `battleEnemyMaxHp` off they say only the number,
  "Second enemy, 50". With no enemy hit yet: "No enemy hit yet".
- Testing, with `battleCommandWheel` on: `C` says the command wheel's commands with the keyboard key for each,
  "Archetype E, Melee F, Guard H, ...", and `LB` with D-pad `Up` says them with the controller's buttons.
- `L`, or `LB` with D-pad `Left`, says every enemy's HP in one line.
- Testing, with `battleEnemyList` on: the keys know every enemy in the battle from its start, hit or not, and a step
  ends with its place, such as "First enemy, 840 of 1320, 1 of 3". An enemy not hit yet says the HP the game holds for
  it, or "not hit yet".
- Testing, with `battleEnemyTarget` on as well: after the target cursor moves, the next step says the enemy the cursor
  is on, and `L` starts from it. With no target yet in this battle, they start from the first enemy.
- Testing, with `battleHpMp` on: `M` says each party member's HP and MP.
- Testing, with `battleEnemyLooks` on: `I` says what the enemy the keys are on looks like, from descriptions written
  for the mod. None is written yet, so for now it says the enemy's name and "no description yet".
- In a battle the game keeps `F` (Wield Arms) and the right stick press (auto-battle).
- `[NEEDS VERIFICATION]` By ear: the command wheel's line, and the controller buttons for Item and Pass, which it says
  by name alone.

### Menus

The mod adds no cursor keys: use the game's own. It speaks what the game's cursor does.

- Title menu: the item with its position, and "Main menu" in front the first time, such as "Main menu, New Game, 1 of
  4".
- The menu during play: "Menu, Equipment, 3 of 9", then each move, such as "Party, 4 of 9". The entries, in order:
  Skill, Item, Equipment, Party, Follower, Quest, Calendar, Memorandum, System. The names are the mod's own wording, as
  the game draws them as pictures.
- Any other menu screen: the position and the list's length, such as "4 of 8", or the position alone when the length
  can't be read.
- Coming (on `main`, not in a release yet): settings rows by name, the settings confirm questions, the key configuration
  grid's column and row, and tutorial pop-ups (their title, how many pages, and the first page; each page you turn to is
  read as "Page 2 of 3"). `[NEEDS VERIFICATION]` By ear.

### Braille

There is no braille setting. Each announcement goes to your screen reader's speech and braille together, where it
supports that, and to speech alone where it doesn't.

## What works and what doesn't, by chapter

The mod is not tied to a chapter yet. For `v0.0.1-unstable.3`, in any part of the game:

| Works | Not yet |
|---|---|
| Dialogue, reply choices and the dialogue keys | Battle |
| Title menu, the menu during play, other menu lists by position | Most menus' contents, such as items and skills |
| What is near, and where you are, in the field | Finding your way: walking guidance, the map |
| Update notice, voiced-lines setting, sound cues setting | The key that reads a name you typed back |

This table is kept in step with each release's notes in [`CHANGELOG.md`](../../../CHANGELOG.md), which also lists what
is on `main` and not yet released. A per-chapter list starts when the first chapter is played through blind.

## Related

- [Install the mod](../how-to/install-the-mod.md)
- [Troubleshoot the mod](../how-to/troubleshoot.md)
- [Sound cues](sound-cues.md)
