# Changelog

What changed for players, newest first. It lists only what you would notice in the game or the installer. How the mod
is built is in [the contributor docs](docs/contributors/README.md). The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Lines tagged `[NEEDS VERIFICATION by ear]` have not been heard in the game with a screen reader yet. The maintainer
checks them in the first game session and removes the tag.

## Unreleased

- **The Calendar says the day.** Opening the Calendar says "Calendar, Waters Day, day 12 of 30", and each move says the
  weekday and the day you land on (the five weekdays are as heard in a test session). The month, events and discounts
  aren't spoken yet (#193). `[NEEDS VERIFICATION by ear]`
- **Dialogue choices say how many options, not all of them.** When a choice opens, the mod says "Choice, 3 options"
  and then the focused option, instead of reading every option at once. Move the cursor to hear the others (#730).
- **Field sounds wait while the game takes over.** The guiding beep and "in reach" call-outs go quiet when a menu,
  dialogue or cutscene starts, and the beep picks up again toward the same thing when you can walk again, unless you
  are on another map by then (#702). The beep waits a second after you can walk again, so a pause between the lines of
  a scene doesn't bring it back (#709). Whether a scripted walk quiets them too isn't known yet.
- **The Equipment slots are named.** In the slot list, rows two to five say their slot ("Weapon, 2 of 5"); the first row
  (the archetype), the characters and the items are still said by place (#217). `[NEEDS VERIFICATION by ear]`
- **The reader key works outside dialogue.** `T`, or `LB` with `RB`, now turns the reader off and on in the field and
  over a tutorial pop-up, so a reader turned off before a pop-up can be turned back on (#769).
  `[NEEDS VERIFICATION by ear]`
- **A beep that guides you to something nearby.** In the field, `G`, or `LB` with the D-pad's Right, starts a
  repeating beep toward the thing you last stepped to, or the nearest you aren't already at: from its side, higher
  ahead, faster as you near it. It says when you reach it; press again to stop (#183). `K` and `J` step through the
  nearby list from the keyboard. `[NEEDS VERIFICATION by ear]`
- **Save and load slots are read.** Each slot says "Slot 3"; the load list's first row says "Autosave". The place,
  playtime and whether a slot is empty aren't read yet (#622). `[NEEDS VERIFICATION by ear]`
- **The guidance beep leads around walls.** It now points at the next turn of a walking route over the map's walkable
  area, not straight through buildings, and plans again if you stray or get stuck. Where it can't find a route it
  points straight and says "No path found, pointing straight" once (#690). `[NEEDS VERIFICATION by ear]`
- **The game-over screen is read.** Its voiced line is spoken, then "Fantasy is Dead. Press A to continue." once the
  voice ends; if you last used the keyboard it says "Press Space to continue" (#623). `[NEEDS VERIFICATION by ear]`
- **"Save complete" is read.** The small pop-up after saving is spoken (#622). `[NEEDS VERIFICATION by ear]`
- **An installer exe beside the zip.** Each release also carries one file, `metaphor-accessibility-VERSION-installer.exe`:
  open it and it says a welcome line, checks the files it carries ("Download checked"), and runs the same spoken
  installer as the zip, in its window. A damaged download says "This download is damaged" and installs nothing. It is
  not signed yet, so Windows warns before it runs (#608). `[NEEDS VERIFICATION by ear]`
- **A welcome on first launch.** The first time the game starts with the mod, the title screen says which version is
  running and that `N`, `L` and `G` are the field keys, once (#617). `[NEEDS VERIFICATION by ear]`
- **A mark key for reports.** Press `F8` when something goes wrong: two short clicks, and a numbered marker with the
  time goes into `report-log.txt`. The log also lists each screen and menu move the mod said nothing for. Nothing is
  sent anywhere. The Alpha feedback form asks when you pressed it (#597). `[NEEDS VERIFICATION by ear]`
- **The installer says where the mod is.** At the end of an install it says the mod's folder, naming your own folders
  ("your AppData folder, Local, Programs, Reloaded-II, ...") instead of reading out the path, and that `uninstall.cmd`
  in that folder removes the mod (#675). `[NEEDS VERIFICATION by ear]`
- **The install check says the mod's version.** `check-install.ps1`, which the installer also runs at the end, now says
  "The mod is version ..." for the copy in Reloaded-II's mods folder, so you can hear which one you have (#674).
  `[NEEDS VERIFICATION by ear]`
- **Tutorial pop-ups say what they are.** When a tutorial opens, the mod reads its title, how many pages it has, and
  its first page. Each page you turn to is read as "Page 2 of 3" and its text (#118). `[NEEDS VERIFICATION by ear]`
- **Yes and No buttons.** The installer asks each question in a small window with Yes and No buttons, which your
  screen reader reads, instead of asking you to type an answer. Where it can't find the game, you choose
  `METAPHOR.exe` in Windows' Open window (#607). `[NEEDS VERIFICATION by ear]`
- **Battle hits are read.** Each hit says its damage, such as "34", or "34 healed". After the action, one line says
  who changed: party members by slot, such as "Ally 1 89, Ally 2 120", and each enemy with its HP left, numbered when
  there are several. Misses and blocked hits aren't read yet, and names aren't known yet (#247).
  `[NEEDS VERIFICATION by ear]`
- **Knock-outs are said.** The hit that takes a party member to 0 HP adds "knocked out" ("Strohl 40, knocked out"), one
  that defeats an enemy adds "defeated", and a heal from 0 HP adds "revived" (#254). `[NEEDS VERIFICATION by ear]`
- **No more gaps where the screen shows a skill, an item or a button.** Text that names a skill or a status condition
  now says its name, as after "Required Skill:". Items, Archetypes, attributes, buttons and other icons, which the mod
  can't name yet, say what kind of thing goes there ("item", "button") instead of leaving a gap, as does the action a
  new key binding clashes with (#108, #109, #564). `[NEEDS VERIFICATION by ear]`
- **Battle banners are read.** The short banner during a turn, such as a boss powering up, "Attack rose!", "Not
  enough MP!" or "Gained one Turn Icon!", is read once, after the hits. A name in it isn't read yet. The setting
  `speech.battleBanners` turns it off (#692, #661). `[NEEDS VERIFICATION by ear]`
- **Battle's skill and item lists are read**: "Skills" or "Items" as the list opens, then each skill or item's name
  and "n of total" as you move. F, or R3 on the pad, reads the focused one's description (#247). Past the first
  screen each row keeps its own name as the list scrolls; one whose name isn't known is said by its position.
  `[NEEDS VERIFICATION by ear]`
- **A status key.** In a battle, H, or L3 on the pad, pressed on its own, says each party member's HP, then the
  enemies' (#631). Outside a battle both keys reach the game as before, so L3 still recentres the world map.
  `[NEEDS VERIFICATION by ear]`
- **What is near, and where am I.** In the field, `N` says the people, doors, chests and enemies near you, nearest
  first, with rough steps and a direction from the camera, as on screen (#686); `LB` with the D-pad's Down and Up
  steps through them. `L`, or `LB` with Left, says the place name the game last showed (#181). `[NEEDS VERIFICATION by ear]`
- **Doors and chests in reach.** Walking up to a door or chest says "Door, in reach" on its own, once, where the
  game's action prompt would show; it's said again after you move away or turn and come back (#644).
  `[NEEDS VERIFICATION by ear]`
- **Sound cues can be turned off and their volume set.** The mod can now play short sounds of its own, explained in
  [sound cues](docs/players/reference/sound-cues.md); `soundCues` and `soundCueVolume` in `settings.json` control
  them. The battle hit sounds arrive with spoken battle hits (#247, #636). `[NEEDS VERIFICATION by ear]`
- **Sound cues can overlap and come from the left or right.** Two cues at once both play, and the mod can now hold a
  steady tone that moves and changes pitch, ready for guidance and wall sounds (#637). `[NEEDS VERIFICATION by ear]`
- **One file to open after extracting.** The zip's top level holds `Install Metaphor accessibility.cmd` beside the
  mod's folder: extract the zip and press Enter on it, with no search through the mod's files (#604).
  `[NEEDS VERIFICATION by ear]`
- **Update notice.** Once per game start the mod asks GitHub for its newest release, and the title screen says "A
  newer version of the mod is available:" and the version when there is one. It sends nothing about you;
  `"checkForUpdates": false` in `settings.json` turns it off. See
  [Turn off the update check](docs/players/how-to/turn-off-the-update-check.md) (#576). `[NEEDS VERIFICATION by ear]`

## 0.1.0 - not released yet

The first early-access release. It is for the Steam version of Metaphor: ReFantazio on PC, single-player only.

### What works

- **Dialogue is read aloud.** The mod sends the text of the dialogue window to your screen reader, or to Windows'
  own voice when no screen reader is running. `[NEEDS VERIFICATION by ear]`
- **Reply choices are read aloud**, and the focused choice is read as you move through them. `[NEEDS VERIFICATION by ear]`
- **Voiced lines.** You choose whether a line the actors speak is also read: the speaker's name only, the full line, or
  nothing. The default reads the name for English voice and the full line for Japanese voice. See
  [Change how voiced lines are read](docs/players/how-to/change-how-voiced-lines-are-read.md).
- **Repeat and switch off.** While the dialogue window is open, X (or `R` on the keyboard) repeats the last line, and
  LB plus RB together (or `T`) turns spoken dialogue on or off. `[NEEDS VERIFICATION by ear]`
- **The title menu and the main menu are read**, and list screens say "item, n of total" as you move. This is early;
  see the limits below. `[NEEDS VERIFICATION by ear]`
- **A spoken installer** (`install.ps1`) does the whole setup and says each step:
  - it finds the game and installs Reloaded-II 1.31.0, the .NET 9 Desktop Runtime and the Visual C++ Redistributable
    if they are missing;
  - every download is the pinned version and is checked against a recorded hash before it is used;
  - it asks in plain words before it starts and before each download, and says what to type;
  - it refuses to run as administrator, and if User Account Control is turned off it says so and asks before going on;
  - when Windows' Controlled folder access blocks a write, it names that as the cause;
  - it puts a shortcut called "Metaphor ReFantazio with accessibility" on your desktop, and ends with a check of the
    install. Run it again to update the mod;
  - `uninstall.cmd` removes the mod, its settings, its shortcut and its entry for the game, and asks before
    removing a Reloaded-II it installed. It keeps the runtimes.

  `[NEEDS VERIFICATION by ear]` The installer's spoken wording. See
  [Install the mod](docs/players/how-to/install-the-mod.md).
- **A signed download.** CI builds the zip, rebuilds it to compare, and signs it with an attestation. See
  [Check a download](docs/players/how-to/check-a-download.md) to verify yours before you install it.

### What does not work yet

- Most menus are not read: the title menu, the main menu and reply choices are the only screens so far.
- Battle is not read.
- There is no help finding your way around the field (walking, interactions, the map).
- Only the Steam version is supported, on the game build the notes name. A game update can stop the mod from
  loading; it then does nothing rather than guess.
- Only NVDA has been planned for; other screen readers are untested.

### Credits

All changes so far come from the maintainer. The ideas and code this project borrows from other projects are credited
in [CREDITS.md](CREDITS.md).
