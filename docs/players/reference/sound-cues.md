# Sound cues

The mod plays a few short sounds of its own, so you know what happened before the screen reader finishes a sentence.
Most last less than a fifth of a second, and all play alongside your screen reader's speech. This page lists every
sound and what it means.

`[NEEDS VERIFICATION by ear]` The sounds have not been heard in the game yet.

## The sounds

| Sound | What it means | What it sounds like |
|---|---|---|
| Ally hit | Someone in your party was hit. It plays just before the damage number is read. | Two low notes going down, like "uh-oh". |
| Enemy hit | An enemy was hit. It plays just before the damage number is read. | Two high notes going up, quicker and brighter. |
| Placed hit (testing: `battleHitNumbers`) | Which enemy or party member a hit was on, when hits say only their number. | The ally or enemy hit sound, from the left for the first enemy or party member through to the right for the last. |
| Guidance beep | The way to what you asked the mod to guide you to in the field ([keys](keys-and-speech.md#in-the-field)). | A short beep that repeats: from the side the thing is on, high ahead and low behind, about once a second far away and five times a second close. |
| Sonar (testing: `sonar`) | A person, object or exit near you in the field. | One short tone at a time, cycling through what's nearby, nearest first (`field.sonarPings` in `limits` plays the nearest few in a row instead): middle for a person, high for an object, low for an exit, from the side it is on screen and louder when nearer. It waits while the guidance beep guides or a route ping sounds. Placeholder tones until the sounds are chosen. |
| Wall tones (testing: `wallTones`) | A wall, or the edge of where you can walk, near you in the field. | A low steady tone for each side with a wall within about three steps, louder as it nears: highest ahead, lower left and right, lowest behind. Softer while the guidance beep or a route ping plays. |
| Route ping (testing: `routePings`) | The way to the place you just stepped to in the nearby list. | One ping from the way to go: two quick high notes when it is straight, two rising notes from the next corner when the way bends, one low note when there is no path. |
| Mark | You pressed the mark key, F8, and the mod wrote a marker to its report log. | Two short high clicks on the same note. |

A simple way to remember them: low and falling is bad news for you, high and rising is good news.

## Turn the sounds off or change their volume

1. Find the mod's settings folder: `User\Mods\metaphor.accessibility` inside your Reloaded-II folder.
   `[NEEDS VERIFICATION]` The folder is checked in the game later.
2. In it, create or open the text file `settings.json`.
3. Put in it, keeping any other settings already there, with a comma between each:

   ```json
   { "soundCues": true, "soundCueVolume": 50 }
   ```

4. To turn the sounds off, change `true` to `false`. To change how loud they are, change `50` to a whole number from 0,
   silent, to 100, the loudest. Save the file.

The change applies at once, and the mod says "Sound cues: on" or "Sound cues: off", or "Sound cue volume:" and the new
number. You don't need to restart the game.

If a value can't be used, such as a volume of 120 or the word "off" instead of `false`, the mod uses the default:
sounds on, at volume 50.

Each sound can also be turned off or made quieter on its own: see the sounds in
[the settings reference](settings.md#sounds).
