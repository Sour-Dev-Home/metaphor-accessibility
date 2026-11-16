# Persona 4 Golden Access: a screen-reader mod for another Atlus game

- **Source:** <https://github.com/AquaRose7/Persona-4-Golden-Access> (GPL-3.0; author credited as "Haru"), read through
  `gh api repos/AquaRose7/Persona-4-Golden-Access`.
- **Saved:** 2026-11-16
- **Game build:** none (this note is about another project's mod, not Metaphor: ReFantazio)

- **HEAD commit read:** `dcff25536710cc777c079a02422a5dc30bcb1e87` (2026-10-18, "release: v2.2.6"). The default branch
  is `main`; the history is 14 commits; repo created 2026-06-24; 12 stars, 3 forks, 3 open issues, Discussions off.
- **Latest release read:** v2.2.6, <https://github.com/AquaRose7/Persona-4-Golden-Access/releases/tag/v2.2.6>, with
  assets `P4G.Access.Installer.exe` (47.9 MB) and `Persona.4.Golden.Access.v2.2.6.zip` (14.5 MB). The zip's own
  `README.md` and `Mods/p4g.golden.access/ModConfig.json` were read from the downloaded asset and are cited as
  "release zip".
- Release list: <https://github.com/AquaRose7/Persona-4-Golden-Access/releases> (v1.1.0 on 2026-06-25 to v2.2.6 on
  2026-10-18, 12 releases).
- Paths below are at the SHA above. `M/` means `p4g64.accessibility-master/p4g64.accessibility/`.
- Not run: no game, no mod, no binary from the zip was executed. Only the zip's text files were read.
- The repo's `README.md` says "Latest release: v2.2.1" while the latest tag is v2.2.6, so the README lags the releases.

The mod is "built on AnimatedSwine37's accessibility mod template" and on
<https://github.com/AnimatedSwine37/p4g64.accessibility> (`README.md`, `COPYRIGHT`). It is the closest precedent for us:
an Atlus game, loaded by Reloaded-II, speech through Prism. Metaphor is a different engine, so nothing about P4G's
offsets or structs transfers (ground rule 1); the design lessons do.

## 1. Features

All claims: `README.md` (repo), release zip `README.md`, and `database/*.md` at the SHA unless another path is given.

- **Dialogue:** speaks each message page once, with the speaker name prefixed ("Name: line"). `M/Components/Dialogue.cs`.
  A leading ">" (narration marker) is stripped (`SanitiseDialog`).
- **Speaker names:** read from the dialogue window's name-plate text object (`SpeakerNameText`), only when the box or
  speaker changes. `M/Components/Dialogue.cs`, `M/Native/Text/Dialog.cs` (`SpeakerNameText` at offset 0x20).
- **Reply choices:** a short cue sound when a choice list appears, then the highlighted option on each move.
  `M/Components/Dialogue.cs` (`SpeakSelection`, `PlayChoiceCue`). The cue's volume is a setting.
- **Menus:** "Most menus are read out loud" (release zip README). Each screen has its own reader class under
  `M/Components/` (title menu, Config, shops, Velvet Room fusion, status, camp, name entry, TV listings, save slots and
  more). The Config menu reads the game's own label table (`M/Native/Text/GameText.cs`).
- **Battle:** "fully spoken": commands, skills, targets with discovered affinities, turn owner, damage, enemy actions,
  status, personas, results, Shuffle Time. `database/BATTLE_SYSTEM.md`, `M/Components/Battle/`.
- **Navigation:** the largest part. Overworld and dungeon target browsers, auto-walk, a sound beacon along the route,
  wall hum and bump sounds, a shadow radar, optional HRTF 3D sound (v2.2.6). Release zip README, `database/OVERWORLD*.md`,
  `database/DUNGEON_*.md`.
- **Cutscenes:** speaks the on-screen subtitles and optional, hand-written visual descriptions synced to the movie, with
  the player's typed hero name substituted for `{player}` (`M/Components/MovieDescription.cs`; the canon name "Yu" until
  a name exists). Authored descriptions ship as `game seens/*/seen.txt`.
- **Tutorials:** the game's own help text is not what `M/Components/Tutorial.cs` speaks. That class is the mod's own
  onboarding: short tips fired once at the right moment, "shown as real in-game help bubbles", with seen tips saved to a
  file. A built-in Help section lists every control and game tips (`database/SETTINGS_MENU.md`).
  Reading the game's own tutorial pop-ups: `M/Components/SystemMessage.cs` and `M/Components/Battle/MessageBubble.cs`
  exist; whether they cover every tutorial pop-up is `[NEEDS VERIFICATION]`.
- **Backlog:** reads the game's dialogue backlog (open with X): each scroll speaks the selected line, plus a "Voiced." tag
  for a voiced line; Space replays the voice (the game plays it). `database/BACKLOG_READER.md`,
  `M/Components/BacklogReader.cs`.
- **Repeat and history keys:** Shift+P repeats the last spoken line; Shift+[ and Shift+] step through the last 20 lines;
  Shift+M toggles the dialogue auto-reader. Controller: LT+RT with the d-pad. `M/Components/HistoryKeys.cs`,
  `M/Components/Speech.cs` (`Max = 20`).
- **Voiced lines:** one switch, no per-line voice detection. With the dialogue reader off ("voice mode", Shift+M) a line
  is recorded to the history but not spoken, so the game's voice is audible, and the player pulls the text with the repeat
  key. `M/Components/Dialogue.cs` (`ReaderEnabled`, `Speech.Record`). Choice lists are always spoken.
- **Placeholders (player name, items):** the dialogue hook reads text the game has already laid out on screen
  (`TextStruct.Lines` to `TextCharacter`, `M/Native/Text/Text.cs`), so name tags are already filled in. No placeholder
  substitution step exists in what was read. Button icons as words: not found, `[NEEDS VERIFICATION]`.
- **Languages:** speech strings are English. Game text is read in the game's language through five glyph tables
  (`M/P4G_EFIGS.tsv`, `_JP`, `_CHS`, `_CHT`, `_Korean`) and the game's own language id
  (`M/Native/Text/GameText.cs`). Release v2.2.5+ says "Item names are read in Japanese, Korean, Chinese, French,
  German, Italian and Spanish" (release zip README). The v2.0.0 notes said English only; the zip README still says
  "currently only works with the game's language set to English".
- **Defaults and settings:** `F1` (or LT+RT+Start) opens a mod-drawn, spoken settings menu: sound volumes (0-200% by
  10), cursor modes, three reader toggles (dialogue on, subtitles off, descriptions on), Windows voice on/off, Help, and
  Restore Defaults (double Enter). Settings save at once to `mod_settings.json` in the mod folder.
  `database/SETTINGS_MENU.md`, `M/ModSettings.cs`. The Reloaded-II config UI (`M/Config.cs`: AnnounceDialogue,
  AnnounceFieldLocation, AnnounceBattle, AnnounceSaveSlots, DebugEnabled) is the template's, so players use the in-game
  menu instead. A failed save speaks "Settings could not be saved." once.
- **Controls:** keyboard keys plus a controller layer behind LT and RT as modifiers (`M/Components/ControllerInput.cs`).
  The release zip README tells Xbox pads to turn Steam Input off and PlayStation pads to turn it on.

## 2. How it finds things in the game

- **Hook points:** hand-picked functions, each with a byte signature scanned at startup (examples in
  `M/Components/Dialogue.cs`):
  `MsgWindow::DrawDialog` (a signature starting `48 89 5C 24 ?? 48 89 6C 24 ?? 57 48 83 EC 20 ...`) and
  `MsgWindow::StartDialog`; `TitleMenu::Render` in `M/Components/TitleMenu.cs`. Other readers hook functions named by
  their Ghidra labels (`FUN_14021E2A0`, `FUN_140265060`, `FUN_14014f110` in `M/Mod.cs` comments) at hard-coded
  addresses, for example the UI text renderer `0x140450C60` in `M/Components/UiTextSpy.cs`.
- **How they were found:** Ghidra decompilation plus a "snapshot method": the sighted-or-blind player holds a screen
  still, an external tool snapshots process memory, one input later another snapshot, and the diff gives cursor and
  field values (`database/SNAPSHOT_METHOD.md`, read-only `ReadProcessMemory`, "far safer than Frida, which has crashed
  P4G"). It states "Arxan hides xrefs", so static cross-references were unreliable. The backlog was found the same way,
  "the live-hunt loop (blind user focuses a line + tells the text on screen)" (`database/BACKLOG_READER.md`). That doc also
  uses the game's named task registry (`itfBacklogDraw`) as "the universal silent-screen anchor" for screens with no
  hookable draw function.
- **Pinning to game versions:** none found. Signatures are scanned with Reloaded's SigScan mod
  (`M/Utils.cs` `SigScan`, dependency `Reloaded.Memory.SigScan.ReloadedII` in `M/ModConfig.json`); a miss logs "Unable to
  find {name}, stuff won't work :(" and the feature stays silent. Many addresses are constants because "P4G PC 64-bit,
  ASLR disabled" (`README.md`). A search of `M/Mod.cs` and `M/Utils.cs` for a hash, file-version or build check found
  none; a check elsewhere is `[NEEDS VERIFICATION]`. Game updates: not addressed in the docs (P4G Golden's PC release is
  old and stable; whether it still patches is not stated).
- **Reading text:** the dialogue window object holds `TextStruct` lines of 2-byte glyph codes; they are decoded through
  an `AtlusEncoding` class "based on Atlus Script Compiler's encoding stuffs" (`M/Native/Text/AtlusEncoding.cs`) and the
  per-language TSV tables. This is the rendered text, not the BMD/MSG file. The backlog is the exception: it walks to the
  message block and decodes Atlus MSG runs itself, skipping 2-byte function tokens (including the voice code F5), turning
  0x0A into a space, and ending at 0x00 (`database/BACKLOG_READER.md`).
- **Menu focus:** no single focus point. Each screen has its own hook or poll: a render-function hook that reads a cursor
  value (title menu, cursor 0-5 mapped to fixed labels), a dispatcher hook (Velvet Room root menu), a polled task
  (backlog, TV listings), or the dialogue selection fields (`SelectedOption`). The dev-only `UiTextSpy` hooks the shared
  UI-text renderer and logs every drawn string with its colour, because "the tactics member cursor exists only as a
  render-state color flip" (`M/Components/UiTextSpy.cs`, Debug builds only).
- **Input:** keyboard polling on a background thread, 40 ms, with `GetAsyncKeyState` and a game-has-focus check
  (`M/Components/HistoryKeys.cs`). The controller is read through XInput, and the mod also hooks the game's own pad
  function (`FUN_140505D50`) to zero the game's buttons while a trigger modifier is held. It synthesises keyboard keys with
  `keybd_event` so every feature has one key path (`M/Components/ControllerInput.cs`). Known problem: open issue #2
  (2026-09-30) reports that pressing Numpad 0 while navigation was stuck made the game announce "Append" (the D key), and
  asks to stop navigation keystrokes triggering unrelated commands
  (<https://github.com/AquaRose7/Persona-4-Golden-Access/issues/2>).
- **Safety in code:** every unsafe read goes through `ReadProcessMemory` (`TryReadRaw`, `M/Utils.cs`) because "an
  AccessViolationException cannot be caught in .NET" (`README.md`); global exception handlers log a stack trace
  (`M/Mod.cs`). Hook delegates take `int` arguments (`StartDialogDelegate(int, int)` in `M/Components/Dialogue.cs`) and
  each hook is stored in a field only after `Activate()` returns. Neither matches our architecture rules 9 and 11.

## 3. Speech and tooling

- **Library:** Prism (`ethindp/prism`, MPL-2.0), replacing Tolk in v2.2.1 (`M/Components/Voice/PrismOutput.cs`,
  `PrismNative.cs`; release v2.2.1 notes). `prism.dll` ships next to the mod DLL and is loaded first by full path
  with `NativeLibrary.TryLoad`, then the P/Invokes bind by bare name (`PrismNative.TryOpen`).
- **Backends:** takes every backend Prism lists, in Prism's priority order, and starts the first that initialises. It
  skips SAPI and OneCore when the "Windows voice" setting is off. It does not exclude ZDSR, PC-Talker or Boy PC Reader, so
  it has the bare-name DLL load our ADR 0005 avoids. It re-picks when Prism's availability callback fires and when a
  line fails, then speaks the line again once. It says Prism's Tolk shim "fell back to SAPI when NVDA closed and never went
  back", hence the direct calls. If the screen reader closes it falls back to a Windows voice, and returns when it runs
  again (release v2.2.1 notes).
- **Braille:** calls `prism_backend_output` (speech plus braille where the backend supports it) and falls back to
  `prism_backend_speak` when the backend returns NOT_IMPLEMENTED (`PrismNative.Output`). No braille setting found.
- **Threads:** `Speech.Say` is called from game-thread hooks and from polling threads; one lock guards Prism ("Backends
  aren't thread-safe"). Calls are synchronous.
- **Interrupt vs queue:** `Say(text, interrupt = true)`. A "grace rule" protects some sources (zone names, check labels,
  damage, enemy actions): a protected line opens a window of 400 ms + 55 ms per character, clamped to 1.2-3.5 s, and an
  interrupting line inside it is downgraded to queued (`M/Components/Speech.cs`).
- **Duplicates:** an identical line within 300 ms is dropped only in battle, because the guard muted deliberate re-reads in
  menus; the history skips consecutive duplicates; manual repeat bypasses the guard (`M/Components/Speech.cs`). Each
  spoken line is logged with the source file name through `CallerFilePath`, to find speech spam in a player's log.
- **Text normalising:** every announcement has the ideographic space U+3000 replaced with a space, "some screen readers
  stumble on it" (`Speech.Normalize`).
- **Dependencies (Reloaded-II mods):** `reloaded.sharedlib.hooks`, `Reloaded.Memory.SigScan.ReloadedII`,
  `p5rpc.modloader`, `reloaded.universal.fileemulationframework.bf` (File Emulation Framework, BF Emulator) and
  `p4g64.customSubMenu` (`M/ModConfig.json`). The release zip bundles these plus the FEMU awb/bmd/pak/spd emulators, CRI FS
  hooks and the Localisation framework (the zip's `Mods/` folder names). `README.md`: the BF Emulator and Custom Sub Menu
  are "load-bearing for navigation; without them the FlowScript-based features silently no-op". It ships a FlowScript file
  (`M/FEmulator/BF/field.flow`). NAudio plays the mod's own sounds.
- **Build:** plain `dotnet build` of one `net9.0-windows` project, copy the DLL into the mod folder. "There are no
  automated tests; verification is manual" (`README.md`). No CI workflow files in the tree. Logs go to
  `%APPDATA%\Reloaded-Mod-Loader-II\Logs`.
- **Release:** a zip containing Reloaded-II's setup, every mod needed and a ready-made `Apps/p4g.exe/AppConfig.json`
  (to skip Reloaded's mod-enabling screen, "which isn't screen-reader friendly"), and from v2.1.0 an installer
  `P4G Access Installer.exe` that "finds the game, installs Reloaded II if needed, downloads the mod and sets everything
  up. It is screen-reader friendly and needs no menus or mouse" (v2.2.6 release notes). The installer's source is not in
  the repo tree. Updating: run the installer again, or copy the mod folder over the old one.
- **Install for blind players (release zip README, "How to install"):** run the Reloaded-II setup; read
  `%APPDATA%\Reloaded-Mod-Loader-II\ReloadedII.json` in Notepad for `ModConfigDirectory` and `ApplicationConfigDirectory`;
  copy the `Mods` folders and the `p4g.exe` app folder in (no typing); fix `AppLocation` in `AppConfig.json` if Steam is
  not at the default path. Then: "**Don't launch the game from Steam.** Starting from Steam often fails to load some of
  the mods", and make a desktop shortcut with the NVDA review-cursor steps below.
- **Licence handling:** GPL-3.0 and `COPYRIGHT` credit the template author. `THIRD-PARTY-NOTICES.txt` ships in the mod
  folder; the repo carries Prism's licence files under `p4g64.accessibility-master/Submodules/Prism/licenses/`.
- **Game-derived data in the repo (observation only):** `database/` commits JSON tables and Python tools that unpack
  game archives (`database/tools/ArcExtractor.py`, `Pm1BmdExtract.py`, `decode_collision.py`), and
  `M/FEmulator/BF/field.flow`. Whether any file contains game content verbatim was not checked, `[NEEDS VERIFICATION]`.
  Our ground rule 4 would not allow this pattern without that check.

## 4. Community

- **In the repo:** Issues are on, Discussions are off. Three issues, all open, none answered (0 comments):
  #1 a Chinese localisation contribution from a user whose blind Traditional Chinese player tested it with NVDA on the
  CHS version; #2 navigation and interaction problems from a blind player; #3 from "a low vision user working on an
  accessibility mod for SMT 3 HD Nocturne" asking for menu description text
  (<https://github.com/AquaRose7/Persona-4-Golden-Access/issues/1>, `/2`, `/3`). Contributors listed: one account
  (`HARUDEN-7`, 14 commits).
- **Discord or forum:** none linked in the README, release notes or zip README. The only mention found is the Discord
  relay below. A release note says the author played the game start to finish with the mod (v2.0.0 notes).
- **Testing with blind players:** the docs record "user-verified" dates for features and describe a loop where a blind
  user reports what is on screen while the author scans memory (`database/BACKLOG_READER.md`,
  `database/SETTINGS_MENU.md`, "Haru's own tuning"). Whether the author is blind or works with named blind testers is
  `[NEEDS VERIFICATION]`. Release notes ask players to send the newest log from the Reloaded logs folder when speech reads
  wrong (v2.2.6 notes).
- **Release cadence:** 12 releases in about four months; every note lists player-facing changes, then install and update
  instructions.

## Relayed by the maintainer from the author on Discord on 2026-11-16 (secondary, not independently checked)

The author's player instructions for a Reloaded-II desktop shortcut with NVDA, as relayed:

1. Open Reloaded-II.
2. Tab to the application list and open the game.
3. Use NVDA's review cursor (Shift+NVDA+arrows) to reach "Create Shortcut" in the app's page.
4. Move the mouse there (Shift+NVDA+M) and click (NVDA+numpad slash, or NVDA+[).
5. Always launch from the "(Reloaded)" desktop shortcut.
6. Alternative: keep Reloaded-II running and press Play in Steam. The shortcut is the more reliable way.

The relay agrees with the release zip README's "How to install, step 5" (a primary source, read above), which gives the
same steps in more detail and the shortcut name "Persona 4 Golden (Reloaded)". They differ on Steam: the zip README says
"Don't launch the game from Steam", while the relayed alternative allows Steam with Reloaded-II running. Which is right
for P4G, and whether it holds for Metaphor, is `[NEEDS VERIFICATION]`.

What this implies for us (a suggestion, not a decision):
- Reloaded-II's own window is reachable with NVDA only through the review cursor and a simulated mouse click. Our player
  install docs (`docs/players/`, still empty) should not assume a player can tab to "Create Shortcut", enable a mod in
  its UI, or edit a setting in it. The P4G answer is a pre-made `AppConfig.json` copied in, no typing.
- For #104, a settings file in Reloaded-II's per-mod user-config folder is fine to read, but the Reloaded-II config page
  is not a usable way for a blind player to change it. The setting needs another route: an in-game spoken settings menu
  like P4G's F1, or a plain file with documented edits and a spoken confirmation when it changes.
- The launch route matters: whether Steam-launched play loads our mod reliably is something to test on Metaphor.

## 5. Compared with this repo

Our side is read from `AGENTS.md`, `docs/adr/0003` to `0006`, `docs/coding-standards.md` and the open issues at
`origin/main` `f2ce54a`.

| Area | P4G Access | This repo | Verdict for us |
|---|---|---|---|
| Architecture | One project; features (`Components/`) call each other's statics and hook the game directly; about 100 files of game logic beside raw memory | Plain `net9.0` libraries plus one thin hook layer, features independent, architecture tests (ADR 0003) | To our benefit: logic is unit-testable and the unsafe code is one reviewable place |
| Tests | None, "verification is manual" | Unit, round-trip, architecture, smoke, mutation tests | To our benefit: regressions are caught without launching the game |
| Hook safety | Reads guarded by `ReadProcessMemory`; `int` hook arguments; hook field set after `Activate`; failures logged | Pointer-sized delegates, hook stored before activate, reads only through `ModuleMemoryReader`, build check by the loader (rules 8-12) | To our benefit: avoids the fail-fast and garbage-argument cases |
| Signature and build pinning | SigScan at startup, hard-coded addresses, no build check found; a miss only logs | Signatures in `hook-signatures.json` match exactly once, per named game build, loader refuses on a mismatch (ADR 0006) | To our benefit, and it matters more for Metaphor, which can still patch. P4G's cheap approach is neutral for a frozen game |
| Finding text | Reads rendered glyph lines from the dialogue window; the backlog decodes MSG runs by hand | Hooks the message-request functions and reads BMD/MSG messages with a text cleaner | Neutral: rendered text has names and items filled in, ours is cleaner but needs #83 and #106 |
| Speaker names | Name-plate text object (already the displayed alias) | Message file's `<NS>` name, which can spoil an alias (#74, #81) | To our detriment today: P4G reads what is shown; ours must find the alias source |
| Menus and focus | One hook or poll per screen, no shared focus point; render and task anchors | Looking for one focus point first (#93), then per-screen fallbacks (#59) | Neutral: P4G shows a shared point may not exist; our spike asks before building |
| Choices | Reads `SelectedOption`; cue sound when a list opens; one line per move | #82 and #59: options on open, focus on move, "<label>, <n> of <total>" | To our benefit: planned count and position; P4G's cue sound is worth copying |
| Voiced lines | Binary reader switch (Shift+M), history keeps the line, backlog tags "Voiced." | Four-mode setting with Auto by voice language, tag-based (#57, #104, #103) | To our benefit if #103 confirms the tag; P4G's switch is simpler and always works |
| Placeholders | None needed (rendered text) | Text cleaner fills tags from message files and memory (#83, #106, #107) | To our detriment: extra work P4G avoided by reading drawn text |
| Input | 40 ms `GetAsyncKeyState` polling, XInput, synthesised keys, hooks the pad function; known stray-key bug (issue #2) | Hook the game's input handling, no polling, no low-level hook (#58) | To our benefit: no synthesised keys, no stray commands |
| Settings | In-game spoken F1 menu, flat `mod_settings.json`, instant save | Per-mod user-config file in Reloaded-II's folder, watched (#104) | To our detriment for blind players: P4G's route needs no inaccessible UI. The file watch is neutral |
| Speech backends | All Prism backends, first that starts, re-pick on change, falls back to a Windows voice | Fixed list NVDA, JAWS, ZoomText, OneCore, SAPI; hash-checked, no bare-name loads (ADR 0005) | To our benefit for safety; P4G's reconnect and fallback behaviour is worth matching |
| Braille | `prism_backend_output` (speech and braille), fallback to speak | `prism_backend_speak` only (`src/Metaphor.Hooks/PrismSpeech.cs`), #115 open | To our detriment today; a small change |
| Interrupt and duplicates | Grace window protects key lines; battle-only 300 ms duplicate guard; consecutive-duplicate skip in history | Announcer with interrupt flag, `RepeatGate` | Neutral: P4G's rules came from player complaints and are worth reading before we tune ours |
| Release | Zip with every mod plus prepared `AppConfig.json`, plus a self-installing installer, no CI | CI-built package with SHA-256 and byte-identical rebuild; our own mod only, `reloaded.sharedlib.hooks` already loaded by the loader (ADR 0006) | To our benefit for trust; P4G's install is further along for blind players, since ours has no player docs yet |
| Install docs | NVDA review-cursor steps, no-typing config copy, installer | `docs/players/` empty | To our detriment today |
| Licensing hygiene | GPL-3.0, credits, third-party notices; commits game-derived data tables | GPL-3.0, ground rule 4, pinned third-party licences | To our benefit |

### What to borrow (suggestions, not decisions)

- **#115 braille:** call Prism's `output` entry and fall back to `speak` on NOT_IMPLEMENTED, as in
  `M/Components/Voice/PrismNative.cs`. It needs a new signature in our Prism wrapper and a check against ADR 0005's list.
- **#57, #58, #104 voiced lines and repeat:** keep a short history (P4G: 20 lines, `Speech.Record` stores a line without
  speaking it) so any voiced-lines mode can still be repeated; keep one reader on/off key as the fallback if Auto proves
  unreliable (`M/Components/Dialogue.cs`).
- **#59 choices:** play a short cue when a choice list opens and detect "a new menu" by the list object changing, so the
  first option is always read (`M/Components/Dialogue.cs` `SpeakSelection`). Make cue volume a setting.
- **#93 menu focus:** P4G found no single point. Its leads: the named task registry as an anchor for screens with no
  draw hook (`database/BACKLOG_READER.md`), and a dev-only hook on the shared UI-text renderer that logs strings with
  colour, since a highlight can exist only as a colour (`M/Components/UiTextSpy.cs`). Both are leads to check for
  Metaphor, `[NEEDS VERIFICATION]` there.
- **#114 backlog:** the game's backlog is gated on a state bit, can open empty and fill a frame later, and has per-line
  voice ids (`database/BACKLOG_READER.md`). Expect the same traps: require a non-empty count before speaking.
- **#74, #81 speaker names:** P4G reads the name-plate text the game draws; check whether Metaphor's drawn name text is
  reachable instead of reconstructing the alias, and treat the narration marker as "no name".
- **#83, #106 placeholders:** P4G avoids tags by reading text after the game has laid it out. Check whether Metaphor has a
  drawn-text point where the player's name is already filled in, as a fallback for the run-time values.
- **#118 tutorials:** its onboarding tips use real in-game help bubbles; Metaphor's tutorial pop-ups may need the same
  kind of per-screen reader. `[NEEDS VERIFICATION]` for Metaphor.
- **Speech hygiene:** normalise U+3000 to a space before speaking, tag each spoken line with its source in the dev log, and
  adopt a grace window for lines that must not be cut off (`M/Components/Speech.cs`). Keep the battle-only dedupe lesson:
  a global duplicate guard muted deliberate re-reads.
- **Install docs (no issue yet):** a copy-in `AppConfig.json` that enables the mod, since Reloaded-II's UI is hard to use
  with NVDA, and clear launch advice after testing it. The Discord relay above applies.
- **Input warning (#58):** if we ever synthesise keys, note issue #2's stray-key bug; our plan to hook input avoids it.
