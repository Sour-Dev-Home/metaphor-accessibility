# Settings

Every setting the mod reads from its settings file, what each does, and its default. You never need to change any of
them: with no file, everything is on. They are there so a player with some sight can turn off the parts they don't
need.

## The settings file

1. Find the mod's settings folder: `User\Mods\metaphor.accessibility` inside your Reloaded-II folder.
   `[NEEDS VERIFICATION]` The folder is checked in the game later.
2. In it, create or open the text file `settings.json`.
3. Write the settings you want to change, and leave out the rest. Save the file.

A change applies at once, while the game runs, and the mod says what changed, such as "Menus: off." or "Keys
changed." You don't need to restart the game.

A value the mod can't use, such as the word "off" where `false` belongs, is ignored: that setting keeps its default,
and the mod's log says which one. Settings the mod doesn't know are ignored too.

A file that turns off menu speech and the nearby list, and moves the repeat key to Q, reads:

```json
{
  "speech": { "menus": false },
  "field": { "nearby": false },
  "keys": { "dialogue": { "repeatKey": "Q" } }
}
```

## General

| Setting | What it does | Default |
|---|---|---|
| `voicedLines` | What is said for lines the actors speak: `auto`, `nameOnly`, `fullLine` or `nothing`. See [change how voiced lines are read](../how-to/change-how-voiced-lines-are-read.md). | `auto` |
| `checkForUpdates` | Whether the mod asks GitHub once per launch for a newer release. See [turn off the update check](../how-to/turn-off-the-update-check.md). | `true` |
| `soundCues` | Whether the mod plays its sound cues at all. | `true` |
| `soundCueVolume` | How loud the sound cues are, a whole number from 0, silent, to 100. | `50` |
| `welcomed` | Whether the welcome has been said. The mod sets it to `true` itself after the first launch; set it to `false` to hear the welcome again next time. | `false` |

## Features still being tested

A feature that needs a by-ear check can ship while it is still being tested. Its default is shown as `testing, off`
below: in a normal install it stays off until you set it to `true`, and it turns on by default in the release after
the check passes. Release builds never turn it on for you. For a contributor, a by-ear session
(`scripts/dev/session.ps1 deploy` or `run`) turns every testing feature on for that session only and never edits your
`settings.json`; a value you set in the file still wins. Passing the check is a one-line pull request that removes
`Testing = true` from the feature's `[SettingKey]` in `Feature.cs` and the `testing, off` from its row here.

## Speech

Each is `true` to speak or `false` to stay quiet, inside a `speech` object: `{ "speech": { "menus": false } }`.

| Setting | What it speaks | Default |
|---|---|---|
| `dialogue` | Dialogue lines and reply choices. Off also leaves the repeat and reader keys to the game in dialogue. | `true` |
| `menus` | The title menu, the main menu, the quick menu, Settings, the difficulty list, yes or no questions and any other menu. Off also leaves the details key to the game. | `true` |
| `battleHits` | Each battle hit as it lands, the battle status key and the enemy HP key. Off also leaves those keys to the game. | `true` |
| `battleOutcomes` | "Knocked out", "defeated" or "revived" after a battle hit's number. Needs `battleHits`. Still being tested. | testing, off |
| `battleLists` | Battle's skill and item lists, row by row. Separate from `menus`. | `true` |
| `itemMenu` | The Item screen's rows and its party-member list. Still being tested. | testing, off |
| `calendar` | The Calendar's day grid. Still being tested. | testing, off |
| `battleBanners` | The short banner during a battle turn: a boss warning, a stat change, "Not enough MP!". Still being tested. | testing, off |
| `saveSlots` | The save and load screens' slots. Still being tested. | testing, off |
| `learnedSettingValues` | The System rows whose setting the mod has not pinned saying their value on change, learned as you change them. Needs `menus`. Still being tested. | testing, off |
| `nameEntry` | The name entry screen: its title, each letter you type, and when a name is refused. | `true` |
| `equipmentMenu` | The Equipment screen's slots and gear. Still being tested. | testing, off |
| `travelMap` | Each destination on the travel map. | `true` |
| `systemMenu` | The System menu's rows. Still being tested. | testing, off |
| `shopMenu` | The shop's top menu and its Buy and Sell lists. Still being tested. | testing, off |
| `placeNames` | The place name the game shows as you arrive somewhere. | `true` |
| `updateNotice` | The title screen's notice of a newer version. The request itself is `checkForUpdates`. | `true` |
| `tutorials` | Tutorial pop-ups: the title, the page count and each page. Off also leaves the repeat key to repeat dialogue, not the pop-up's page. | `true` |
| `termNames` | The skill and condition names a line's tags stand for. Off says the tag's stand-in. Still being tested. | testing, off |
| `choiceCount` | A reply choice saying how many options there are, with each option said as the focus moves, rather than every option up front. Needs `dialogue`. Still being tested. | testing, off |
| `choiceDelay` | A reply choice's options waiting until they are on screen, not said straight after the prompt. Needs `dialogue`. Still being tested. | testing, off |
| `windowCloseStop` | A closed dialogue window stopping the line still being spoken. Needs `dialogue`. Still being tested. | testing, off |
| `welcome` | The welcome on the title screen the first time the game starts with the mod: the mod's version and the field keys. | `true` |

## Field

Inside a `field` object, `true` or `false`: `{ "field": { "whereAmI": false } }`. Off leaves that feature's keys to
the game.

| Setting | What it does | Default |
|---|---|---|
| `nearby` | The nearby list: N, or LB with Down and Up on the D-pad. | `true` |
| `whereAmI` | "Where am I": L, or LB with Left on the D-pad. | `true` |
| `guidance` | The guidance beep toward a nearby thing: G, or LB with Right on the D-pad. | `true` |
| `guidancePause` | The beep pausing, not ending, while the game has control (a cutscene or a line), and coming back once free roam has held. Needs `guidance`. Still being tested. | testing, off |
| `reach` | The callout when a door or chest comes into reach: "Door, in reach". | `true` |

## Report

Inside a `report` object, `true` or `false`: `{ "report": { "silentScreens": false } }`. Both write only to
`report-log.txt` in the mod's folder; nothing is sent anywhere. See [report a problem](../how-to/install-the-mod.md#report-a-problem).

| Setting | What it does | Default |
|---|---|---|
| `markKey` | The mark key, F8: writes a numbered marker with the time to the report log and plays the mark sound. Off leaves F8 to the game. | `true` |
| `silentScreens` | Lists in the report log each screen and each move in a menu that the mod said nothing for. | `true` |

## Sounds

Each sound cue has its own setting inside a `sounds` object, with `on`, `true` or `false`, and `volume`, a whole number
from 0 to 100 that is a share of `soundCueVolume`: 50 plays it at half the sound cue volume.
[Sound cues](sound-cues.md) says what each sound means.

```json
{ "sounds": { "allyHit": { "on": false }, "enemyHit": { "volume": 50 } } }
```

| Setting | The sound | Default |
|---|---|---|
| `allyHit` | Someone in your party was hit. | on, volume 100 |
| `enemyHit` | An enemy was hit. | on, volume 100 |
| `mark` | The mark key wrote a marker. | on, volume 100 |

## Keys

Each key and button the mod uses can be moved, inside a `keys` object, then the feature's object. A key is a letter,
a digit, or F1 to F12, written in quotes: `"Q"`, `"7"`, `"F5"`. Pad buttons are their names, joined by a plus sign when
held together: `"LB+RB"`. `"none"` leaves an action without a key or button.

The button names are A, B, X, Y, LB, RB, LT, RT, Start, Back, LS and RS for pressing the left or right stick, and
DpadUp, DpadDown, DpadLeft and DpadRight.

```json
{ "keys": { "dialogue": { "repeatKey": "Q", "readerButtons": "LT+RT" } } }
```

| Setting | What it does | Default |
|---|---|---|
| `dialogue.repeatKey` | Repeats the last dialogue line. | `"R"` |
| `dialogue.repeatButtons` | The same on the pad. | `"X"` |
| `dialogue.readerKey` | Turns the dialogue reader on or off, in dialogue and in menus. | `"T"` |
| `dialogue.readerButtons` | The same on the pad. | `"LB+RB"` |
| `menus.detailsKey` | Reads the rest of the focused menu item's details. | `"F"` |
| `menus.detailsButtons` | The same on the pad. | `"RS"` |
| `battle.statusKey` | In a battle, says each party member's HP and the enemies hit so far. | `"H"` |
| `battle.statusButtons` | The same on the pad. | `"LS"` |
| `battle.enemyHpKey` | In a battle, says the HP of the enemies hit so far. Unbound until free keys are chosen (#767): F and RS belong to the game's melee and auto-battle. | `"none"` |
| `battle.enemyHpButtons` | The same on the pad. | `"none"` |
| `field.nearbyKey` | Speaks the whole nearby list. | `"N"` |
| `field.placeKey` | Says where you are. | `"L"` |
| `field.holdButtons` | Held for the field's pad chords. | `"LB"` |
| `field.nextButton` | With the held button: the next thing on the nearby list. | `"DpadDown"` |
| `field.previousButton` | With the held button: the thing before. | `"DpadUp"` |
| `field.placeButton` | With the held button: where you are. | `"DpadLeft"` |
| `field.guideKey` | Starts or stops the guidance beep. | `"G"` |
| `field.guideButton` | With the held button: starts or stops the guidance beep. | `"DpadRight"` |
| `field.nextKey` | The next thing on the nearby list, from the keyboard. | `"K"` |
| `field.previousKey` | The thing before, from the keyboard. | `"J"` |
| `report.markKey` | Writes a marker to the report log. Keyboard only for now. | `"F8"` |

A key the game also uses still reaches the game whenever the mod isn't using it, so choose one the game leaves free
where you'll press it.
