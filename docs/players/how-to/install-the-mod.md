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

## What the mod says when it loads

Nothing yet, as far as we know. The mod writes a status file and a log line when it loads, but we found no code that
speaks on load `[NEEDS VERIFICATION]`. Once the game is running and a line of dialogue appears, the mod should read it
aloud through your screen reader or Windows speech
([ADR 0005](../../adr/0005-prism-speech-backend.md)). If the game's version doesn't match one the mod knows, it
refuses to hook anything and the game runs as normal, silently.

To check that it loaded, look at the newest file in `%APPDATA%\Reloaded-Mod-Loader-II\Logs\`. It lists the mod by its
ID, `metaphor.accessibility`. If you report a problem, attach that file.

## Keys

Not yet checked in the game:

| Key | Controller | Does |
|---|---|---|
| R | X | Repeats the last dialogue line in full |
| T | LB and RB together | Turns spoken dialogue off or on, and says which |

They work at any time, and the game sees these presses too, so they also keep their normal job. They can't be changed
yet. An optional file makes them work only during dialogue, hidden from the game then
([turn on the dialogue window hooks](../../contributors/how-to/turn-on-the-dialogue-window-hooks.md)).

This list will grow as the mod gains features. Every key the mod reads will be listed here, including any the game
doesn't show, because players of other accessibility mods found keys their README left out
([AccessMods survey](../../raw-sources/accessmods-org-survey-2026-11-16.md)).

## Credits

The desktop-shortcut method and the idea of shipping a ready-made config come from
[Persona-4-Golden-Access](https://github.com/AquaRose7/Persona-4-Golden-Access) by AquaRose7 (GPL-3.0). Listing every
key comes from the AccessMods organisation's READMEs.
