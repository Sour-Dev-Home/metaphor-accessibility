# Install the mod

> **Draft for testers on dev builds.** There is no release yet. This page is finished with the first alpha
> ([#131](https://github.com/Sour-Dev-Home/metaphor-refantazio/issues/131)). Steps marked `[NEEDS VERIFICATION]` have
> not been checked by ear with a screen reader yet. If one is wrong, say so in an issue.

This guide installs Reloaded-II, the program that loads the mod into the game, adds the game and the mod to it, and
makes a desktop shortcut that starts the game with the mod. It is written for NVDA. Each step says what you should
hear.

You need the game installed on PC (single-player only) and NVDA running.

## Install Reloaded-II

The mod is checked against Reloaded-II **1.31.0**
([run notes](../../raw-sources/mod-entry-point-first-run-2026-11-15.md)). Use that version. A newer one may work, but
nobody has checked.

1. Open the Reloaded-II releases page on GitHub: `https://github.com/Reloaded-Project/Reloaded-II/releases`. NVDA
   reads a page title naming the project.
2. Press `H` to move between headings until NVDA reads `1.31.0`. Newer releases come first, so go on past them.
3. Press `K` or `Tab` through the links under that release's "Assets" heading and open the Windows setup file.
   `[NEEDS VERIFICATION]` The exact asset name for 1.31.0, and whether the release offers a setup program or only a
   zip.
4. Run the setup. `[NEEDS VERIFICATION]` What the setup's dialogs say and which buttons NVDA finds. Accept the default
   folder unless you have a reason not to.

## Add the game to Reloaded-II

`[NEEDS VERIFICATION]` All of this section. Reloaded-II's window is reachable with NVDA mainly through the review
cursor and a simulated mouse click
([P4G Access notes](../../raw-sources/persona-4-golden-access-2026-11-16.md)).

1. Start Reloaded-II. NVDA should read its window title.
2. Find the button that adds an application, using NVDA's review cursor: `NVDA+numpad 7` moves to the top of the
   window, and `NVDA+numpad 9` or `NVDA+numpad 3` moves by line. `Shift+NVDA+arrows` is the laptop layout.
3. Choose the game's program file, `metaphor.exe`, in the game's install folder. Reloaded-II names the new entry after
   the program (its ID is `metaphor.exe`).

## Add the mod from a dev build

`[NEEDS VERIFICATION]` Where testers get a dev build, and what it is called. The project's CI builds a package of the
mod on every merge and keeps it as a workflow artifact with a SHA-256 hash
([README](../../../README.md#checks)). The package holds our mod only; Reloaded-II is installed separately.

1. Download the package from the CI run's artifacts and check its SHA-256 against the one shown with it.
2. Unzip it. You get a folder named for the mod ID, `metaphor.accessibility`.
3. Open Reloaded-II's settings file, `%APPDATA%\Reloaded-Mod-Loader-II\ReloadedII.json`, in Notepad. Find the value of
   `ModConfigDirectory`. This is the mods folder
   ([P4G Access notes](../../raw-sources/persona-4-golden-access-2026-11-16.md)).
4. Copy the `metaphor.accessibility` folder into that mods folder.
5. Enable the mod for the game. Doing this in Reloaded-II's window is hard with a screen reader, so the P4G Access mod
   ships a ready-made `AppConfig.json` for the game's folder under Reloaded-II's applications folder. We don't ship one
   yet. `[NEEDS VERIFICATION]` Enabling the mod by ear in the window: find the game's mod list with the review cursor,
   move to `metaphor.accessibility`, and click it.

Developers can skip all of this with `scripts/deploy-dev.ps1`
([deploy a dev build](../../contributors/how-to/deploy-a-dev-build.md)).

## Make a desktop shortcut that starts the game with the mod

Always start the game from this shortcut. These steps are P4G Access's method, which we expect to work the same here
`[NEEDS VERIFICATION]`.

1. Open Reloaded-II.
2. Press `Tab` to reach the application list and open the game's entry.
3. Use NVDA's review cursor (`NVDA+numpad` keys, or `Shift+NVDA+arrows` on a laptop) to reach `Create Shortcut` on the
   game's page.
4. Move the mouse to the review cursor with `NVDA+Shift+M`, then click with `NVDA+numpad slash`, or `NVDA+[` on a
   laptop.
5. A shortcut appears on the desktop, named after the game with `(Reloaded)` in it. `[NEEDS VERIFICATION]` the exact
   name and whether NVDA announces the click.

P4G Access's own guide says not to start the game from Steam, because it often fails to load some mods. Another
instruction from its author says Steam works while Reloaded-II is running, but the shortcut is more reliable. Which
holds for Metaphor is `[NEEDS VERIFICATION]`; use the shortcut.

## Launch

1. Close the game and Reloaded-II if they are running.
2. Open the `(Reloaded)` shortcut on the desktop. The game starts. You can close Reloaded-II's window if it shows up.
   `[NEEDS VERIFICATION]` whether Reloaded-II stays open.

## Copy in a Reloaded-II app config

Enabling a mod in Reloaded-II's window is hard with a screen reader. Persona-4-Golden-Access gets round it by shipping a
ready-made `AppConfig.json`. We don't ship one yet, so edit the one Reloaded-II made
([P4G Access notes](../../raw-sources/persona-4-golden-access-2026-11-16.md)). `[NEEDS VERIFICATION]` All of this
section, by ear.

1. In Notepad, open `%APPDATA%\Reloaded-Mod-Loader-II\ReloadedII.json`. Find `ApplicationConfigDirectory`. This is where
   Reloaded-II keeps one folder per game.
2. Open the folder for the game, the one holding an `AppConfig.json` whose `AppId` is `metaphor.exe`. If you added the
   game as above, it is there.
3. Open that `AppConfig.json` in Notepad. Find `EnabledMods`. Make it list the mod's ID, so the line reads
   `"EnabledMods": ["metaphor.accessibility"]`. Save the file.
4. If `AppLocation` doesn't point at your `metaphor.exe`, correct it. A relative path is read from the folder holding
   the file ([Reloaded-II config notes](../../raw-sources/reloaded-ii-app-and-mod-config-2026-11-15.md)).

## What the mod says when it loads

When the game starts, the mod speaks one of three messages once:

1. `Metaphor accessibility loaded`: the mod is running.
2. `This game version isn't supported yet; the mod is off`: your game build is one the mod doesn't know. It hooks
   nothing and the game runs as normal.
3. `Metaphor accessibility couldn't start; the mod is off`: any other failure.

You may hear nothing if your screen reader or Windows speech isn't reachable. Then look at the newest file in
`%APPDATA%\Reloaded-Mod-Loader-II\Logs\`, which lists the mod by its ID, `metaphor.accessibility`. If you hear the first
message, the install worked.

## Keys and buttons

Everything the mod reads is here. The mod reads no mouse input.

### Repeat and reader toggle

1. `R` on the keyboard, or `X` on a controller: repeats the last dialogue line in full. With no line yet, it says "No
   dialogue to repeat".
2. `T` on the keyboard, or `LB` and `RB` held together on a controller: turns spoken dialogue off or on. It says
   "Dialogue reader on" or "Dialogue reader off".

A press counts when the key goes down, not while it is held. A key already held when a dialogue window opens does
nothing. They can't be rebound yet.

By default they work at any time, on every screen, and the game sees the presses too, so `R`, `T`, `X` and `LB`+`RB`
keep their normal job. Reader off silences dialogue lines only, not the keys, the load message or the settings message.

With the opt-in window hooks file they work only while a dialogue window is open, and the game doesn't see them then.
To turn this on, put an empty file named `opt-in-dialogue-window.txt` in the mod's folder (`metaphor.accessibility` in
your mods folder) and restart the game. An update replaces that folder, so add the file again afterwards. Delete the
file to turn it off ([details](../../contributors/how-to/turn-on-the-dialogue-window-hooks.md)).

### Menus and choices: no keys of their own

The mod adds no keys here. It speaks what the game's own cursor does, so use the game's own movement keys or buttons.

1. Title menu: when the cursor settles on an item, the mod speaks it with its position. The first time it adds
   "Main menu" in front, such as "Main menu, New Game, 1 of 4".
2. Reply choices in dialogue: when the choices appear, the mod speaks "Choose:" and the options. Moving the cursor
   speaks the option and its position, such as "Option, 2 of 3".
3. The menu during play: opening it speaks "Menu" and the entry's position, such as "Menu, 3 of 9"; each move speaks
   the position. The entries, in order: Skill, Item, Equipment, Party, Follower, Quest, Calendar, Memorandum, System.
   Their names aren't spoken yet.
4. Any other menu screen: each move speaks the position only, such as "4".

`[NEEDS VERIFICATION]` All by ear in the game. They need a supported game build.

### Voiced lines

For lines the voice actors speak, the mod can say the speaker only, or the full line. The default (`auto`) says the name
when the voice is English and the full line when it is Japanese. To change it, make a file named `settings.json` in the
mod's user-config folder, by default `User\Mods\metaphor.accessibility` next to Reloaded-II (`[NEEDS VERIFICATION]` where
it lands on your machine), holding:

`{ "voicedLines": "nameOnly" }`

The choices are `auto`, `nameOnly`, `fullLine` and `nothing`. The mod reads the file at start and again when you save
it, and says "Voiced lines: name only." (or the new value) when it changes. A file it can't use is ignored and the
default applies.

### Braille

There is no braille setting. Each announcement goes to your screen reader's speech and braille together, where it
supports that, and to speech alone where it doesn't. `[NEEDS VERIFICATION]` By ear and with a display.

## Report a problem

Use the Alpha feedback form on the project's issues page.

1. Find `report-log.txt` in the `metaphor.accessibility` folder in your mods folder. The mod writes it fresh each time
   the game starts, so copy it before you start the game again.
2. Open the form, answer its questions, and drag the file into the "Report log" box, or choose it with the file picker.
3. The log holds what the mod did (whether it loaded, how many hooks went in, errors, and which lines and choices the
   game asked for, by label such as `Title_System_08`), never the game's text, and your user name is replaced with
   `<redacted>`. Read it in Notepad first if you want to check.

## Credits

- The desktop-shortcut steps, the NVDA review-cursor steps and the idea of a copy-in app config come from
  [Persona-4-Golden-Access](https://github.com/AquaRose7/Persona-4-Golden-Access) by AquaRose7 (GPL-3.0), at `dcff255`
  (v2.2.6). No code is copied.
- Listing every key, with when it works, comes from the [AccessMods](https://github.com/AccessMods/PhoenixWrightTrilogy)
  organisation (MIT): blind players of its mods hit keys the README left out. Pinning the loader version follows its
  CONTRIBUTING.

More in [`CREDITS.md`](../../../CREDITS.md).
