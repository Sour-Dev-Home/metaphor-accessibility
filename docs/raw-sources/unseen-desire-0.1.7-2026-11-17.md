# Unseen Desire 0.1.7, a screen-reader mod for Persona 5 Royal: what it does and what we can take

- **Source:** the Unseen Desire 0.1.7 release zip by Haru (heavenay.com), read as data: its `README.txt`,
  `Unseen Desire - what to expect.txt`, `THIRD-PARTY.txt`, `LICENSE.txt`, the mod's `ModConfig.json`,
  `p5r.accessibility.deps.json`, `help_content.json`, the headers of its data files, and the bundled mods' `ModConfig.json`
  files. Nothing was run or decompiled. No public source repository: `ModConfig.json` has `"ProjectUrl": ""` and the README
  says "Its source code is available on request: ask through heavenay.com." A GitHub search on 2026-11-17 found only
  `vlad9293828/unseen-desire-preview`, created 2026-11-15 and empty, with no link to the author; it is not treated as the
  source. DRM: PCGamingWiki's `Persona_5_Royal` page source through its API, read 2026-11-17.
- **Saved:** 2026-11-17
- **Game build:** none. It targets Persona 5 Royal (Steam), not Metaphor: ReFantazio. Its byte patterns, offsets and
  file names do not carry over; only methods and design do. The release names no P5R build it was verified on.

Feeds #52, #143–#147, #176–#186, #211 and #232. Compare `persona-4-golden-access-2026-11-16.md`,
`arcana-v0.1.0-alpha.3-2026-11-16.md` and `field-navigation-accessibility-mods-2026-11-16.md`.

## 1. What it covers, by area

Everything here is what the release text says; how it is done in code cannot be seen (no source).

- **Menus.** Reads the camp menu, items, equipment, Personas, skills, stats, calendar, system menus, settings, shops,
  confidants, the Velvet Room and the phone's chats. Extra keys: I and K move "between the rows of a long screen", J and L
  "Read the current row one item at a time". Optional "menu and screen descriptions (what a screen looks like)". How it
  finds the active screen and the focus: not stated in the release [NEEDS VERIFICATION: needs the source].
- **Dialogue.** Lines and choices with the speaker; a "voice mode" (dialogue reader off) that leaves voiced lines to the
  actors and still reads unvoiced ones; I says who speaks, K the line; Ctrl+P repeats; Ctrl+brackets walk a speech
  history. Unvoiced lines come from `event_unvoiced.txt` and `palace_unvoiced.txt`, "Written by
  database\tools\voice_cues.py": scene lines whose voice cue is missing from the scene's voice banks, or whose clip
  "lasts under half of the line".
- **Cutscenes.** Hand-written descriptions per story scene (`E###_###.txt`, about 1,800 files) and per movie
  (`MOV###.txt`, `OPENING.txt`), timed to the movie's frame clock or anchored on the game's own lines (`@fragment =>
  text`). Each file opens with a comment saying how and when it was written. These are the author's prose, not game text.
- **Battle.** Commands, skill costs, targets and known weaknesses, damage, status, turn order, Hold Ups, negotiation,
  All-Out Attacks, results. Keys: O whose turn "with everything about them", semicolon and apostrophe cycle party members,
  period "The goals of the battle", F2 lists the battle keys "with your own keys".
- **Field navigation.** A scanner by category (Places, NPCs, Interactables, Exits...), "nearest first", with a key that
  repeats the selected entry "with its way there leg by leg and how far"; a guide beacon; "Where next" (picks the next
  place to explore); story spots in Bookmarks; auto walk where, since 0.1.6, "the game itself moves him, as if you held
  the stick", run by holding run and cancelled by any movement input; wall sounds and a bump sound; room and street names;
  a free map cursor (one metre or one room per step) and a cursor on the game's own map screen (by squares, Tab zooms);
  camera snap to north or the next main direction; optional 3D sound for headphones.
- **Walk map data.** `navmesh/` (28 MB): 841 `F###_###.tri` binaries with the magic `P5NV` (the mod's own format), plus
  text layers (door locks by flowscript bit, fences and the flag that lifts them, link doors, warps, climb and jump zones,
  fog triggers, Mementos exits and gates, moving collision), each headed by the script that generated it (`fences.py
  --write`, `walktest --door-reach` ...). They are derived from the game's field data and shipped, not extracted at install.
- **Settings.** F1 mod menu (LT+RT+Start on a pad): every setting, each sound's volume, and Help built from
  `help_content.json` (sections of `name`/`text` items, a `controller` section and `tips`). A first-run setup at the title
  screen asks about dialogue, subtitles, 3D sound, cutscene and menu descriptions, and the auto walk's limit. Own tutorials
  about the mod's keys appear "as you meet each thing".
- **Speech.** Prism (`prism.dll`, MPL-2.0) for screen readers and Windows voice; tested with NVDA only. A pad key stops
  speech for a long text.
- **Controllers.** Xbox pads as they are; PlayStation and Switch Pro pads "with no Steam Input and no DS4Windows", and a
  PlayStation pad "hears its own button names". Without a pad, "the mod shows the game one, so it can walk Joker by the
  stick".
- **Game changes it admits.** Mementos' Reaper after about 6 minutes instead of 3; added tutorials; the virtual pad above.
  "The mod never writes to your saves."
- **Packaging and install.** An installer (not in the zip) with two modes, with a visible Reloaded-II or a hidden one;
  states the download size, disk space and destinations "before anything is written"; fetches .NET 9 and the VC++
  runtime from Microsoft; Stable and Testing channels; delta updates that keep settings. The game files it reads are
  "extracted from your own copy of the game into the mod's GameData folder", with CriFsV2Lib; the manual zip extracts them
  on first start. A bug-report guide names the Reloaded log, warns it holds the Windows user name and the hero's name, and
  says how to zip one save slot.

## 2. Architecture (as far as the release shows)

- **Hooks and memory.** The mod's dependencies are `reloaded.sharedlib.hooks` (Reloaded.Hooks 1.9.0, Definitions 1.11.0)
  and `Reloaded.Memory.SigScan.ReloadedII` (1.2.14), so it signature-scans and hooks. The data files cite an internal
  `RE_FINDINGS` (§118, §125) and `database\frida\movie_watch.py`, so Frida traces were part of the research. The split
  between hooks and memory reads: not visible.
- **p5rpc.lib and file emulation.** `ModConfig.json` depends on `p5rpc.modloader` (2.9.4) and `p5rpc.lib` (1.1.0), but
  `p5r.accessibility.deps.json` references neither `p5rpc.lib.interfaces` nor any emulator interface; its only non-Reloaded
  packages are CriFsV2Lib 2.1.2, NAudio 2.2.1 and Reloaded.Memory 9.4.1. File replacement goes through Persona 5 Royal
  Essentials folders: `P5REssentials/CPK/...` holds an edited `FLDATDNGCAT.FTD` and four `SCRIPTTUTORIAL_SYSTEM_0xx.BF`
  (the mod's tutorials), and `FEmulator/PAK/INIT/TTRTABLE.BIN/` three `.ttd` tables. These are game-format files the mod
  ships.
- **DRM.** PCGamingWiki: "All versions require Denuvo" (sources dated 2022). The release never mentions DRM or Denuvo. It
  works at runtime inside the community loader stack (Reloaded-II, p5rpc.modloader) and does not ship a modified
  executable. How it stays clear of protected code: not stated [NEEDS VERIFICATION].
- **Localisation.** English only ("Other languages have not been tested"). `P5R_EFIGS.tsv` is a 238-row by 16-column
  table mapping each code of the game's text encoding to a character (rows start at `\u0000`): a decode table for the
  EFIGS font. `Reloaded.Universal.Localisation.Framework` and its Steam and Windows providers ship as dependencies of
  p5rpc.modloader, not of the mod. The mod's own strings are English in its DLL and JSON.
- **Own formats.** `sounds.pak` (magic `UDSP`, 5.6 MB), `hrtf/ku100.p5rh` (magic `P5RH`, from the SADIE II KU100
  set), `navmesh/*.tri` (`P5NV`). `.tri` is a walk map of triangles, not a game format; `.tsv` is the glyph table above.
  `look_models.txt` maps Persona and enemy ids to models ("Generated ... from NAME.TBL and UNIT.TBL"), and
  `persona_looks.txt` gives each model a description written from rendered sheets.

## 3. Licence and credits

- **The mod:** GPL-3.0 (`LICENSE.txt`, version 3, 29 June 2007). Source "available on request", not published. GPL-3.0
  is our licence, so its code would be compatible, once obtained, with credit by our rule 7.
- **Bundled, per `THIRD-PARTY.txt`:** Reloaded.Hooks, Reloaded.Memory.SigScan, CriFs.V2.Hook and CriFsV2Lib,
  FileEmulationFramework and its emulators (LGPL-3.0); p5rpc.modloader, p5rpc.lib, Reloaded.Universal.Localisation.Framework
  (GPL-3.0); Prism (MPL-2.0) with simdutf (Apache-2.0), NVDA controller RPC definitions (relicensed to MPL-2.0), Moderncom
  and {fmt} (MIT), concurrentqueue (simplified BSD), dr_wav (public domain), Highway, djinni and NVGT's SAPI bridge; NAudio
  (MIT); SADIE II HRTFs (Apache-2.0). Reloaded-II (GPL-3.0) is downloaded by the installer. Licence texts are in
  `licenses/` and `hrtf/`. All are GPL-3.0-compatible.
- Sounds by "Saul (Discord: msaul)", licence not stated [NEEDS VERIFICATION]: not reusable without asking.

## 4. Ideas for Metaphor

| Idea | Kind | Tickets |
|---|---|---|
| Scanner by category, nearest first, plus a repeat key that gives the way "leg by leg and how far"; "Where next" | idea | #181, #183 |
| Auto walk by feeding the game's own stick input (a virtual pad), cancelled by any player input | idea | #184 |
| An offline walk map: triangles from the game's collision plus generated layers (locked doors with their flags, fences, warps), each with its generator named | idea | #179, #185, #183 |
| Voice mode: skip lines whose voice clip covers them, computed from the voice banks; keys for speaker and line; speech history | idea | #52, #232 |
| Row and item keys over long menu screens and battle target lists | idea | #211, #145, #143 |
| Battle keys: whose turn with full status, cycle party, battle goals | idea | #143 |
| One key for date, time of day, weather and the current objective | idea | #146 |
| Map cursor on the game's own map screen, by squares and by room, with a bookmark | idea | #144 |
| Extract the needed game files from the player's copy at install with CriFsV2Lib (already credited) instead of shipping them | idea, library (with credit) | #131 follow-up, #179 |
| HRTF 3D audio with NAudio (MIT) and SADIE II (Apache-2.0) | library (with credit) | #182, #183, #185 |
| First-run setup, a data-driven Help (`help_content.json`), an honest "what to expect" page, a bug-report guide with a privacy note | idea | #131 follow-up, `docs/players/` |

No code can be borrowed today: the source is not public.

## 5. Where we can do better, with evidence

- **Open source and review.** Theirs is on request; ours is a public repository with CI and a `security-reviewer` stamp
  on hook and install code (`AGENTS.md`, `CONTRIBUTING.md`). Their installer downloads and runs runtimes and Reloaded-II;
  no review of it is published.
- **Build pinning.** Neither text file names a P5R build or says what happens after a game patch; our rule 2 makes every
  hook check the build and do nothing on a mismatch.
- **Game-derived data.** They ship 28 MB of walk maps derived from game fields, an edited `.FTD`, `.ttd` tables and
  `.BF` files. Our rule 4 forbids that; the way through, which they already use for GameData, is to generate such data on
  the player's PC from their copy.
- **Published research.** Their data headers cite an unpublished `RE_FINDINGS` and internal scripts; our findings and
  raw sources are public with build and tool (`docs/findings/`, `docs/raw-sources/`).
- **Tests.** No tests or replay fixtures are visible in the release; ours has unit, architecture and replay tests
  (`tests/`, `tests/fixtures/sessions`). Whether they test privately: unknown.
- **Where they lead:** breadth of play-testing (through the fourth Palace), cutscene descriptions and the player docs.
  Our equivalents are not built yet.
