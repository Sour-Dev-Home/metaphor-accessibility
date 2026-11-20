# Metaphor: ReFantazio, whole-game map from text sources

- **Source:** web pages listed under each heading, read through a summarising fetch tool (wording is paraphrased; no game
  text copied; structure only, no story detail).
- **Saved:** 2026-11-20
- **Game build:** none; publicly described structure of the PC release (2024-10-11), no build checked.

Purpose: plan chapter-by-chapter releases (ADR 0016, `docs/adr/0016-release-by-chapter.md`). Builds on
`metaphor-chapter-structure-2026-11-20.md`. Guides disagree on some order and dates; anything not backed by a cited
page, or where sources conflict, is [NEEDS VERIFICATION]. The maintainer's play-through is the real check.

## Arcs in order, with main dungeons

Source A, Gamer Guides walkthrough contents (location split, not official chapters):
https://gamerguides.com/metaphor-refantazio/guide/walkthrough

| # | Arc (Source A name) | Dungeons and areas listed | Bosses listed |
|---|---|---|---|
| 1 | Prologue | Tradia Desert, Royal Capital (Grand Trad), Northern Border Fort, Nord Mines | 2 |
| 2 | Grand Trad | Belega Corridor | 4 |
| 3 | Martira | Grand Trad Mausoleum, Man-Eater's Grotto, Abandoned Tomb, Kriegante Castle | 4 |
| 4 | Port Brilehaven | Port Brilehaven, Tomb of Lament, Forsaken Tower, Charadrius (the ship) | 3 |
| 5 | Virga Island | Dragon Temple, Drakongrace Shinjuku, Spire of Blind Faith | 5 |
| 6 | Altabury Heights | Altabury Heights, Opera House | 2 |
| 7 | Finale | Ancient Eldan Sanctum, Skybound Avatar, Tyrant's Star | 4 |

Source B (a list of dungeons by unlock date, 9 main plus 16 side):
https://selphie1999gaming.com/game-guides/metaphor-refantazio/metaphor-refantazio-list-of-all-dungeons/
Main story dungeons in order: Northern Border Fort, Nord Mines, Regalith Grand Cathedral, Kriegante Castle, Giant
Sandworm's Belly, Giant Sandworm's Nest, Skyrunner Charadrius, Dragon Temple, Montario Opera House. Side dungeons
include Belega Corridor, Tradia Desert, Abandoned Tomb, Man-Eater's Grotto, Forsaken Tower, Tomb of Lament, Gracia
Forest, Spire of Blind Faith, Scoundrel's Hold, Land of Ceremony, Orbwise Path, Tower of Insolence, Everfrost Forest,
Mt. Vulkano (Rank 6 bond with a follower). Source B's unlock dates are in-game calendar dates.

Source C, a day-by-day schedule (in-game dates), https://www.powerpyx.com/metaphor-refantazio-walkthrough/ , gives
this rough span: opening 06/05 to 06/10; Grand Trad 06/11 to 06/16; desert and early travel to 07/05; Martira from
06/29; Brilehaven 07/20 to 08/18; Virga Island and beyond 08/18 to 09/05; Altabury Heights 09/09 to 09/24; final
stretch 09/26 to 10/14 (Skybound Avatar); ending from 10/26 (Tyrant's Star), then Clear Save Data and New Game+.
Source C's day dates and Source A's arc boundaries do not line up exactly (Source C places Martira before Brilehaven
and the Sandworm between them). Exact boundaries: [NEEDS VERIFICATION].

Source D, Wikipedia, describes the shape without names: an opening in the capital, a Royal Tournament made of tasks
each sending the party to a new setting, a middle stretch in far regions, a return to the capital for a climax, then a
finale in a separate realm and epilogue, with bad endings branching from choices at the final confrontation:
https://en.wikipedia.org/wiki/Metaphor:_ReFantazio

## Systems and where they appear

Timing is from Source C (in-game dates) unless noted; "not found" means no fetched page said.

| System | Introduced or unlocked | Our issues |
|---|---|---|
| Field exploration, dash, dodge, Fae's Sight | Prologue (chapter structure note) | #144, #176 to #186 |
| Field real-time combat, ambush advantage (Source D) | First dungeon | #186 |
| Turn-based Press Turn battle, Fast & Squad (Source D) | First boss | #143, #163, #251 to #254 |
| Synthesis | Needed by the 06/11 boss (Source C); synergy per Source D | #260 |
| Formation, Retry, Escape, items (Source D lists switch formation, items, restart, escape) | Unlock timing not found | #259, #262, #256, #165 |
| Party swap with the reserve | Not found for battle; Source C has choose-a-companion-for-a-day from 08/16 | #258 |
| Auto Battle and Tactics | Not found | #257, #218 |
| Assist, Pass | Not found | #263, #261 |
| Analyse, statuses and affinities, results | Not found | #255, #164, #166 |
| Archetypes, ranks, inheritance (Source D) | First bond after first boss | #145, #160, #219, #220 |
| Followers and bonds | First bond after first boss; more via quests | #147, #198 to #200 |
| Royal Virtues (five stats; some bonds need levels) | Not found for first rise; gained via activities, books, speeches | #146 area, none specific |
| Calendar, deadlines, free time, travel time | After first boss; deadlines are game over (Source D) | #146, #190 to #193, #200 |
| Shops, money | First town | #147, #195 to #197 |
| Requests and bounties (Recruitment Centers) | Not found; early towns | #194 |
| Magla hollows (safe rooms) | First dungeons (Source D); exact first one [NEEDS VERIFICATION] | none |
| Academy (Akademia) | Quests from 06/12; study runs to 09/22; mask and vessel crafting from 08/29 | none |
| Gauntlet Runner (camp: stove, pantry, bookshelf) | From 06/24; route travel from 06/29 | none |
| Cooking | Inn from 06/13; Gauntlet Runner recipes from 06/24 | none |
| Fishing | Gauntlet Runner from 10/03 (Source C); the prologue guide links a fishing page [NEEDS VERIFICATION] | none |
| Debates (podiums) | From 07/09 | none |
| Coliseum, rival Gauntlet Runner fights | After the Gauntlet Runner arrives | none |
| Tutorial pop-ups | Throughout | #118 |
| Timed or spatial puzzles, platforming and stealth in dungeons (Source D) | Dungeons | #178, #179 |

Prerequisites: no issue yet for Royal Virtues, Magla hollows, Academy, the Gauntlet Runner camp, cooking, fishing,
debates or the Coliseum.

## Missable content (Source C)

Missable items: a Gold Beetle at the camp (06/05), the Nord Mines dragon weapon (06/06, one chance), a book in
Brilehaven (07/23), a Gold Beetle in the Plaza of Light (09/24), and the Sandworm Nest beetle (no return after 07/03).
Rotating shop stock (two stalls). Story deadlines that end the game if missed (Source D). Whether each is truly
missable: [NEEDS VERIFICATION].

## Proposal (not a decision): slices after Chapter 1

Chapter 1 stays as milestone 1 (opening through the first boss and the first bond). Roughly demo-sized pieces
(the free demo is about five hours and covers the first four dungeons; see the chapter structure note):

- **Slice 2, "Grand Trad": the rest of the demo-sized opening.** First free days, the calendar and followers loop,
  shops, requests, the Belega Corridor, Academy start, first cooking. Introduces the day loop that every later
  chapter reuses. Ends before the long travel.
- **Slice 3, "Martira and the road":** Gauntlet Runner camp, route travel, Kriegante Castle, the Sandworm, early
  debates and Royal Virtues use, party swap if it appears. Ends at the arrival in Brilehaven.
- **Later slices (rough):** Brilehaven; Virga Island; Altabury Heights; Finale including endings and New Game+.
  Optional content (Coliseum, fishing, side dungeons) goes in the slice where it unlocks.

The order and size are a guess from the sources above, to be re-cut after Chapter 1 is played.
