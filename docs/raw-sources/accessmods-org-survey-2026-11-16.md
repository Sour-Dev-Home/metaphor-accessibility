# AccessMods organisation survey

- **Source:** https://github.com/AccessMods (public GitHub organisation), read through the GitHub API with `gh` on the
  date below. Org description is empty; 6 public repositories, all MIT-licensed except the profile and website repos
  (which have no licence). Org-profile files and linked Discord, forum (audiogames.net) and website were not fetched.
- **Saved:** 2026-11-16
- **Game build:** none (this note is about other games' mods, not Metaphor: ReFantazio)
- Method: `gh api orgs/AccessMods`, `gh api orgs/AccessMods/repos --paginate`, `gh api repos/<r>/contents/<path>`,
  `gh issue list -R`, `gh release view -R`. Short quotes only. Nothing was posted or joined.

## Repositories and HEAD commits (default branch, read 2026-11-16)

| Repository | HEAD SHA | Last push | Licence |
|---|---|---|---|
| PhoenixWrightTrilogy | eb40f479de0de685b2751b1b49a1adfcc5409467 | 2026-03-01 | MIT |
| DDLCPlusAccess | 1bd12123edf469df13ec9fd00d63f3a3704b695d | 2026-01-21 | MIT |
| UnityAccessibilityLib | 958e20c4da0caf0f6deb6f4177207150b0b1c990 | 2026-01-12 | MIT |
| AccessModsInstaller | 549e8980ba02ea4af92f247c9f957302537b54e6 | 2026-01-21 | MIT |
| .github (profile, CONTRIBUTING, workflow guide) | 020f3bc271a39bdb284d794689c8fdad89730817 | 2026-01-17 | none |
| AccessMods.github.io (static site) | 384af7b392b7d2b007cdf0722aa6063c7ca55d18 | 2026-01-19 | none |

Scope note: every mod here targets Unity games (Mono, MelonLoader, Harmony). None targets Unreal. Metaphor: ReFantazio is
not Unity, so their hooking code does not transfer; their feature design, community process and library choices do.
No repository has Discussions enabled (`has_discussions` false for PhoenixWrightTrilogy and DDLCPlusAccess).

## 1. Features per mod

### Phoenix Wright: Ace Attorney Trilogy (PhoenixWrightTrilogy @ eb40f47)

Source: `README.md`, `CLAUDE.md`, `AccessibilityMod/Core/InputManager.cs`.

- Spoken: dialogue with character name; menus and UI; investigation hotspots; court record (evidence, profiles);
  trial life gauge; seven minigames; the orchestra music player; Psyche-Lock; verdicts; save/load and options menus
  (README "Features"; CLAUDE.md "Patches").
- Character names: names are image-based in the game, so a sprite-ID to name table ships as JSON per game
  (`GS1_Names.json` etc.); evidence detail images get hand-written text descriptions (`EvidenceDetails/GS1/*.txt`).
  Both hot-reload (CLAUDE.md "Services").
- Keys (README "Keyboard Shortcuts"; confirmed as `Input.GetKeyDown` in `InputManager.cs`):
  R repeat last output; I announce current state; `[` and `]` step through hotspots, evidence or targets; U jump
  to next unexamined hotspot; H trial life gauge; F1 context hint or list of all items; F5 hot-reload config.
  R is disabled in modes where the game uses R itself (vase puzzle, court record), per a code comment.
- Repeat scope: only dialogue, narrator and credits text is stored for R, not menu or system text
  (`AccessibilityMod.cs`, `ShouldStoreForRepeatPredicate`).
- Hint commands were unified onto F1 in v1.3.0 (release notes, PR #19).
- Speech: UniversalSpeech with SAPI fallback for players without a screen reader (README intro).
- Settings: no in-game settings menu; behaviour is the shipped JSON data plus the library's static properties.
  [NEEDS VERIFICATION] whether any user-facing verbosity or rate setting exists; none was found in README or CLAUDE.md.
- Player requests in issues:
  - Issue #2 (https://github.com/AccessMods/PhoenixWrightTrilogy/issues/2): a blind player could not present evidence;
    the answer was keys the README did not list (Q to press, Tab to open the court record, E to present). The player
    wrote that "Having these types of events documented in the read me would help" later players.
  - Issue #14 (https://github.com/AccessMods/PhoenixWrightTrilogy/issues/14): a blind Korean player reported names,
    items and court terms missing from speech while visible on screen (checked by OCR). The maintainer shipped a test
    build in the thread, the reporter confirmed, fixed in PR #15. Highlighted words were the pattern. Relevant to #83.
  - Issue #1 (https://github.com/AccessMods/PhoenixWrightTrilogy/issues/1): outside contributors offered name tables
    for six languages and asked for a list of all hard-coded strings to translate.

### Doki Doki Literature Club Plus (DDLCPlusAccess @ 1bd1212)

Source: `README.md`, `CLAUDE.md`.

- Spoken: all dialogue with speaker names, history screen, menus and preferences, poetry minigame word feedback,
  file browser and file viewer, side stories, poems, gallery (full image descriptions), mail app, jukebox,
  system messages.
- Keys: R repeat last dialogue; Up/Down walk history entries (when the history screen has focus) or file lines;
  C data-collection percentage (settings app); P playlist position (jukebox).
- Defaults: "No Configuration Required: Works out of the box with optimal settings"; duplicate-announcement filtering.
- History as a review buffer is the game's own history screen read with arrow keys, not a mod-owned transcript.
- Issues: none open or closed (`gh issue list --state all` returned nothing).

### Shared library behaviour (UnityAccessibilityLib @ 958e20c)

Source: `README.md`, `SpeechManager.cs`, `UniversalSpeechWrapper.cs`, `.github/ACCESSIBILITY_WORKFLOW.md`.

- `SpeechManager.Output(speaker, text, textType)`: formats "speaker: text", drops an identical message inside a 0.5 s
  window (`DuplicateWindowSeconds`), stores narrative text for repeat, speaks, and also sends the same string to a
  braille display (`EnableBraille` default true) through the screen reader.
- `RepeatLast()` replays the stored text (and braille); `Stop()` cancels speech; `TextType` is an int with
  `CustomBase` for per-game types.
- `TextCleaner` strips Unity rich-text tags and supports custom replacements (PWAAT replaces the multiplication
  sign with "x").
- `UniversalSpeechWrapper.Speak(text, interrupt = false)`: default is queue, not interrupt.

## 2. Community and process

- Reporting: GitHub Issues on each mod repo. PWAAT has 4 issues in total (3 closed, 1 open) at HEAD. No issue
  templates exist in PhoenixWrightTrilogy (`.github/` holds only `workflows/format-check.yml`); the CONTRIBUTING
  "Reporting Issues" section asks for game version and episode, steps, expected vs actual, and `MelonLoader/Latest.log`.
- Real-time discussion: a Discord server ("#development" channel) and the audiogames.net forum are named as the
  hubs (`.github/CONTRIBUTING.md`, `ACCESSIBILITY_WORKFLOW.md`). Discord link in the workflow doc is a placeholder
  (`PUT_YOUR_DISCORD_LINK_HERE`). Secondary, not fetched. Issue #1 notes a contributor also messaged the maintainer
  on "Audiogame Hub".
- Releases: GitHub Releases with zip assets; PWAAT has 9 (v1.0.0-beta 2025-12-27 to v1.3.3 2026-03-01), auto-generated
  "What's Changed" lists, with a plain-language note on player impact (v1.3.0: "the only player-facing change being a
  fix to a bug where certain rare lines of dialogue would be skipped").
- Installer: AccessModsInstaller finds the Steam install, installs MelonLoader (pinned to 0.7.1 for PWAAT), downloads
  the latest release and copies the mod DLL, library, UniversalSpeech.dll and nvdaControllerClient.dll
  (`AccessModsInstaller/README.md`). Supports `--non-interactive`, `--prerelease`.
- Game patches / loader drift: issue #25 (https://github.com/AccessMods/PhoenixWrightTrilogy/issues/25, open) reports
  the newest MelonLoader does not work with the game while 0.7.1 does. Maintainer reply: the automated installer pins
  0.7.1, the manual route lets users pick a version, so the fix is to state 0.7.1 in the README. Compatibility is
  handled by pinning the loader, not by in-mod build checks. [NEEDS VERIFICATION] whether any code checks the game
  version; none seen in the files read.
- Testing with blind players: stated as policy, "Does NVDA/JAWS actually speak the new text?" and stereo-panning check
  for sound cues before a PR (`.github/CONTRIBUTING.md`). Issue #14 shows beta builds handed to the reporter inside the
  issue for a test, and the maintainer waiting for the report before merging.
- Contribution rules (`.github/CONTRIBUTING.md`): "Main is for Players" (main always stable); branches such as
  `feature/...`, `fix/...`; PRs strongly preferred even for owners; fork-and-PR for outsiders; follow the repo's
  existing code style (note for AI users to tell the agent to); descriptive commits (`fix: resolve crash when opening
  inventory`); no binaries in git history, use Releases; update the mod README for new features; topics `game-mod`,
  `library`, `stable`/`beta`. New mods enter the org after a "Stability Phase" of public beta feedback on
  audiogames.net or Discord. The guide gives screen-reader navigation hints for GitHub UI steps.
- PWAAT-specific (`CONTRIBUTING.md`): Harmony patches must verify method names against the decompile; `format-check`
  CI workflow; translation validator project (`LocalizationValidator`, `--strict` treats warnings as errors).
- AI use: PWAAT ships a `CLAUDE.md`; the org guide explicitly allows LLM agents with a style instruction.
- Licences: all four code repos MIT (LICENSE files present). The game's DLLs are not distributed; UniversalSpeech.dll
  and nvdaControllerClient.dll are bundled in release zips [NEEDS VERIFICATION of their licences; both third-party].
- Localisation workflow (`TRANSLATORS.md`): per-language folders (`en` fallback, `fr`, `de`, `ja`, `ko`, `zh-Hans`,
  `zh-Hant`, `pt-BR`, `es`) chosen by the game's language setting; `{0}` placeholders in `strings.json`; contributors
  (e.g. PRs #21, #23 for pt-BR) credited in release notes.

## 3. Tools and tradeoffs

- Speech: UniversalSpeech (https://github.com/qtnc/UniversalSpeech) via P/Invoke in `UnityAccessibilityLib`, with
  SAPI fallback and NVDA via `nvdaControllerClient.dll`. The org workflow guide names Tolk as "the gold standard" and
  UniversalSpeech as the alternative that "some have more success with"; no head-to-head data is given. Prism is not
  mentioned anywhere read. Braille goes through the screen reader (`DisplayBraille`), on by default.
- Loaders: MelonLoader (PWAAT, DDLC); BepInEx supported by the library; RE-UE4SS listed for Unreal
  (`ACCESSIBILITY_WORKFLOW.md`). No Reloaded-II mention.
- Hooks: Harmony postfixes on game methods (PWAAT `CLAUDE.md` "Patches"); multiple redundant hooks for dialogue
  ("robust dialogue capture"), and v1.3.0 "Restore Board_Prefix hook with continuation detection" after lines were
  skipped. Hot-path menu cursor tracking uses a coroutine runner.
- Input: polling `Input.GetKeyDown` inside the mod's `OnUpdate` each frame, with mode-dependent gating
  (`InputManager.cs`). No low-level keyboard hook and no warning about one was found in the files read.
  [NEEDS VERIFICATION] whether the org docs warn against one anywhere else.
- Talking over voice acting: no rule found. Default `interrupt = false` queues speech; duplicate window is the only
  suppression. [NEEDS VERIFICATION] whether PWAAT's voiced lines (it has voice clips) are handled; nothing in README
  or CLAUDE.md addresses it.
- Config: JSON for strings and names, plain `.txt` per evidence item, hot-reload on F5; user files live under
  `UserData/AccessibilityMod/`, overriding shipped defaults.
- Build and targets: net35 to match MelonLoader's Mono runtime, hence polyfills (`Net35Extensions`); the library
  multi-targets net6.0, net472 and net35.
- Design philosophy (`ACCESSIBILITY_WORKFLOW.md`): "Translation, Not Transformation": convert visuals to audio, avoid
  mechanical assists except the "Speed of Sound" rule (slow a timer to allow for listening time). Guidance on
  sound cues: pitch-shift beeps as health drops; stereo panning check.
- Tradeoffs visible in the code: static singletons (`SpeechManager`) are simple but share one repeat buffer across all
  text types, hence the predicate hook; the same key (R) means different things per mode and has to be gated.

## 4. Relevance to this repo (suggestions, not decisions)

- #58 repeat key: copy the idea of repeating only narrative text (dialogue, narrator) and leaving menu and system
  text out of the repeat buffer; also gate the key when the game uses it. PWAAT and DDLC both use R. Their buffer holds
  one item; ours may want last-N for #114.
- #114 backlog: DDLC relies on the game's own history screen with Up/Down; PWAAT has no history. A mod-owned
  transcript (our ADR 0004) goes beyond what these mods ship. Nothing here to borrow beyond Up/Down navigation.
- #57 voiced lines: no precedent found; the org queues speech (`interrupt = false`) and does not mention voice-over.
  Treat as an open design question, not a solved one.
- #59 menus: PWAAT announces menu and choice text as typed categories (`Menu`, `MenuChoice`); typed categories make
  per-type rules (repeat, interrupt, verbosity) cheap. Consider the same split.
- #83 placeholders: issue #14 is a concrete failure of exactly this kind, highlighted or coloured spans dropped from
  speech while shown on screen. `TextCleaner` strips tags rather than expanding them; verify our placeholder
  expansion against a diff of English vs another language's output, as the reporter did with logs.
- #104 settings: they ship no settings UI, only defaults plus hot-reloaded JSON. "Works out of the box" is an explicit
  goal. Settings reload without restart (F5) is a cheap, liked feature.
- #115 braille: the org sends every spoken string to braille by default through the screen reader library (one flag).
  Prism's own braille support is not covered here [NEEDS VERIFICATION against ADR 0005].
- Process: a "Reporting Issues" template asking for game build, steps and the log file, and a README key list that
  covers every mode (issue #2) are both cheap wins. Pinning the loader version and saying so in the README (issue #25)
  parallels our build-pinning rule. Beta builds handed to reporters in the thread (issue #14) fit the "testing with
  blind players" step.
- Possible contact: the org takes new mods only after community testing; sharing our notes there is optional and
  would go through the maintainer, not decided here.

## Not confirmed

- Any content from Discord, audiogames.net, the static site (`AccessMods.github.io`) and the org profile README.
- Voice-over handling, in-game settings, game-version checks, and which of the bundled DLLs' licences apply.
- The `.github/ACCESSIBILITY_WORKFLOW.md` content is guidance prose; it was read in full but the practices described
  are not verified against the mods' code beyond what is cited above.
