# Troubleshoot the mod

> **Draft for testers.** Nobody has checked these steps by ear with a screen reader yet. If one is wrong, say so in an
> issue.

Find your problem below. Nothing here needs a typed command: you open files and press keys. Each section ends with
where to report it.

## What the mod says when it loads

When the game starts, the mod speaks one of three messages once:

1. `Metaphor accessibility loaded`: the mod is running.
2. `This game version isn't supported yet; the mod is off`: your game build is one the mod doesn't know. See
   [The mod went quiet after a game update](#the-mod-went-quiet-after-a-game-update).
3. `Metaphor accessibility couldn't start; the mod is off`: any other failure. See
   [The mod did not load](#the-mod-did-not-load).

If you hear the first message, the install worked. Hearing nothing at all is the next section.

## No speech

You started the game and heard no message from the mod, or the mod stopped speaking.

1. Start the game from the desktop shortcut "Metaphor ReFantazio with accessibility", not from Steam. The
   install guide starts the game only from that shortcut.
2. Check that your screen reader is running before you start the game. The mod speaks through NVDA, JAWS or ZoomText,
   and through Windows' own voice when none of them is running.
3. Press `T` while a dialogue line is on screen. The mod may be switched off: it says "Dialogue reader on" or
   "Dialogue reader off". `T` works only while a dialogue window is open.
4. Run the installer again, as in [Install the mod](install-the-mod.md#install-with-the-spoken-installer). It ends
   with the install check, which says what it found and what to fix first. Each line starts with "Pass", "Problem" or
   "Not checked". It checks, in order: Reloaded-II is set up; the mod is in its mods folder; the game is added and the
   mod turned on for it; the game's version is one the mod knows; the speech library, `prism.dll`, is in place; how the
   mod last started. It changes no file or setting beyond what the installer does, and reads nothing over the network.
   The last line names the first thing to fix.
5. If the check finds nothing wrong and the mod is still silent, [report it](#report-it).

## The mod did not load

You heard "Metaphor accessibility couldn't start; the mod is off", or the game starts but the mod does nothing.

1. Run the installer again and read what the install check says, as in "No speech", step 4. Fix the first
   problem it names, then run the installer once more.
2. Look at the newest file in `%APPDATA%\Reloaded-Mod-Loader-II\Logs\` (press `Windows+R`, type that folder name, press
   `Enter`). It lists the mod by its ID, `metaphor.accessibility`, and says why it failed to start.
3. If Windows blocked the installer or its files, see
   [If Windows says the file is from the internet](install-the-mod.md#if-windows-says-the-file-is-from-the-internet).
4. [Report it](#report-it).

## The mod went quiet after a game update

The mod pins every patch to one game build. After a game update the build changes, and the mod then does nothing at all,
on purpose, rather than guess: the game runs as normal, with no speech from the mod.

1. If the game has just updated, this is the likely cause. The mod usually says "This game version isn't supported yet;
   the mod is off" when it loads. You may hear nothing if the game's update also changed what the mod needs for speech.
2. The install check says whether the game's version is one the mod knows (see "No speech", step 4).
3. Nothing you can change makes the mod work on the new build. It needs a new release. Check the
   [releases page](https://github.com/Sour-Dev-Home/metaphor-accessibility/releases), and when there is a newer
   version, run its installer. The title screen also says "A newer version of the mod is available:" when there is one,
   unless you turned the check off ([turn off the update check](turn-off-the-update-check.md)).
4. [Report it](#report-it) if no newer release exists, so we know a build needs checking.

## Report it

Use the Alpha feedback form on the project's issues page. Steps: [Report a problem](install-the-mod.md#report-a-problem).
It attaches `report-log.txt`, which the mod writes fresh each game start, with no game text and your user name
replaced, so copy it before you start the game again.
