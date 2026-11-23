# Changelog

What changed for players, newest first. It lists only what you would notice in the game or the installer. How the mod
is built is in [the contributor docs](docs/contributors/README.md). The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

Lines tagged `[NEEDS VERIFICATION by ear]` have not been heard in the game with a screen reader yet. The maintainer
checks them in the first game session and removes the tag.

New entries wait as files in [changelog.d](changelog.d/README.md) until a release joins them under a version below.

## Unreleased

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
