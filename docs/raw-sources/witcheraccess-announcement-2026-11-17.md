# WitcherAccess (The Witcher 3): what a full screen-reader mod covers, as prior art for field navigation (#144)

- **Source:** the author's release announcement for WitcherAccess 1.0, a screen-reader mod for The Witcher 3: Wild
  Hunt published on Nexus Mods, shared with this project by the maintainer on 2026-11-17. A web search the same day
  found no listing to link (`[NEEDS VERIFICATION]`: the Nexus Mods URL). Only the feature list is kept, paraphrased.
- **Saved:** 2026-11-17.
- **Game build:** none; another game. This records design prior art, not facts about Metaphor.

## What it covers

Per the announcement, after six months of work and more than four months of testing with dozens of players, version
1.0 covers the base game and both expansions:

- Every menu, dialogue, choice, book and letter read aloud.
- An accessible world map, with fast travel.
- An object scanner listing what is around the player, with distance and direction.
- Navigation assistance in the style of The Last of Us, sound beacons, and automatic walking.
- Audio cues for doors, containers, enemies and incoming attacks.
- Audio description of cutscenes, with recorded voices in 17 languages.
- In-game settings: every audio cue can be turned on or off, and its volume adjusted.

## Against our plan (2026-11-17)

| WitcherAccess | Ours |
|---|---|
| Object scanner with distance and direction | #181 nearby list, #182 sonar |
| Navigation assistance, beacons, auto-walk | #183 guidance, #184 auto-walk |
| Cues for doors, containers, enemies | partly #185 wall tones, #186 engaging enemies |
| Incoming-attack cues | none yet; Metaphor has real-time field combat before turn-based battles |
| World map with fast travel | none yet; Metaphor's travel and Gauntlet Runner routes |
| Cutscene audio description | none yet |
| Per-cue on/off and volume | none yet; the settings file (#104) could grow into it |
| Long testing with many blind players | the alpha (#131) is the start of that |
