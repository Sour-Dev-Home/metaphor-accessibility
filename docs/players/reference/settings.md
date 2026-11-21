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

## Speech

Each is `true` to speak or `false` to stay quiet, inside a `speech` object: `{ "speech": { "menus": false } }`.

| Setting | What it speaks | Default |
|---|---|---|
| `dialogue` | Dialogue lines and reply choices. Off also leaves the repeat and reader keys to the game in dialogue. | `true` |
| `menus` | The title menu, the main menu, the quick menu, Settings, the difficulty list, yes or no questions and any other menu. Off also leaves the details key to the game. | `true` |
| `nameEntry` | The name entry screen: its title, each letter you type, and when a name is refused. | `true` |
| `travelMap` | Each destination on the travel map. | `true` |
| `placeNames` | The place name the game shows as you arrive somewhere. | `true` |
| `updateNotice` | The title screen's notice of a newer version. The request itself is `checkForUpdates`. | `true` |

## Field

Inside a `field` object, `true` or `false`: `{ "field": { "whereAmI": false } }`. Off leaves that feature's keys to
the game.

| Setting | What it does | Default |
|---|---|---|
| `nearby` | The nearby list: N, or LB with Down and Up on the D-pad. | `true` |
| `whereAmI` | "Where am I": L, or LB with Left on the D-pad. | `true` |

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
| `field.nearbyKey` | Speaks the whole nearby list. | `"N"` |
| `field.placeKey` | Says where you are. | `"L"` |
| `field.holdButtons` | Held for the field's pad chords. | `"LB"` |
| `field.nextButton` | With the held button: the next thing on the nearby list. | `"DpadDown"` |
| `field.previousButton` | With the held button: the thing before. | `"DpadUp"` |
| `field.placeButton` | With the held button: where you are. | `"DpadLeft"` |

A key the game also uses still reaches the game whenever the mod isn't using it, so choose one the game leaves free
where you'll press it.
