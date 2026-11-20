# A voice clip reads its line only in the event, bond, battle-event, dungeon, battle-support and narration families

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's data files from `base.cpk` offline: the message files and the sound archives' cue
  tables. No ADR 0002 row covers data files, which no protection guards; the maintainer approved this survey on
  2026-11-20 (#551). The six sightings come from our own Reloaded-II mod's message log.
- **Sources:** none, see Steps

For #551: of the 37,040 messages with a `<VOICE "…">` tag in the English message files, the clip speaks the whole line
only when its name starts `v_e<n>`, `v_f<n>`, `v_BE_`, `v_du_`, `v_br_` or `v_tr_nar`. Every other family (`v_co_`,
`v_en_`, `v_fm`, `v_tr_travel_voice`) is a short generic clip reused across many lines, so its text has to be read.
The brief's candidate rule ("`v_co_` means generic") is right about `v_co_` but leaves out `v_en_` and `v_fm`, which
behave the same way.

## Evidence

The full-reading families sit in the per-chapter event archives (`EN/sound/event/*.acb`) or are named after their
message; the generic ones sit in `EN/sound/partvoice.acb` and are shared between many texts. A cue's length (the
`Length` column of the archive's `CueTable`, in ms) divided by the word count of its text is the proxy for "the clip
reads the line": the full-reading families run 220-570 ms per word (5th to 90th percentile), the generic ones have
medians of 54-100 ms per word.

| Family | Messages | Clips | Clips on more than one text | Named after the message | Median ms per word | Below 150 ms/word | Context (message folder) | Sample text |
|---|---|---|---|---|---|---|---|---|
| `v_co_<n>_c<n>` | 15,119 | 2,831 | 1,718 | 0 | 54 | 13,506 | `event/` 8,434, `field/` 6,158, `report/` 379 | "Let's take a break." |
| `v_co_Dpv_…` | 2,795 | 602 | 340 | 7 | 58 | 2,487 | `field/` 1,757, `event/` 669, `report/` 345 | "People are just awful..." |
| `v_co_` other (`v_co_<n>_m<n>`, `voicetube`) | 1,322 | 416 | 229 | 0 | 60 | 1,206 | `field/` 1,161 (shops, NPCs) | "Come to buy a weapon…" |
| `v_en_` | 5,096 | 780 | 591 | 0 | 84 | 4,001 | `field/` 5,017 (Local Chatter, crowds) | "Hey, Mr. Driver, how long until we depart?" |
| `v_fm` | 420 | 309 | 80 | 0 | 61 | 344 | `event/follower/` 404 (minor characters) | "That's... That's a lie!" |
| `v_tr_travel_voice` | 315 | 94 | 50 | 0 | 100 | 222 | `ui/Townmap.msg` 289 | "That's the city of Martira." |
| `v_tr_nar` | 329 | 329 | 0 | 0 | 406 | 0 | `ui/Narration_Happening.msg` (More narrates) | "The party sets foot in the tower…" |
| `v_e<n>_…` | 7,895 | 7,886 | 1 | 7,886 | 376 | 2 | `event/` 7,893 (story cutscenes) | "By war, by conquest, by right…" |
| `v_f<n>_…` | 1,254 | 1,253 | 0 | 1,252 | 365 | 0 | `event/follower/` (bond events) | "I can stay by your side...?" |
| `v_BE_…` | 514 | 514 | 0 | 514 | 387 | 0 | `battle/event/` | "We'll just have to make it work!" |
| `v_du_Dpv_…` | 1,106 | 1,105 | 1 | 1,105 | 351 | 0 | `report/Dungeon_Common.msg` (party remarks) | "Want to rest a bit?" |
| `v_br_…` | 875 | 874 | 0 | 874 | 362 | 0 | `report/Support_Battle_*.msg` (navigator) | "Careful, there's a lot of 'em!" |

"Named after the message" means the clip with its `v_`, family and speaker suffix removed equals the label without
`MES_` (`MES_e00_002_001_001` → `v_e00_002_001_001_c051`; `Dpv_PC_Eupha_faint_01` → `v_du_Dpv_PC_Eupha_faint_01_c006`).
Each message carries one `<VOICE>` tag. The 6 sightings in #551 all fall in generic families: 380-737 ms for 2-17 words,
on clips shared by 2-89 different texts (`v_co_1090_c001` by 89). The two Scheming Man controls are 5,234 ms for 14
words and 4,654 ms for 10.

### Rules compared

Proxy for "the clip reads the line": cue length ≥ 150 ms per word. 488 messages have no cue length (see exceptions).

| Rule | Name only, clip short (text lost) | Text read, clip long enough (possibly said twice) |
|---|---|---|
| A: `v_co_` reads the text, anything else says the name | 4,569 | 1,967 |
| B: a clip named after its message says the name, anything else reads the text | 3 | 3,556 |
| C: `v_e<n>`, `v_f<n>`, `v_BE_`, `v_du_`, `v_br_`, `v_tr_nar` say the name, anything else reads the text | 2 | 3,222 |

Rule C is recommended: it needs only the clip name, which the announcer already has, while B needs the message label.
Its "said twice" column is mostly short generic-family lines whose bark is about as long as the text (`v_co_1340_c001`,
836 ms, "Well then..."); reading those costs a repeat, never a lost line.

### Exceptions

- **Named, but short (rule C's 2):** `MES_e08_070_001_039` (1,435 ms, 10 words) and `MES_e12_002_001_002` (579 ms,
  "Are you all right?"). Both are within fast speech; likely full readings `[NEEDS VERIFICATION]`.
- **`v_co_` named after its message:** 7 lines, Gallica's cat remarks (`MES_Dpv_PC_Gallica_cat_01`…`_05`) and a guard
  in D07 (`Dpv_D7_npc02_07`/`_08`). Short and probably spoken in full; rule C reads them anyway.
- **A generic clip on a line it could be speaking:** 3,222 `v_co_`/`v_en_`/`v_fm`/`v_tr_travel_voice` messages
  (1,967 of them `v_co_`) pass the 150 ms/word proxy; the 14 sampled were 2-9 word lines. "Hey, look!"
  (`v_co_Dpv_PC_Gallica_talk_31_c011`, 380 ms) passes it, yet the maintainer heard a sound effect, so the proxy can't
  settle short lines either way. The game-over lines
  (`MES_GAMEOVER_000_000`, `v_co_51300_c051`, 1,824 ms, "Will your tale end here?") are on single-use clips and may be
  full readings `[NEEDS VERIFICATION]`.
- **A full-reading clip on another message:** 9 `v_e` and 2 `v_f` messages reuse an event clip under a different label
  (`MEScommon_AKADEMEIA_LASTBATTLE_BEFORE_005` → `v_e14_026_001_026_c051`). Rule C says the name; B would read the text.
- **No cue in the English archives:** 288 messages (282 clips). 244 are `v_br_` lines whose text is "Placeholder"; the
  other 44 are `v_e` (19), `v_du_` (12), `v_BE_` (7) and `v_co_` (5) lines, such as More's "So ends your story…" in
  `e03_005_003`. If those clips play nothing, rule C loses their text. Whether they live in another archive is
  `[NEEDS VERIFICATION]`.

### Not checked

No clip was listened to or transcribed: the HCA stream checked in `partvoice.awb` is encrypted (`ciph` type 56, AWB subkey
nonzero), and decoding them needs a key; per ground rule 5 the survey stopped there. Every claim about what a clip says
rests on the cue length, the maintainer's by-ear sightings in #551, or is `[NEEDS VERIFICATION]`. Only the English
message files and the English voice archives were read.

## Steps

Python 3.12.10, scripts written for this survey and kept outside the repository (not committed):

1. Read `base.cpk`'s `@UTF` header and TOC (CRI CPK format; the TOC is unencrypted on this build), and extracted every
   `EN/message/**/*.msg` (2,673 files, CRILAYLA-compressed, plain text after decompression), `EN/sound/partvoice.acb`
   and `EN/sound/event/*.acb` to a scratch folder outside the repository.
2. Parsed each `@<label> { … }` message for `<NS>`, `<VOICE "clip">` and the text with tags removed; counted words as
   runs of `[A-Za-z0-9']`.
3. From each ACB's `CueNameTable` (name → `CueIndex`) and `CueTable` (`Length`), took every cue's length; joined on the
   clip name.
4. Grouped by clip prefix as in the table, counted distinct texts per clip, and scored rules A-C against the
   150 ms/word proxy.
5. Read the first HCA header in `EN/sound/partvoice.awb` (AFS2) to check for encryption; deleted the scratch copies.
