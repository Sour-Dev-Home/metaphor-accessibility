# Keys and what the mod speaks

> **Draft for testers.** Written against the pre-release `v0.0.1-unstable.3` and the code after it. Lines marked
> `[NEEDS VERIFICATION]` have not been heard in the game with a screen reader yet.

Every key and button the mod adds, and what it speaks where. The mod reads no mouse input. A press counts when the key
goes down, not while it is held, and a key already held when a window opens does nothing. Every key and button can be
moved, and every part of the mod turned off, in the settings file: see [the settings reference](settings.md).
Where the mod uses a key it hides that press from the game, only in the situation named; elsewhere the key does its
usual job in the game.

## Keys and buttons

| What it does | Keyboard | Controller | When it works |
|---|---|---|---|
| Repeat the last dialogue line in full | `R` | `X` | A dialogue window is open |
| Turn the mod's speech on or off | `T` | `LB` and `RB` held together | A dialogue window or a menu is open |
| Say what is near, nearest first | `N` | none | Walking in the field |
| Step to the next thing on that list | none | `LB` held, then D-pad `Down` | Walking in the field |
| Step to the previous thing | none | `LB` held, then D-pad `Up` | Walking in the field |
| Say where you are | `L` | `LB` held, then D-pad `Left` | Walking in the field |
| Read more of the focused menu entry | `F` | Right stick press (`R3`) | A menu is open |
| Note a moment for your report ([mark key](#mark-key-note-a-moment-for-your-report)) | `F8` | none | Anywhere in the game |

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

## What is spoken where

### Dialogue

- Each line is read when its window opens. Reader off silences dialogue lines only, not the keys, the load message or
  the settings message.
- `R` or `X` with no line yet says "No dialogue to repeat". `T` or `LB` and `RB` says "Dialogue reader on" or
  "Dialogue reader off".
- Reply choices: when they appear, "Choose:" and the options. Moving the cursor speaks the option and its position, such
  as "Option, 2 of 3".
- Lines the voice actors speak follow your voiced-lines setting: the speaker only, the full line, or nothing. The default
  says the name for English voice and the full line for Japanese voice
  ([change how voiced lines are read](../how-to/change-how-voiced-lines-are-read.md)).

### In the field

- `N` says up to 8 people, doors, chests and enemies, each with its name or kind, then how far and which way from where
  you face, such as "Chest, 4 steps ahead-left". With nothing near: "Nothing nearby".
- The pad's `LB` and D-pad steps through the same list, such as "2 of 5. Door, 9 steps behind"; after 10 seconds without
  a step it starts again at the nearest.
- `L` says the place name the game last showed, or "Place not known yet".
- Distances are rough steps: treat them as near or far.

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
