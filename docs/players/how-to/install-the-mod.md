# Install the mod

> **Draft for testers.** The newest download is the pre-release `v0.0.1-unstable.4`; there is no stable release yet.
> Some details have not been checked by ear with a screen reader yet; they are listed in
> [Not checked by ear yet](#not-checked-by-ear-yet). If a step is wrong, say so in an issue.

This guide installs the mod with its spoken installer: one file to open after extracting the zip. The installer sets up
Reloaded-II, the program that loads the mod into the game, adds the game and the mod to it, and makes a desktop shortcut
that starts the game with the mod. It is written for NVDA. Each step says what you should hear.

You need the game installed on PC (single-player only) and NVDA running. Download the zip from the
[releases page](https://github.com/Sour-Dev-Home/metaphor-accessibility/releases) and check it first
([Check a download](check-a-download.md)).

## Install with the spoken installer

The zip holds two things: the mod's folder, `metaphor.accessibility`, and one file beside it,
`Install Metaphor accessibility.cmd`, which starts the installer in that folder. The installer does every step on this
page for you and says each one as it goes. It needs no clicks in Reloaded-II's window.

1. Extract the whole zip (unblock it first if Windows marked it, see
   [If Windows says the file is from the internet](#if-windows-says-the-file-is-from-the-internet)). File Explorer opens
   the extracted folder. Opening the file inside the zip without extracting doesn't work: it says to extract the zip
   first.
2. Press `End` to reach the last item, "Install Metaphor accessibility", the one file there (File Explorer lists the
   mod's folder before it), and press Enter.
3. Windows probably asks first, because the file came from the internet and isn't signed: an "Open File - Security
   Warning" window asking whether to run it. Choose `Run`. With Smart App Control on, Windows blocks the file instead;
   see the next section. Why Windows asks, and what was checked:
   [notes](../../raw-sources/install-launcher-motw-2026-11-20.md).
4. A window opens and the installer starts. It asks before it starts and before each download, each question in a
   small window with a Yes and a No button that your screen reader reads. Press `Enter` for the button it lands on, `Y`
   for Yes or `N` for No, or `Tab` to move between them. Where the installer can't find the game in Steam, Windows'
   Open window appears: choose `METAPHOR.exe` in the game's install folder, or press `Escape` to stop.
5. It finds the game, installs Reloaded-II 1.31.0 if you don't have it (in your user folder, no admin rights needed),
   installs the .NET 9 Desktop Runtime and the Visual C++ Redistributable if they're missing, copies the mod in, turns
   it on for the game, and puts a shortcut called "Metaphor ReFantazio with accessibility" on your desktop. The two
   runtimes need admin rights: when it installs one, Windows asks for permission, and you answer yes there too.
6. It ends by running the install check ([below](#if-the-mod-says-nothing-run-the-install-check)) and says what it
   found. Before you start the game with the mod for the first time, the check says the mod hasn't reported a start
   yet; that is expected. Then it says where the mod is and how to remove it: "The mod is in your AppData folder,
   Local, Programs, Reloaded-II, Mods, metaphor.accessibility. To uninstall, run uninstall.cmd in that
   folder." "Your AppData folder, Local" is the folder that typing `%LOCALAPPDATA%` in `Windows+R` opens; the
   window shows the full path. See [Uninstall](#uninstall).
7. The window stays open and its last line says "The installer has finished. Read what it said above, then press any
   key to close this window." Read back with NVDA's review cursor if you need to, then press a key.
8. Start the game from the desktop shortcut. Always start it from this shortcut. The keys the mod adds are on
   [Keys and what the mod speaks](../reference/keys-and-speech.md); if something goes wrong,
   [Troubleshoot the mod](troubleshoot.md).

Each step says whether it worked. A step that fails says what to do and stops there. Run the installer again after
fixing that, or to update the mod: it changes only what needs changing and says so. With NVDA, JAWS or ZoomText
running, it speaks each line through your screen reader, with the speech library the mod ships, so you don't need to
read the window; your screen reader reads the question windows itself. Windows' own voice speaks the steps and questions
when no screen reader is running. That voice is an English one when Windows has one; otherwise the installer says so and
uses Windows' default voice. Open it with Enter, not "Run as administrator": it refuses to run as administrator. With
User Account Control turned off every window is administrator, so it says so and asks before going on.

`install.cmd` in the mod's folder does the same as `Install Metaphor accessibility.cmd`. If Windows won't open the
first, try the second. If neither opens, Windows is blocking the file, not failing to find it: unblock it as described
in the next section. Typing a command to get round the block isn't something this guide asks of you, because it needs
switches that a screen reader doesn't voice well; if the block stays, report it with the
[alpha feedback form](#report-a-problem).

The installer checks every file it downloads against a recorded hash. A download that gets no data for a minute, or
takes more than half an hour, stops, and the installer says what to do. If you ever open Reloaded-II's own window, it may
offer to update itself; that update comes from Reloaded-II's newest release, which nobody has checked with this mod.

If you would rather set Reloaded-II up yourself, see [Appendix: install by hand](#appendix-install-by-hand).

## If Windows says the file is from the internet

Windows marks a downloaded file as from the internet, and the mod's scripts aren't code-signed, so it may warn or ask
before running them. Check the download first ([Check a download](check-a-download.md)); only then follow these.
Every step is by keyboard. Microsoft doesn't document the spoken phrases below
([notes](../../raw-sources/smartscreen-and-unblock-microsoft-docs-2026-11-20.md)).

### Unblock the zip before you unzip it

Unzipping in File Explorer copies the mark to every file inside, so clearing it on the zip first saves a warning later.

1. In File Explorer, move to the zip with the arrow keys. Press `Alt+Enter`. A Properties window opens and the screen
   reader reads its title, the zip's name followed by "Properties".
2. On the General tab, press `Tab` until you reach a check box named "Unblock", near the bottom, in the Security
   group. The screen reader reads "Unblock", "check box", "not checked". If there is no such check box, the file isn't
   marked: close the window with `Escape` and go on.
3. Press `Space`. It reads "checked".
4. Press `Tab` to "OK" and press `Enter`. The window closes.
5. Unzip: press the `Applications` key (or `Shift+F10`), choose "Extract All...", then press `Enter` on "Extract".

If you unzipped first, repeat steps 1 to 4 on `Install Metaphor accessibility.cmd` in the extracted folder.

### Get past "Windows protected your PC"

SmartScreen shows this blue window when a downloaded program has no reputation yet. It is not a verdict that the file
is harmful. `Install Metaphor accessibility.cmd` usually gets the older "Open File - Security Warning" window
instead (step 3 of the installer above).

1. The window opens and the screen reader reads "Windows protected your PC", then its text, which names the file.
   Press `Tab`: focus lands on the link "More info". Press `Enter`.
2. A button "Run anyway" appears. Press `Tab` until the screen reader reads "Run anyway", "button", then press `Enter`.
   The file runs.
3. If the window offers only "Don't run", or you are on a work or school PC, an administrator has blocked the file;
   you can't go on from here. With Smart App Control on, Windows blocks the file with no such button: unblock it as
   above.

## What the mod says when it loads

When the game starts, the mod speaks one of three messages once: `Metaphor accessibility loaded` (the mod is running),
`This game version isn't supported yet; the mod is off` (your game build is one the mod doesn't know; it hooks nothing
and the game runs as normal), or `Metaphor accessibility couldn't start; the mod is off` (any other failure). If you
hear the first, the install worked. If you hear another, or nothing, see [Troubleshoot the mod](troubleshoot.md).

The first time the game starts with the mod, the title screen then adds, once: "Metaphor accessibility" and the
version, "is running. In the field, N says what's near you, L says where you are, G guides you." After that the mod
sets `"welcomed": true` in its `settings.json`, and later launches skip it. Set it to `false` to hear it again;
`[NEEDS VERIFICATION]` by ear in the game.

## If the mod says nothing: run the install check

The installer ends with the install check, which says why the mod didn't load. To run it again, run the installer
again; it changes only what needs changing. Nothing needs typing. What the check looks at, and what to do with its
result, is in [Troubleshoot the mod](troubleshoot.md#no-speech). With no screen reader running, it speaks each result
with Windows' own voice; with NVDA, JAWS or ZoomText running it speaks through your screen reader. Spoken lines leave
out folder paths and long codes; the printed lines keep them.

## Keys and what the mod speaks

Every key and button the mod adds, and what it speaks in dialogue, the field and menus, is on its own page:
[Keys and what the mod speaks](../reference/keys-and-speech.md). Once the game has started from the shortcut, that is
the page to open next.

## Settings

### Voiced lines

For lines the voice actors speak, the mod can say the speaker only, or the full line. The default (`auto`) says the name
when the voice is English and the full line when it is Japanese. To change it, make a file named `settings.json` in the
mod's user-config folder, by default `User\Mods\metaphor.accessibility` next to Reloaded-II, holding:

`{ "voicedLines": "nameOnly" }`

The choices are `auto`, `nameOnly`, `fullLine` and `nothing`. The mod reads the file at start and again when you save
it, and says "Voiced lines: name only." (or the new value) when it changes. A file it can't use is ignored and the
default applies.

### Update check

Each time the game starts, the mod asks GitHub once for its newest release, and the title screen says "A newer version
of the mod is available:" and the version when there is one. It sends nothing about you. To stop it, add
`"checkForUpdates": false` to the same `settings.json`; see
[turn off the update check](turn-off-the-update-check.md).

### Braille

There is no braille setting. Each announcement goes to your screen reader's speech and braille together, where it
supports that, and to speech alone where it doesn't.

## Uninstall

The mod's folder holds `uninstall.cmd`, which starts the same installer to remove the mod. It says each step as the
install does.

1. Close the game and Reloaded-II.
2. Open the mod's folder in File Explorer: the unzipped folder you downloaded, or the mod's folder in Reloaded-II's
   mods folder, which the uninstall then removes. Move to `uninstall.cmd` with the arrow keys, or type `uninstall`, and
   press Enter. Windows may ask first, as it does for `install.cmd`; choose `Run`. If Windows won't open it, it is
   blocking the file; unblock it as for the installer.
3. A window with a Yes and a No button asks "Uninstall the mod?" and says what it does, as the install's questions do.
   Press `Y` for Yes. The focus starts on No, so `Enter` alone, or `N`, stops without changing anything.
4. It says five steps, each starting with "Done", "Nothing to remove" or "Failed": it turns the mod off for the game in
   Reloaded-II, keeping your other mods on; removes the mod's folder and the mod's settings from Reloaded-II; deletes
   the desktop shortcut "Metaphor ReFantazio with accessibility"; and checks Reloaded-II.
5. If the installer installed Reloaded-II, a second window asks "Remove Reloaded-II too?". That also removes every other
   mod and game you added to Reloaded-II, so press `Enter` alone (No) to keep it, or `Y` to remove it. A
   Reloaded-II you installed yourself is kept without asking.
6. It says it kept the .NET 9 Desktop Runtime and the Visual C++ Redistributable, which other programs may use. To
   remove them, use Installed apps in Windows Settings.
7. Its last line is "The uninstall is done", or "There was nothing left to remove" when you run it again. The window
   stays open and then says "The uninstall has finished. Read what it said above, then press any key to close this
   window."

It removes nothing outside Reloaded-II's folder, and leaves alone a folder that holds a link to somewhere else and a
shortcut of the same name that starts another program; a step that leaves something says so as "Failed" and what to
do. The other steps still run. Reloaded-II's settings file in your AppData folder stays.

## Report a problem

Use the Alpha feedback form on the project's issues page.

1. Find `report-log.txt` in the `metaphor.accessibility` folder in your mods folder. The mod writes it fresh each time
   the game starts, so copy it before you start the game again.
2. Open the form and answer its three questions: what you were doing, roughly when you pressed the
   [mark key](../reference/keys-and-speech.md#mark-key-note-a-moment-for-your-report), and the file: drag it into the
   "Attach report-log.txt" box, or choose it with the file picker.
3. The log holds what the mod did (whether it loaded, how many hooks went in, errors, and which lines and choices the
   game asked for, by label such as `Title_System_08`), your mark key presses and the screens the mod said nothing
   for, never the game's text. Your user name is replaced with `<redacted>`, and the names you typed for your
   characters with `<name>`. Its first lines say how each part of the mod started. Read it in Notepad first if you
   want to check.
4. If the game closed on its own, add Windows' record of the crash too:
   [Find crash details](find-crash-details.md), no admin rights needed.

## Not checked by ear yet

Nobody has heard these with a screen reader yet, so what NVDA reads may differ from what this page says. Tell us in an
issue if it does.

- `[NEEDS VERIFICATION]` The spoken phrases and the focus in the Windows windows above: Properties, "Windows protected
  your PC", "Open File - Security Warning", and the installer's Yes and No windows and Open window (#573). Whether
  File Explorer opens the extracted folder after "Extract All", what NVDA reads for the installer file, and whether
  `Escape` answers No in the question windows (Windows' Yes and No windows usually ignore it).
- `[NEEDS VERIFICATION]` Whether unblocking lifts the block when Smart App Control is on.
- `[NEEDS VERIFICATION]` The installer's spoken wording, and with User Account Control off (#601).
- `[NEEDS VERIFICATION]` The install check by ear with NVDA, and whether Windows asks before running a script from a
  downloaded zip.
- `[NEEDS VERIFICATION]` Braille with a display, and where `settings.json` lands on your machine. The keys and spoken
  lines are marked on their own page.
- `[NEEDS VERIFICATION]` The uninstall, by ear.

## Appendix: install by hand

The installer above does all of this. Use this appendix only if you want to set Reloaded-II up yourself. It is written
for NVDA and was never checked by ear, so expect to explore.

### Install Reloaded-II

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

### Add the game to Reloaded-II

`[NEEDS VERIFICATION]` All of this section. Reloaded-II's window is reachable with NVDA mainly through the review
cursor and a simulated mouse click
([P4G Access notes](../../raw-sources/persona-4-golden-access-2026-11-16.md)).

1. Start Reloaded-II. NVDA should read its window title.
2. Find the button that adds an application, using NVDA's review cursor: `NVDA+numpad 7` moves to the top of the
   window, and `NVDA+numpad 9` or `NVDA+numpad 3` moves by line. `Shift+NVDA+arrows` is the laptop layout.
3. Choose the game's program file, `metaphor.exe`, in the game's install folder. Reloaded-II names the new entry after
   the program (its ID is `metaphor.exe`).

### Add the mod

1. Extract the release zip and check it, as [Check a download](check-a-download.md) says. You get a folder named for the
   mod ID, `metaphor.accessibility`.
2. Open Reloaded-II's settings file, `%APPDATA%\Reloaded-Mod-Loader-II\ReloadedII.json`, in Notepad. Find the value of
   `ModConfigDirectory`. This is the mods folder
   ([P4G Access notes](../../raw-sources/persona-4-golden-access-2026-11-16.md)).
3. Copy the `metaphor.accessibility` folder into that mods folder.
4. Enable the mod for the game with the app config below.

Developers can skip all of this with `scripts/dev/session.ps1 deploy`
([deploy a dev build](../../contributors/how-to/deploy-a-dev-build.md)).

### Copy in a Reloaded-II app config

Enabling a mod in Reloaded-II's window is hard with a screen reader. Persona-4-Golden-Access gets round it by shipping a
ready-made `AppConfig.json`. We don't ship one, so edit the one Reloaded-II made
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

### Make a desktop shortcut that starts the game with the mod

Always start the game from this shortcut. These steps are P4G Access's method, which we expect to work the same here.
`[NEEDS VERIFICATION]` All of this section.

1. Open Reloaded-II.
2. Press `Tab` to reach the application list and open the game's entry.
3. Use NVDA's review cursor (`NVDA+numpad` keys, or `Shift+NVDA+arrows` on a laptop) to reach `Create Shortcut` on the
   game's page.
4. Move the mouse to the review cursor with `NVDA+Shift+M`, then click with `NVDA+numpad slash`, or `NVDA+[` on a
   laptop.
5. A shortcut appears on the desktop, named after the game with `(Reloaded)` in it. The exact name, and whether NVDA
   announces the click, are not checked.

P4G Access's own guide says not to start the game from Steam, because it often fails to load some mods. Another
instruction from its author says Steam works while Reloaded-II is running, but the shortcut is more reliable. Which
holds for Metaphor is `[NEEDS VERIFICATION]`; use the shortcut.

### Launch

1. Close the game and Reloaded-II if they are running.
2. Open the `(Reloaded)` shortcut on the desktop. The game starts. You can close Reloaded-II's window if it shows up.
   `[NEEDS VERIFICATION]` Whether Reloaded-II stays open.

## Credits

- The desktop-shortcut steps, the NVDA review-cursor steps and the idea of a copy-in app config come from
  [Persona-4-Golden-Access](https://github.com/AquaRose7/Persona-4-Golden-Access) by AquaRose7 (GPL-3.0), at `dcff255`
  (v2.2.6). No code is copied.
- Listing every key, with when it works, comes from the [AccessMods](https://github.com/AccessMods/PhoenixWrightTrilogy)
  organisation (MIT): blind players of its mods hit keys the README left out. Pinning the loader version follows its
  CONTRIBUTING.

More in [`CREDITS.md`](../../../CREDITS.md).
