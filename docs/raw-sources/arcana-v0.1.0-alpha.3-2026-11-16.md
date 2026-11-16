# Arcana (Persona 3 Access) v0.1.0-alpha.3: what it speaks, how it works, what to borrow

- **Source:** GitHub REST API (`gh api`): the release, tag, files and source of `ChipsAhoiMcCoy/arcana` at tag
  `v0.1.0-alpha.3` (commit `c7b8a908eb6882382a348cbf94a945e79486df10`); URLs in "Sources" below. Nothing was
  downloaded, built or run.
- **Saved:** 2026-11-16
- **Game build:** none (this is about another project's mod; ours is not pinned to a build by this note).

Extends the Arcana lines in `game-text-hooks-community-2026-11-15.md` and
`screen-reader-speech-libraries-2026-11-15.md` (both at Arcana HEAD `d4f7c16`).

## Sources

All read through `gh api`, 2026-11-16. Nothing was downloaded or run. `S` = commit
`c7b8a908eb6882382a348cbf94a945e79486df10`; paths below are `ChipsAhoiMcCoy/arcana` at `S` unless noted.

- Release: https://github.com/ChipsAhoiMcCoy/arcana/releases/tag/v0.1.0-alpha.3, published 2026-11-11T07:29:04Z,
  title "Persona 3 Access 0.1.0-alpha.3", target `main`.
  - Assets (names and sizes only): `persona-3-access-0.1.0-alpha.3.zip` (33,769,886 bytes) and its `.sha256` (101
    bytes). The notes give the zip's SHA-256 as `b78c4f63...b50450`.
- Tag `v0.1.0-alpha.3`: annotated tag object `7921abe4f99243035d91d179fad54faf18d588cd` (2026-11-11T07:28:51Z,
  message "Persona 3 Access 0.1.0-alpha.3") pointing at commit `S`. Repository licence: Zlib (API field `license`).
- Files at `S` (browse as `https://github.com/ChipsAhoiMcCoy/arcana/blob/S/<path>`): `README.md`, `CONTRIBUTING.md`,
  `INSTALL.md`, `KNOWN_ISSUES.md`, `CHANGELOG.md`, `THIRD_PARTY_NOTICES.md`, `src/speech.cpp`, `src/speech.h`,
  `src/hooks.cpp`, `src/input.cpp`, `src/msg.cpp`, `src/msg_render.cpp`, `src/voice.cpp`, `src/settings.cpp`,
  `src/settings.h`, `src/commands.cpp`, `src/mod.cpp`, `src/ue/ue.h`, `src/ue/ue.cpp`, `src/ue/scan.cpp`,
  `src/core/navigator.h`, `src/core/graph_announcer.h`, `src/screens/menu.cpp`, `src/screens/choice.cpp`,
  `src/screens/backlog.cpp`, `src/screens/tutorial_list.cpp`, `third_party/prism-bin/prism-sdk-v0.18.1/include/prism.h`.
- Issues and discussions: the repo reports `open_issues_count` 0 and `has_discussions` false, and
  `gh api repos/ChipsAhoiMcCoy/arcana/issues?state=all` returned no items. Nothing outside the release notes and the
  files above was available to read about this release.
- Freshness: at the time of reading, `main` was 193 commits ahead of `S` (`compare` API, status "ahead"). Everything
  below is about `S`; `main` may differ. One check on `main`: `src/settings.cpp` still lists `VoicedSilent` as the
  `voiced_lines` default.
- Naming: the release notes link to `ChipsAhoiMcCoy/persona-3-access`; the API serves the repo as `arcana`.
  [NEEDS VERIFICATION] which is the current name (the earlier notes used `arcana`).

## 1. What changed in alpha.3 and what the mod speaks

### What changed (release notes, "What's new in this version")

Headings: Sonar (new), Menus, Shadows in Tartarus, Exploring, Sounds, Battles, Keys (F1), Game over, Victory screen,
Shuffle Time, Velvet Room. In short:

- Sonar: every nearby point of interest repeats its own sound from where it is, "faster and louder the closer it is";
  F3 toggles; settings for scope, range (10 m default, up to 40), per kind and volume; sounds take turns.
- Menus: what a menu presses in the game "now goes out on the device you pressed" (key or button), fixing a keyboard
  confirm turning into a controller press; Rewind can reach any day; no re-read of "Menu, System, 9 of 9" after
  returning to the title.
- Tartarus Shadows: growl heard from 30 m, strike-reach ring read from the game's own hit area, alarm when noticed.
- Exploring: auto-walk finds the way in to walled-in targets, stops on controller stick push or when a dialogue or
  menu opens; exits "said by name alone".
- Battles: target no longer wraps ("a further press says who you are still on"); "All enemies"/"All allies" as one
  choice; damage on the target you chose yourself is "the number alone"; Rush announced; hit words ("Weak",
  "Critical", "Resist", "Miss", "Block") spoken.
- Keys: F1 names Auto-Recover and Navi in Tartarus and the keys of a dialogue choice list.
- Game over: the poem is read, confirm skips the rest, each choice followed by its own description, choices
  announced only once the game takes input on them.
- Victory screen no longer waits for the game's animation; Shuffle Time cards say their face; Velvet Room status
  page is read again ("Register this Persona?" first).
- Known issues in the notes: 3D story scenes have no audio description; "Nobody has finished the game with the mod
  yet"; "A game update can break the mod."

### What it speaks (README "What is covered", release notes, `KNOWN_ISSUES.md`)

- Dialogue: dialogue, inner thoughts ("Thinking: ..."), system notices and pages; cutscene subtitles only if turned
  on (setting "Read cutscene subtitles", default off, `src/settings.cpp` `subtitles`).
- Voiced lines: three modes, setting "Voiced dialogue" (`src/settings.h` L12 `enum Voiced { VoicedRead, VoicedName,
  VoicedSilent }`; labels in `src/core/strings.h`: "read aloud", "speaker's name only", "not read, the voice plays
  alone").
  - **Default is "not read, the voice plays alone"**: `src/settings.cpp` L54 `voiced_lines ... VoicedSilent` as the
    default value (`Def::def` is the stored value, `settings.cpp` L27 and L98). In that mode nothing is spoken for a
    voiced line; `src/msg.cpp` `voiced()` (L155-157) speaks the speaker only when the setting is `VoicedName`.
    The earlier note's "(speaker name only)" describes the option, not the default.
  - "A speaker is named only on their first line in a row" (README, Mod settings; `src/msg.cpp` L141-142 comment and
    `g_last_speaker`). A narration line or the end of the message session resets it.
  - A line the game will not voice "is read at once" (`say_dialogue`, `src/msg.cpp` L200-220).
  - Whether a page is voiced is read from its BMD data: the page opens with message functions and a voiced page has
    function table 3, function 1, a non-zero cue argument (comment and `declares_voice`, `src/msg.cpp` L175-196,
    measured by the author 2026-11-03). Those layouts are P3R-specific.
  - Voice playback itself is read from the sound manager's player slots (`src/voice.cpp` L1-7), so the mod knows when
    a voice starts and ends.
  - After a line: setting "When a line ends" (say "Next" (default), page-turn sound, message chime, cursor tick, off);
    "On a voiced line this comes when the voice finishes" (README). Suppressed while Auto is on.
  - Party/navigator chatter during battle and exploration is not read over the voices; it goes to the log (README
    Battles and "party's voiced chatter"; `src/msg.cpp` L168-175 `g_hush`).
  - While fast-forward is on, lines go to the log unspoken and "The last one held is read if it still shows when
    fast-forward ends" (`src/msg.cpp` L158-163).
- Menus and focus: "Every menu ... is the mod's own: it reads each entry from the game's data, speaks it the moment
  you move, and moves the game's highlight to match" (README, "In menus"). Position is spoken ("3 of 9"); at a list
  end the item is read again (`src/core/navigator.cpp` L270-277); Home/End, Tab for sections; typeahead in settings.
  Menus "wait for the game's opening animations" and presses before then are dropped (`CHANGELOG.md` alpha.2).
- Choices: "the highlighted answer with its position as the list appears and on every move" (README). Implemented as a
  menu over the message window's `USelItem` (`src/screens/choice.cpp` L1-4); the question is read, then the answer.
  The mod writes the list's cursor and plays the cursor sound (`choice.cpp` comment: `ListCursor (+0x60) written`).
- Battle: turn owner with HP/SP, target with health and place ("1 of 2"), buffs and affinities, skills with costs and
  descriptions, items with counts, enemy and ally actions, damage, healing and defeats; analyze, survey and details
  keys; the player's own choices are not read back (README "Battles"; alpha.2 and alpha.3 notes above).
- Navigation: wall tones, the sonar, a beacon, a points-of-interest list read as "Iwatodai Station, 6 meters, 2
  o'clock, 1 of 3", auto-walk, "Describe the area" text, where-am-I, objective, area name on arrival (README
  "Keys", "What is covered").
- Backlog: the game's log screen (key B) is read as you scroll. Its text storage "is unmapped, so each entry is read
  from the mod's own record of the session (msg::history)"; older entries "are said to be so" (`src/screens/backlog.cpp`
  L1-5 and README "The log").
- Repeat keys: Backslash "Repeat the current message, or describe the current screen" (README "Keys";
  `src/commands.cpp` L91 binds `VK_OEM_5`; `src/msg.cpp` `repeat()` speaks "Speaker: text" plus the focused choice,
  interrupting). Controller: X in the trigger layer. Shift+Backslash: Social Links.
- F1 reads the keys of whatever is in front of you; hints are never spoken by themselves (`CONTRIBUTING.md` rule
  "Key hints are never spoken by themselves. They go behind F1.").
- Braille: not found. No file read at `S` mentions braille (README, CONTRIBUTING, INSTALL, KNOWN_ISSUES, CHANGELOG,
  `src/speech.cpp`), and `src/speech.cpp` calls only `prism_backend_speak`, `prism_backend_stop` and
  `prism_backend_is_speaking`. [NEEDS VERIFICATION] for files not read; a repo-wide code search returned nothing.
- Settings: `settings.ini` under `%LOCALAPPDATA%\P3RAccess`, edited in an in-game accessible menu (F2, or both
  triggers + View). "Changes are saved at once" (README "Mod settings"). About 30 rows: sonar, volumes, 3D sound,
  voiced dialogue, line-end cue, subtitles, audio description.

## 2. How it finds text and focus, handles input, and copes with updates

- Reflection: `src/ue/ue.h` header: "A small Unreal Engine 4.27 reflection layer for P3R (offsets measured by
  Dumper-7 on build 0x69ce3439 and re-validated at init). Everything is resolved BY NAME at runtime -- classes,
  properties, functions -- so a game patch that only moves code keeps working." Rules stated there: never hold a
  `UObject*` across frames (use a weak handle), touch game objects only on the game thread.
  - `src/ue/ue.cpp` L525-530: GObjects and GNames found by byte signature first, "measured offsets are the
    fallback and the cross-check"; ProcessEvent is vtable slot 0x44 of any UObject.
  - Messages: `src/msg.cpp` L1-8: `UMsgManager.ProcList` (reflected) -> `UMsgProcItem` -> `UMsgItem` ->
    `FCurrentBmdPage`, plus a hand-read choice object `USelItem` (offsets `+0x40 selIndex`, `+0x48 selCount`,
    credited to community RE `rirurin/p3rpc.nativetypes`, "verified live 2026-10-27"). The page is rendered to plain
    text by calling the game's own `BmdPageToFString`, found with `scan::find_unique` (`src/msg.cpp` L566-569), so
    the game substitutes names and tags itself. Icons become key names (`src/msg_render.cpp` L1-2).
  - Text drawn straight to screen: `src/textcap.cpp` captures the game's font-draw calls (credited to
    `rirurin/p3rpc.slplus` and `AnimatedSwine37/p3rpc.socialStatTracker`).
  - Object creation: `src/watch.cpp` registers an engine object-creation listener (`GUObjectArray +0x68`) so the mod
    sees objects such as `PlayAdxControl` as they are made.
  - Frame clock: the mod's tick is the message pump (`src/hooks.cpp` L217-221: "ProcessEvent can go quiet for seconds
    on a static screen ... so the message pump is the mod's frame clock"); a `ProcessEvent` hook only backs it up
    (`src/ue/ue.cpp` L489-496).
- Hooking: Microsoft Detours; every transaction "updates EVERY thread" (`src/hooks.cpp` L18-20). Loaded by a
  `dsound.dll` proxy (`src/proxy/`), which waits "until the object array and name pool are populated (the protection
  layer has also finished with the code by then)" before scanning or hooking (`src/mod.cpp` L83-93, up to 2
  minutes).
- Input:
  - Rule: "Never use a low-level keyboard hook. It breaks NVDA." (`CONTRIBUTING.md`, "Rules for changes").
  - Reason and enforcement: `src/hooks.cpp` L194-197: a `WH_KEYBOARD_LL` hook in a busy game "gets the screen
    reader's own low-level hook timed out and removed by Windows (Grim Dark, 2026-08-21: NVDA keys dead after
    alt-tab)". The mod therefore refuses any such hook the game or a library installs (it returns a fake handle) and
    swallows sticky/toggle/filter-keys `SystemParametersInfoW` calls (L198-215).
  - Keys come from a detour on the game's own window procedure: "Unreal reads keys from these messages on the game
    thread, so a key the mod binds is handled here ... and swallowed" (`src/hooks.cpp` L228-229, L237
    `wndproc_hook`; `GetKeyState`/`GetAsyncKeyState` are deliberately not hooked, `src/input.cpp` L471-473).
  - Controller: the import-table slot of `XInputGetState` in the game executable is pointed at the mod's function
    (`src/input.cpp` L455-471). Reason recorded: a Detours hook on the function body "was overwritten by Steam's"
    Steam Input patch, so a DualSense never reached the layer (2026-10-29). Holding both triggers opens the mod's
    controller layer and hides buttons from the game.
  - The mod does not steal focus: "Never steal focus from the player" (`CONTRIBUTING.md`).
- Game updates: `ue::known_build()` compares the executable's PE timestamp to one measured value
  (`src/ue/ue.cpp` L28, L546-548). Reflection and signatures "work on any build"; fixed addresses and offsets do not, so
  on other builds menus are turned off and the mod says so (`src/ue/ue.h` comment on `known_build`: crashes, frozen
  games and menus stuck "(a player's log, 2026-11-05)"; `src/mod.cpp` L145 speaks `kUnsupportedBuild`; `CHANGELOG.md`
  alpha.2 "Stability"). If the engine never comes up the mod stays inert and says "could not find the game engine"
  (`src/mod.cpp` L94-97; `INSTALL.md`). The notes still say "A game update can break the mod."

## 3. Speech, install and configuration

- Prism use (`src/speech.cpp`, whole file, 115 lines at `S`):
  - Loads `prism.dll` by full path from beside the mod's DLL (that folder is not on the game's DLL search path),
    then `prism_init`, `prism_registry_create_best`, `prism_backend_initialize`. Prism picks the backend; there is no
    list and no setting. README: "NVDA or JAWS. Without a screen reader, the mod uses Windows speech." INSTALL:
    "The mod picks its speech at startup and does not switch later"; the log line `speech: using backend` says which.
  - Pinned Prism SDK `v0.18.1`, committed in the repo as a prebuilt binary under `third_party/prism-bin/` (MPL-2.0;
    `THIRD_PARTY_NOTICES.md` "Prism").
  - Interrupt vs queue: one function `speak(text, interrupt = true)` passes `interrupt` straight to
    `prism_backend_speak`; the project rule is "Speech doesn't interrupt by default. Focus moves and screen entry do."
    (`CONTRIBUTING.md`). In practice dialogue pages interrupt unless Auto is on (then they queue so a page cannot cut
    off the last, `src/msg.cpp` L164-166, L215); a choice list is queued after its question (`say_after`, L222-227).
  - Duplicate suppression: `say_line` in `src/screens/menu.cpp` L39-52: "a line already said is dropped until the
    player presses something or something else speaks. Nothing here counts time." It uses a set cleared when the
    player's move counter, the controller activity time or `speech::count()` changes. The message path skips an
    echo of the same page (`src/msg.cpp` L306) and system notices repeating a line (L327). Project rule: "No
    delays, debounces or retries to fix a double announcement or a stale read. Find the state the game is really
    in." (`CONTRIBUTING.md`).
  - Text prepared by `core::tidy_joins` (no stray ".,"); a lone "." is avoided because it "is read as 'dot'"
    (`src/core/text_clean.cpp` L3-5).
  - A mute flag stops backend calls but still records history; the last 500 lines and every call's timing are kept
    for a dev server (`src/speech.cpp`). The backend call "runs on the game thread"; calls over 8 ms are logged.
  - Braille: no use at `S`. Prism's header at the pinned version declares `prism_backend_braille` and
    `prism_backend_output` and the flag `PRISM_BACKEND_SUPPORTS_BRAILLE` (`prism.h` L160-162, L284, L402-407).
    Which backends implement braille is [NEEDS VERIFICATION].
- Install: unzip, copy `dsound.dll` and the `P3RAccess` folder into `P3R\Binaries\Win64` (the folder with
  `P3R.exe`), start the screen reader first, then the game from Steam; "You will hear 'Persona 3 Access loaded'"
  (release notes and `INSTALL.md`). Uninstall is deleting those two items. The zip ships a `.sha256`. Troubleshooting
  covers antivirus quarantining `dsound.dll`, running reader and game at the same permission level, and reading the
  log at `%LOCALAPPDATA%\P3RAccess\p3access.log` (previous session: `p3access.last-play.log`); the notes warn the log
  holds spoken story text and may contain a Windows user name.
- Configuration by blind players: entirely in-game. F2 (or both triggers + View) opens a settings menu that works
  like every other menu (arrows, Home/End, type to jump, Space steps values, "Changes are saved at once");
  `settings.ini` is plain text in the same folder; key hints are on F1 and Shift+F1. Rules: "Every mod key has a
  controller equivalent, and every player-facing key or setting goes in the README in the same change"
  (`CONTRIBUTING.md`). Bug reports ask for screen reader, keyboard or controller, and the log.
- Credits and licence: built on Grim Dark (Austin Hicks) and Dark Vision (Bradley Renshaw) (both Zlib), uses Prism,
  Steam Audio, Detours and miniaudio; the mod is under zlib (release notes "Credits"). Files altered from Grim Dark
  carry a header saying so (`src/speech.cpp` L1).

## 4. Compared with this repo

Ours: Reloaded-II mod in C#, hooks through Reloaded's shared hooks (ADR 0006), Prism from a fixed backend list (ADR
0005), one announcer interface (ADR 0004), logic in plain libraries (ADR 0003).

| Difference | Arcana at `S` | This repo | For us? |
|---|---|---|---|
| Loader | `dsound.dll` proxy, Detours | Reloaded-II hook loader (ADR 0006) | Neutral: each fits its stack. |
| Prism backend choice | `prism_registry_create_best` | Fixed list, never `create_best` (ADR 0005) | To our benefit: ours avoids loading a planted DLL by bare name. |
| Prism DLL in git | Prebuilt `prism.dll` committed (v0.18.1) | Fetched by SHA-256 at publish time (v0.18.3) | To our benefit: nothing binary in the tree. |
| Braille | None found | Wanted (#115) | Neutral: Arcana is no help; Prism's API has it. |
| Voiced-lines default | "not read" (name optional) | Auto: name for English voice, full line for Japanese (#57) | To our benefit: Auto serves players who cannot hear the voice. |
| Voiced detection | Reads BMD function bytes plus live voice playback | `<VOICE>` tag in the message (#57, #103) | Neutral: ours needs no native layout, but needs #103. |
| Settings UI | In-game accessible menu, `settings.ini` | File in Reloaded's per-mod config, watched (#104) | To our detriment for now: players edit a file or use Reloaded's launcher; Arcana's is usable without sight. |
| Menu focus | Own menus that mirror and write the game's cursor | Speak game focus (#59, #93) | Neutral: theirs is far more code per screen. |
| Duplicate suppression | Per-state, "Nothing here counts time" | Not yet specified | To our benefit if copied: a simple rule that cost them real bug reports. |
| Input | Window-procedure detour + XInput import-table patch | Hook the game's own input (#58) | Neutral: same idea; their notes on Steam Input apply. |
| Scope | Whole game: sonar, battle, audio description | Dialogue first (#52) | To our detriment only in reach; ours is early. |
| Update safety | Executable timestamp, one build, menus off elsewhere | Signature must match exactly once, else no patch (ADR 0006, ground rule 2) | Neutral: same principle, ours is per hook. |
| Maturity and tests | Alpha, "nobody has finished the game"; `tests/` for core only | Spec-first, architecture tests | Neutral. |

## What to borrow (suggestions, mapped to open issues)

- #57 / #104 (voiced lines): Arcana's behaviours: name only on the first of a run of lines by one speaker; unvoiced
  lines read at once; waiting for the voice to end before the "Next" cue (`src/msg.cpp` L134-146, L200-220;
  README "Voiced dialogue"). Its default of "not read" is the one choice to not copy. Also: battle and
  field chatter windows go to the log unspoken rather than interrupting (`msg.cpp` L168-175); consider whether our
  Metaphor spikes show similar auto-advancing windows.
- #58 (repeat key): Arcana's repeat speaks "Speaker: text" plus the focused choice and always interrupts
  (`msg.cpp` `repeat()`); when there is nothing, it says so ("kNoMessage"). Its fast-forward rule keeps the last
  held line for a repeat. For input, read its reasons for XInput import-table patching and Steam Input
  (`src/input.cpp` L455-471) before choosing a controller hook.
- #59 (menu and choice focus): "A focus change is announced exactly once no matter what caused it" and the pull-based
  announcer (`src/core/navigator.h` header); `say_line`'s no-repeat rule (`src/screens/menu.cpp` L39-52); "n of m"
  positions; re-reading the item at a list end; wait for the opening animation and drop earlier presses; a choice
  reads as question first then answer (`src/screens/choice.cpp` L1-4).
- #83 (placeholders): Arcana does not substitute tags itself. It calls the game's own page renderer and then cleans
  the text (`src/msg.cpp` L566-569, `src/msg_render.cpp`, `src/core/text_clean.cpp`). A spike could check whether
  Metaphor has an equivalent renderer before building lookups; the address and signature would be ours to find
  ([NEEDS VERIFICATION], P3R's does not carry over).
- #114 (backlog): Arcana's backlog text storage "is unmapped", so it reads its own per-session record and says
  entries before the session are earlier (`src/screens/backlog.cpp` L1-5). A fallback if our spike also cannot read
  the game's storage; the cost is that earlier-session lines are unavailable.
- #115 (braille): no prior art in Arcana. The Prism header at v0.18.1 has `braille` and `output` entries
  (`prism.h` L160-162, L402-407); check them in our pinned v0.18.3 and per backend (NVDA, JAWS) before designing.
- #118 (tutorial pop-ups): in P3R the tutorials are a message-window kind ("Tutorial") read like dialogue, with the
  page position added ("... 1 of 3") and Q/E turning pages (`src/msg.cpp` L316-322; README "Key hints"). That is a
  hint to look at the message route for a different `kind` in our spike; Metaphor's route is unconfirmed, and in
  session 1 none went through `MsgRequestByName` (our issue text).
- Housekeeping ideas: F1 as the only place hints are spoken; a controller equivalent for every key; an in-game
  settings menu so blind players never edit a file (relevant to #104's file-only plan).
