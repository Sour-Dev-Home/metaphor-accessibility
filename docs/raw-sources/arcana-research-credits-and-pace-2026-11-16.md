# Arcana's research credits, what it does well, and how fast it moved (compared with this repo)

- **Source:** GitHub REST API (`gh api`) and `gh repo list`, read 2026-11-16, for `ChipsAhoiMcCoy/arcana` and the
  projects it credits. Nothing was downloaded, built or run. URLs and commit SHAs are in "Sources".
- **Saved:** 2026-11-16
- **Game build:** none (about other people's projects; nothing here is pinned to a Metaphor: ReFantazio build).

Extends `arcana-v0.1.0-alpha.3-2026-11-16.md`, `game-text-hooks-community-2026-11-15.md` and
`modding-tools-survey-2026-11-13.md`.

## Sources

Arcana at `main` HEAD `306007c065c694689a2ad605bd51d6a5b323d32f` (2026-11-16T19:48:04Z), licence Zlib (API field):

- <https://github.com/ChipsAhoiMcCoy/arcana/blob/main/THIRD_PARTY_NOTICES.md> ("Research credits"),
  <https://github.com/ChipsAhoiMcCoy/arcana/blob/main/CREDITS.md>, `README.md`, `CONTRIBUTING.md`, `KNOWN_ISSUES.md`,
  `CHANGELOG.md`, and the five releases (tags below).

Credited projects (author, repo, licence by API, HEAD SHA and last push at read time):

- rirurin (<https://github.com/rirurin>): `p3rpc.nativetypes` LGPL-3.0 `eff9018` (2026-06-21); `p3rpc.commonmodutils`
  LGPL-3.0 `f9eb0f6` (2026-10-20); `p3rpc.classconstructor` LGPL-3.0 `0a9edf9` (2025-03-18); `p3rpc.slplus` LGPL-3.0
  `46eee97` (2025-04-04). Siblings: `riri.flowscriptframework` GPL-3.0 `85d20ba`; `riri.eventframework` GPL-3.0
  `e0021e1`; `ghidra-scripts` `360c093` (2026-10-31, no licence field); `sqcrab` GPL-3.0 `29f717e`;
  `p5-exe-structures-docs` `b9ec6ab`; `opengfd` `1677394`; `riri-mod-tools` `a708e94` (2026-06-27, latest tag 0.3.1);
  `cri-adx-rs` `d916192`.
- AnimatedSwine37 (<https://github.com/AnimatedSwine37>): `p3rpc.essentials` GPL-3.0 `9ab815a` (2025-12-10);
  `p3rpc.socialStatTracker` GPL-3.0 `e01bbef` (2025-01-07); sibling `p4g64.accessibility` GPL-3.0 `d1294bd`.
- Malurth (<https://github.com/Malurth>): `p3rpc.socialStatTrackerPlus` GPL-3.0 `23d4123` (2026-06-17), "a GPLv3 fork
  of AnimatedSwine37's Social Stat Tracker".
- Encryqed (<https://github.com/Encryqed/Dumper-7>) `2c1eae3` (2026-10-26).
- TGE (GitHub user `tge-was-taken`), Atlus Script Tools: <https://github.com/tge-was-taken/Atlus-Script-Tools>, GPL-3.0,
  `master` `05483fe12f9c482ee17f38ded2e115f4c55015b3` (2026-10-25).

## 1. Do the credited projects help us?

Short answer: little directly. Nearly all of it is Persona 3 Reload, which is an Unreal Engine game; Metaphor runs on
Atlus's GFD-family engine (OpenGFD's description: "Atlus's GFD Engine (Persona 5, Metaphor: Refantazio)",
<https://github.com/rirurin/opengfd>). Struct layouts and byte patterns do not carry over. What carries over is method.

| Project (author) | Covers Metaphor or our Atlus pieces? | Use to us |
|---|---|---|
| Dumper-7 (Encryqed) | No. Confirmed Unreal-only: "SDK Generator for all Unreal Engine games. Supported versions are all of UE4 and UE5." (README line 4, `2c1eae3`). | None. |
| p3rpc.nativetypes, classconstructor, slplus, commonmodutils (rirurin) | P3R/Unreal only (descriptions: "Struct defintions for Persona 3 Reload code mods", "Persona 3 Reload specific"). No Metaphor. | Prior art for how a Reloaded-II code mod documents native layouts in C#; commonmodutils is "Mod utility code for Reloaded-II mods" (LGPL-3.0). Method only. |
| p3rpc.essentials (AnimatedSwine37) | P3R base support, "A Reloaded mod that adds base support for P3R modding." | None for Metaphor. |
| socialStatTracker, socialStatTrackerPlus (AnimatedSwine37, Malurth) | P3R UI overlays. | None. |
| Atlus Script Tools (TGE) | See below: no Metaphor library. | Format background only. |

### Atlus Script Tools (TGE)

- Supported games list (README, `05483fe`): Catherine, Persona 3/4/5 and their variants, Persona 3 Reload, SMT3, Digital
  Devil Saga, Raidou, Trauma Center, Etrian Odyssey II. It does not list Metaphor. The tree (725 paths at `05483fe`)
  has no path containing "metaphor" or "refantazio"; `Source/AtlusScriptLibrary/Libraries/` holds one JSON per game and
  none is Metaphor (the newest Unreal-era one is `Persona3Reload.json`, `"MessageScriptFormatVersion": "Version1Reload"`).
  This agrees with `modding-tools-survey-2026-11-13.md`. Whether Metaphor's `.msg` text files (we read them as
  `EN/message/CharaName.msg`) share the BMD binary format: `[NEEDS VERIFICATION]`; no source found.
- Message syntax it documents (`Documents/Reference/MessageScriptLanguageReference.txt`, 82 lines): `[d <id> <speaker>]`
  dialogue, `[s <id>]` selection dialog, `[f <table> <function> ...]`, `[n]`, `[e]`, `[w]` ("Waits for the player to press
  A"), `[b ...]` bust-up, `[v ...]` voice cue. Its comments: "speakerId = 0xFFFF = use no speaker name?" and "speakerName
  is actually a line itself". This is bracket syntax from the PS-era games. It does not document `<NS>`, `<VOICE>`, or
  `<CHAR_NAME>`/`<FOLLOWER_NAME>`-style tags, and no name table. Those tags in our notes come from the game's own files.
- Other reference files: `Documents/RE/MessageScriptInterpreter.cpp`, `Documents/RE/Font.cpp` (last commit
  `574633a`, 2017-09-21), so very old and not Metaphor.
- Tooling note: the compiler/decompiler is a CLI (`AtlusScriptCompiler -Decompile ... -Library P5R`); our rules (no
  game content) apply to anything it would extract.

### rirurin's work, newer than what we hold

Already in our sources: OpenGFD `1677394` (2026-03-19), riri-mod-tools `a708e94`. Nothing newer on those two (same
HEADs at read time). Other rirurin repos with a Metaphor link, found by `gh repo list rirurin`:

- `ghidra-scripts`: `ghidra_scripts/MetaphorLabelSwitch2VTables.java`, header "Labels VTables in Switch 2 exefs (designed
  for Metaphor Refantazio, probably works for other Switch 2 games)", `//@author Rirurin`; last commit `360c093`
  2026-10-31, "Add scripts for auto-analyzing all binaries in folder and retrieving vtables from RTTI on Switch (2)
  executables". Switch 2 executable, not the PC build we hook. Whether it helps on the PC executable: `[NEEDS VERIFICATION]`.
  The README says most scripts target Unreal. `old/ghidra_11.0/ghidra_scripts/CreateStructuresForGFD.java` exists
  (GFD structures; contents not read).
- `sqcrab` (GPL-3.0): "Rust bindings for Squirrel", "specifically targets Squirrel 3.2 with SQ64 set to true"; its credits
  list modmusu's NutCracker-Metaphor (Squirrel decompiler). Relevant to the Squirrel function notes
  (`squirrel-script-functions-2026-11-16.md`); NutCracker is already in the tools survey.
- `p5-exe-structures-docs`: Persona 5 Royal (same engine family) notes from 2023: Task Data, Resource, Shader pages. No
  message/text page in the tree at `b9ec6ab`. Engine-family hint only.
- `riri.flowscriptframework`: "for Persona 3 Reload" only.
- AnimatedSwine37's `p4g64.accessibility` ("makes it more accessible", README is only a title) and `persona-modding-docs`
  are not credited by Arcana; Persona 4 Golden Access is already surveyed in `persona-4-golden-access-2026-11-16.md`.

### Mapping to open issues

| Issue | Finding |
|---|---|
| #74, #81, #106, #107, #108, #109, #83 (names, placeholders, CharaName) | Nothing in the credited projects documents Metaphor's name tags, `CharaName.msg` mapping or display-name choice. Atlus Script Tools' `speakerId = 0xFFFF` / "use variable speaker name?" comments are a PS-era hint for #74/#106, not evidence for this build: `[NEEDS VERIFICATION]`. Arcana's own `src/msg.cpp` cites P3R RE ("rirurin/p3rpc.nativetypes Xrd777.cs, verified live 2026-10-27", HEAD `306007c`); it shows the practice, not our answer. |
| #93, #94, #103 (hooks, menu focus, voice) | `ryo-cri-atom-patterns-2026-11-16.md` and the Reloaded-II notes are closer. rirurin's `cri-adx-rs` ("calling statically linked methods from Criware's ADX Library") is CRI-related; Metaphor use `[NEEDS VERIFICATION]`. |
| #114, #118 (backlog, tutorial pop-ups) | Arcana's `src/screens/backlog.cpp` and `tutorial_list.cpp` (see the alpha.3 note) are the prior art; none of the credited projects. |
| #84 (hook layer) | riri-mod-tools (Rust hooking on Reloaded-II) and `sqcrab` are method references only. |

## 2. What Arcana does well (observations)

- **Scope:** one release at a time grew from speech for "dialogue, choices, notices, menus, battles, shops, Social Links,
  the Velvet Room, calendar, mail, saves" (alpha.1 `CHANGELOG.md`) to exploration sonar, audio description of 3D story
  scenes (0.3.0) and a rename to Arcana. Unit tests need no game (`CONTRIBUTING.md`: "The tests need no game");
  `tests/` holds 37 entries; a `tools/` dev client builds and drives the game.
- **Release notes:** every release has the same shape: "What this is", a not-affiliated line, "What's new in this version"
  with plain-language headings, "Requirements", "Install or update" (steps, uninstall, the old-folder warning when the
  folder was renamed), then a link: "Every change is in CHANGELOG.md". Bullets name what the player hears, for example
  `"Jin, 96%, Downed, Poisoned"`. All five are marked pre-release in the API. `KNOWN_ISSUES.md` opens "Arcana is an alpha.
  This is what is missing and what has not been tested."
- **Credits and licensing practice:** `CREDITS.md` (people, "Built on", "Research", with unofficial-fan-project notice) plus
  `THIRD_PARTY_NOTICES.md` and a `LICENSES` folder; `CONTRIBUTING.md`: "Mark any file you take from elsewhere, and add its
  license to `LICENSES` and `THIRD_PARTY_NOTICES.md`." The research wording, which we might adopt:
  > "These projects were used as research references. None of their code is included. The mod's source comments name them
  > where a fact came from them."
  > "Eleven of the thirteen short byte patterns in our source that locate game functions (in `src/bmdpages.cpp`,
  > `src/msg.cpp`, `src/stats.cpp`, `src/textcap.cpp` and `src/ue/ue.cpp`) are the same as patterns published in those
  > mods. They describe the game's own machine code. No other text is shared with those projects: a check for shared lines
  > of 40 or more characters found none."
  The section is in the first commit `d6d61b6` (2026-11-05). The repo has no script for the 40-character check that I found
  in the top-level listing (`.gitignore CHANGELOG.md CMakeLists.txt ... tests third_party tools`); how it was run:
  `[NEEDS VERIFICATION]`. Each credit names licence and URL; the AI-assisted authorship is stated ("Claude Code (Anthropic)
  writing most of the code").
- **Testing with players:** the README says "Most of the game is covered and much of it tested, but nobody has finished the
  game with the mod yet", and asks for bug reports with screen reader, input device and log. The repo reports
  `open_issues_count` 0 and no issues exist (alpha.3 note), so no tester reports are visible on GitHub. Player testing
  beyond that: `[NEEDS VERIFICATION]`.

## 3. Productivity numbers

Arcana (`gh api repos/ChipsAhoiMcCoy/arcana/commits`, 220 commits; contributors API: ChipsAhoiMcCoy 220):

- First commit `d6d61b6880ce35771fba504a36e93c0e9e7bf892`, 2026-11-05T02:34:52Z ("Persona 3 Access 0.1.0-alpha.1"). The
  repo's `created_at` is 2026-11-05T03:22:42Z, after that commit, so earlier work may have been imported or squashed:
  `[NEEDS VERIFICATION]`. Latest `306007c`, 2026-11-16T19:48:04Z. Span: 12 calendar days.
- Commits per day (author date): 11-05 2; 11-07 2; 11-10 8; 11-11 78; 11-12 39; 11-13 28; 11-14 4; 11-15 43; 11-16 16
  (partial day). Per ISO week (Mon-Sun): 2026-11-02 week 4; 11-09 week 200; 11-16 week 16 so far.
- Releases (all pre-release): `v0.1.0-alpha.1` 2026-11-05T03:32Z (tag commit `3093d4c`); `v0.1.0-alpha.2` 2026-11-07T07:24Z
  (`f0fe009`); `v0.1.0-alpha.3` 2026-11-11T07:29Z (`c7b8a90`); `v0.2.0` 2026-11-13T12:37Z (`d0b3d8e`); `v0.3.0`
  2026-11-16T05:00Z (`5060d08`). Gaps: 2, 4, 2, 3 days.
- Commits between releases (compare API): alpha.1 to alpha.2 1; to alpha.3 24; to 0.2.0 130; to 0.3.0 52; since 0.3.0 11.
- `CHANGELOG.md` bullet counts per section: alpha.1 8; alpha.2 25; alpha.3 42; 0.2.0 113; 0.3.0 40; Unreleased 10.
  Accumulation: alpha.1 = speech for most screens; alpha.2 = stability, battle details, victory screen; alpha.3 = (see the
  alpha.3 note); 0.2.0 = top-down exploration, sonar sounds, outfit descriptions; 0.3.0 = rename, audio description in 3D
  scenes, auto-walk, Shadow sounds.

This repo (`origin/main` `253a141`, 2026-11-16T22:57:21Z; `git log | wc -l` = 65; contributors API: SourE-dev 59,
dependabot[bot] 3):

- First commit `3642a84`, 2026-11-13 ("chore: project skeleton for Metaphor: ReFantazio modding"). Span: 4 calendar days.
- Commits per day: 11-13 10; 11-14 13; 11-15 6; 11-16 36 (partial day). Per ISO week: week of 11-09 29; week of 11-16 36.
- Merged PRs (`gh pr list --state merged --limit 200`): 53 (11-14 11; 11-15 7; 11-16 35); 2 open; closed issues 42; 0 releases
  and 0 tags; 31 open issues listed at read time.
- Same day-count basis: Arcana had 90 commits by its 7th calendar day (to 11-11); this repo has 65 by its 4th. Arcana ships
  playable builds; this repo has none yet (its work so far is notes, spikes and hook scaffolding).
