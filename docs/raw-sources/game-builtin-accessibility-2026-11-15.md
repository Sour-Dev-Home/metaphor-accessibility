# Metaphor: ReFantazio built-in accessibility (official pages, patch notes, reviews)

- **Source:** official store pages, patch notes and reviews:
  - Steam store API: `https://store.steampowered.com/api/appdetails?appids=2679460&l=english`
  - Steam announcements API: `https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=2679460&count=100&maxlength=0&feeds=steam_community_announcements`
  - PlayStation Store: <https://store.playstation.com/en-us/concept/10001029> (read through a fetch-and-summarise tool, so
    its quotes are as returned by that tool)
  - Can I Play That? review, Mike Matlock, 2025-02-24:
    <https://caniplaythat.com/2025/02/24/metaphor-refantazio-accessibility-review/> (same tool)
  - Atlus news: <https://atlus.com/metaphor-refantazio-latam-spanish-subtitles-now-available/>
  - PC Gamer interview headline (body not retrieved):
    <https://pcgamer.com/games/rpg/persona-and-metaphor-refantazios-ui-designer-is-open-to-accessibility-options-for-players-who-find-the-stylish-menus-overstimulating-that-is-something-we-understand-well-need-to-work-on-and-provide-in-the-future>
- **Saved:** 2026-11-15
- **Game build:** none for the store pages and reviews. Patch notes name their own versions (1.03 to 1.0.13.0).

## Finding

No official source or review found mentions text-to-speech, menu narration, screen reader support, text size, high
contrast or colour-blind options for any platform. A built-in TTS or narration option in the PC version: not found.
Not checked in the game's own settings menu `[NEEDS VERIFICATION]`.

## Steam (PC)

`categories` from the store API includes these accessibility-related entries:

> "Camera Comfort", "Adjustable Difficulty", "Keyboard Only Option", "Playable without Timed Input", "Stereo Sound",
> "Surround Sound"

Steam's "Narrated Game Menus" and the text-size and colour categories are absent. Languages:

> English*, French, Italian, German, Spanish - Spain, Japanese*, Korean, Russian, Simplified Chinese, Traditional
> Chinese, Portuguese - Brazil, Spanish - Latin America ... *languages with full audio support

So voiced dialogue exists only in English and Japanese; all other text is unvoiced.

## PlayStation Store

17 features listed: "Visual Comfort (Basic)", "Volume Controls", "Subtitles (Advanced)", "Captions (Advanced)", stick
sensitivity/inversion, "Playable without Button Holds / Rapid Button Presses / Simultaneous Presses / Motion Controls /
Touch Controls / Controller Vibration / Adaptive Trigger Effect", "Adjustable Difficulty (Basic)", "Tutorial Reminders",
"Game Pausing", "Manual Saving". No text-to-speech or screen reader entry.

## Patch notes (Steam announcements)

- 1.03 (2024-10-18): "Anti-aliasing feature implemented", "Adjustments made to the Camera Shake Off feature",
  "Adjustments made to audio volume balance".
- 1.09 (2024-11-01): "New ON/OFF toggle setting for motion blur animation when dashing. *Dash effects setting can be
  changed from SETTINGS > System Setting > Dash Effects." Also "Improved support for AZERTY keyboards."
- 1.0.11 (2025-01-15): "Now able to change party formation and configuration in Main Menu > EQUIPMENT", "Now able to
  cycle through item categories in Main Menu > ITEM".
- 1.0.13.0 (2025-05-22): "LATAM Spanish subtitles are now available".
- Security update (2025-10-28): "We released a version update regarding Unity. There are no changes made to the game
  content." What Unity component this refers to is unknown `[NEEDS VERIFICATION]`. On the installed PC copy
  (2026-11-15, build not hashed) the game folder's top level holds `libcurl-x64.dll`, `PhysX*_64.dll`,
  `steam_api64.dll` and a `bin` folder, and no `UnityPlayer.dll`.

None of these adds speech or narration.

## Can I Play That? (2025-02-24)

General review, no disability lens stated. Quoted (via the fetch tool):

> Metaphor doesn't have subtitle sizing options, but there is text scrolling adjustment settings.

> there aren't any colorblind options or high contrast mode in the game.

Settings it names include subtitles (on by default), a contrast/brightness slider, text scrolling adjustment,
auto-scrolling, separate BGM/SFX/voice/master volume, remappable keyboard and mouse (turn-based battle commands
excluded), dash hold/tap, camera shake toggle, and "full mouse support for menus". It does not mention TTS, menu
narration or play by blind players.

## Developer statement

The PC Gamer headline quotes the UI designer: "That is something we understand we'll need to work on and provide in the
future", about options for players who find the menus overstimulating. Article body, date and author not retrieved
`[NEEDS VERIFICATION]`.
