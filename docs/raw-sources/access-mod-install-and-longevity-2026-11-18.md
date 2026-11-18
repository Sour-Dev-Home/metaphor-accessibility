# Installing access mods by ear, and what keeps them alive

- **Source:** GitHub repositories, releases and discussions read through the GitHub API (`gh api`, `gh release view`,
  `gh search`) on the date below; web pages read with a fetch tool on the same date. Each claim names its URL or repo
  path. Repositories read at their default branch on that date unless a commit is given.
- **Saved:** 2026-11-18
- **Game build:** none (this note is about Reloaded-II and other games' mods, not Metaphor: ReFantazio)
- **Not reached:** forum.audiogames.net returned HTTP 403 to the fetch tool, so no thread body there was read. Thread
  URLs and quotes below come from the audiogames.net monthly digest, which quotes the threads. Anything that would need
  a thread's text is `[NEEDS VERIFICATION]`. Nothing was downloaded, run, posted or joined.

`R` below means <https://github.com/Reloaded-Project/Reloaded-II/blob/458987f26bc8d78fdc0f86b73fc0d6fd4b673d01>
(`master` on 2026-11-18; latest release 1.31.0, 2026-09-23, assets `Setup.exe`, `Setup-Linux.exe`, `Release.zip`,
`Tools.zip`).

## 1. Reloaded-II with a screen reader

### The launcher window

- No issue in Reloaded-II's tracker matches "screen reader", "NVDA", "blind" or "keyboard navigation"; "accessibility"
  matches one unrelated issue (#323). `gh search issues --repo Reloaded-Project/Reloaded-II`.
- Discussions: the only screen-reader hit is the maintainer's 1.27.10 changelog post in the update thread
  (<https://github.com/Reloaded-Project/Reloaded-II/discussions/369#discussioncomment-9695170>).
- History, from release notes:
  - 1.20.0 (2022-08-24): "Improved: Overall UI performance by disabling WPF UI automation."
    (<https://github.com/Reloaded-Project/Reloaded-II/releases/tag/1.20.0>)
  - 1.27.9 (2024-06-04): "re-enables WPF Automation Functionality to improve accessibility for users who require the
    use of assistive screen readers ... I originally assumed that the functionality was only used for debugging
    purposes." (<https://github.com/Reloaded-Project/Reloaded-II/releases/tag/1.27.9>)
  - 1.27.10 (2024-06-07): "labelling many previously unlabeled items throughout the core UI ... It's not going to be
    perfect, but I hope it's just a little better." Commit `b29f49f` "Improved: Accessibility for Screen Reader Users"
    touches 10 files (<https://github.com/Reloaded-Project/Reloaded-II/releases/tag/1.27.10>).
- Today `AutomationProperties` appears in 4 XAML files (`AppSummaryPage`, `ManageModsPage`, `DownloadPackagesPage`,
  `SettingsPage`); the ones read name search and tag boxes (`R/source/Reloaded.Mod.Launcher/Pages/BaseSubpages/
  ApplicationSubPages/AppSummaryPage.xaml#L48`, `#L55`). Whether NVDA reads the mod list's check boxes is
  `[NEEDS VERIFICATION]` by ear.
- Practice from the one Reloaded-II access mod: P4G Access ships a ready-made `AppConfig.json` to skip the
  mod-enabling screen, "which isn't screen-reader friendly", and its shortcut steps use NVDA's review cursor and a
  simulated click ([persona-4-golden-access-2026-11-16.md](persona-4-golden-access-2026-11-16.md)). Narrator and
  JAWS: no source found, `[NEEDS VERIFICATION]`.

### Installing Reloaded-II and its runtimes without the GUI

- `Setup.exe` (the WPF installer, `R/source/Reloaded.Mod.Installer/Program.cs#L6-L8`) first calls
  `Cli.TryRunCli(args)`. With `--nogui` it runs the console install instead of the window; with `--dependenciesOnly` it
  installs runtimes only (`R/source/Reloaded.Mod.Installer.Cli/Cli.cs#L15-L37`).
- `Setup-Linux.exe` is the same console installer built for `win-x64` (`AssemblyName` `Setup-Linux` in
  `R/source/Reloaded.Mod.Installer.Cli/Reloaded.Mod.Installer.Cli.csproj#L8`; `R/source/Publish.ps1#L83-L84`).
- Flags (`R/source/Reloaded.Mod.Installer.Lib/Settings.cs#L17-L31`): `--installdir <path>`, `--nogui`,
  `--nocreateshortcut`, `--nostartreloaded`. The default folder is the Desktop, unless it is cloud-synced
  (`#L9`, `#L35-L40`). The loop runs to `args.Length - 1`, so the last argument is never read as a flag; `--nogui`
  still works anywhere because `TryRunCli` scans all arguments. Put `--nogui` last. Reading of the code, not a run:
  `[NEEDS VERIFICATION]`.
- Runtimes: the installer reads every `*.runtimeconfig.json`, downloads each missing .NET runtime and runs it with
  `/install /quiet /norestart`; it does the same for the Visual C++ x64 and x86 redistributables
  (`R/source/Reloaded.Mod.Installer.DependencyInstaller/DependencyInstaller.cs#L28-L98`). No runtime installer window
  should appear. UAC prompts are `[NEEDS VERIFICATION]`. Errors use a Win32 `MessageBox`
  (`R/source/Reloaded.Mod.Installer.Lib/MainWindowViewModel.cs#L54`, `#L137`), which screen readers read as a
  standard dialog `[NEEDS VERIFICATION]`.

### Launching and enabling mods without the GUI

- `Reloaded-II.exe --launch "<game exe>"` starts a registered application with its saved `EnabledMods`; no argument
  selects mods, and an unregistered exe probably throws
  ([reloaded-ii-launch-and-logs-2026-11-13.md](reloaded-ii-launch-and-logs-2026-11-13.md)).
- Registration and the mod set are plain JSON: `AppConfig.json` (`AppId`, `AppLocation`, `EnabledMods`) under
  `ApplicationConfigDirectory`, mods under `ModConfigDirectory`, both named in
  `%APPDATA%\Reloaded-Mod-Loader-II\ReloadedII.json` ([reloaded-ii-app-and-mod-config-2026-11-15.md](reloaded-ii-app-and-mod-config-2026-11-15.md)).
  A script can write all three. Whether `ReloadedII.json` exists before the launcher's first run is
  `[NEEDS VERIFICATION]`.
- Mod packs (`.r2pack`, `--r2pack`, `--r2packdl`) still open a GUI wizard
  (`R/docs/InstallingModPacks.md`; `R/source/Reloaded.Mod.Launcher.Lib/Startup.cs#L41-L61`), so they don't help.

## 2. How other access mods install

| Mod | Loader | Install path for blind players | Source |
|---|---|---|---|
| Stardew Access | SMAPI | "Accessible Stardew Setup" (Python GUI, "screen reader-accessible graphical interface", installs SMAPI, Kokoro, Project Fluent and the mod, optionally sets Steam launch options); manual route uses SMAPI's console `install on Windows.bat` ("type "2" to enter a custom game path") | <https://github.com/stardew-access/stardew-access/blob/development/docs/setup.md>; <https://github.com/kataquin/AccessibleStardewSetup> (2 releases, last push 2024-05-05) |
| Factorio Access | own launcher | `launcher.exe` placed in the game folder, speaks through the system voice and "provid[es] an accessible alternative to the graphical main menus"; downloads the mod; Steam users paste a launch-options string. README warns the launcher "is currently flagged as a virus". A `FactorioAccessInstaller.exe` now ships with builds | <https://github.com/Factorio-Access/FactorioAccess> README; <https://github.com/Factorio-Access/Factorio-Access-Launcher>; release `ci-latest` assets |
| Hearthstone Access | patched game files | Community patcher by leibylucw: pick folder and channel, "Press the "Patch Hearthstone" button"; manual: copy three folders. "has to be installed again every time Hearthstone itself is updated" | <https://www.hearthstoneaccess.com/downloads.html>; <https://github.com/leibylucw/hearthstone-access-patcher> |
| Say the Spire 2 | game's own mod support | Installer finds the game, enables mods in the game's settings file, asks whether to enable screen-reader support and whether to disable Godot UIA ("Recommended Yes for ... NVDA users"); update is a button in the installer; ships JAWS config files | <https://bradjrenshaw.github.io/say-the-spire2/installation.html> |
| Persona 4 Golden Access | Reloaded-II | Installer that "finds the game, installs Reloaded II if needed ... needs no menus or mouse"; zip with a ready-made `AppConfig.json` | [persona-4-golden-access-2026-11-16.md](persona-4-golden-access-2026-11-16.md) |
| AccessMods (Phoenix Wright, DDLC+) | MelonLoader | AccessModsInstaller, pins MelonLoader 0.7.1, supports `--non-interactive` | [accessmods-org-survey-2026-11-16.md](accessmods-org-survey-2026-11-16.md) |
| FF6 Screen Reader (pixel remaster) | MelonLoader | Manual: MelonLoader's installer ("You may need to use NVDA object navigation during installation"), copy the DLL and `Tolk.dll` | <https://github.com/BlindGuyNW/FF6ScreenReader> README |
| Pokémon Access (Crystal; Gen 3 fork) | VBA-ReRecording Lua | Manual: load `pokemon.lua` from the emulator's Lua window; fork links the emulator on mega.nz | <https://github.com/tspivey/pokecrystal-access>; <https://github.com/nuive/pokemon-access> |

Celeste and Persona 5 Royal: no screen-reader mod found on GitHub (`gh search repos`), `[NEEDS VERIFICATION]` elsewhere.

Pattern: every mod still played in 2026 has, or is getting, a one-step installer that also installs the loader. Manual
routes survive as fallbacks and assume NVDA object navigation for third-party GUIs.

## 3. What keeps them going

### Release cadence (GitHub releases per year, `gh api repos/<r>/releases`)

| Mod | Releases by year | Latest |
|---|---|---|
| Stardew Access | 2021: 8, 2022: 14, 2023: 13, 2024: 10, 2025: 5, 2026: 1 | v1.7.0-beta.2, 2026-05-08 |
| Factorio Access | 2023: 12, 2024: 31, 2026: 1 (`ci-latest`); `CHANGES.md` lists 0.16.46 to 0.16.57 from 2025-12-21 to 2026-03-04, so tags are published elsewhere `[NEEDS VERIFICATION]` | 0.16.57 |
| Factorio Access launcher | 2024: 9, 2025: 21, 2026: 12 | 2.0.36, 2026-06-13 |
| Hearthstone Access (original) | 2021: 20, 2022: 26, then none | 2022-11-04 |
| Say the Spire | 2020: 2, 2021: 4, 2022: 4, 2023: 4 | 0.5.0-beta, 2023-05-24 |
| Say the Spire 2 | 2026: 37 (since 2026-03-09) | v1.6.0, 2026-11-13 |
| P4G Access | 2026: 12 | v2.2.6 |
| FF6 Screen Reader | 2025: 5, 2026: 2 | v2.0, 2026-03-17 |

### Maintainers and bus factor (top contributors by commits, `gh api repos/<r>/contributors`)

- **Stardew Access:** khanshoaib3 990, kataquin 238, then 35 and fewer; 17 contributors; moved to a
  `stardew-access` organisation; two Patreon pages (Shoaib, Kat). Release pace has slowed since 2024.
- **Factorio Access:** original author Crimso777 (98 commits). His repo now opens "This is the page for the old and
  outdated mod version" and points to the new one (<https://github.com/Crimso777/Factorio-Access>). Today LevFendi
  1232 and ahicks92 1162 lead an organisation of 7 repos. On the mod portal ahicks writes "I'm one of the other devs
  behind the scenes, also a totally blind senior software engineer"
  (<https://mods.factorio.com/mod/FactorioAccess/discussion/6644b27c43cc1f402005f05c>). The README says Vanilla Mode
  "relies on active sighted users and/or developers", since "blind developers cannot test it at all".
- **Hearthstone Access:** one developer (GuideDev, 1 contributor on the repo). He stopped because "I've been seriously
  struggling to keep up with everything", "I simply don't have as much time", and "Hearthstone Access is just getting
  worse with every patch" (<https://hearthstoneaccess.github.io/sunsetting-hearthstone-access.html>). It survived
  because "Developers from the Hearthstone Access community were able to continue developing the mod after Guide Dev
  provided some tools and information", through <https://github.com/HearthstoneAccess/DevTools>
  (<https://www.hearthstoneaccess.com/>).
- **Say the Spire:** bradjrenshaw 301 of 305 commits. The last release (2023) was still a beta, and he now ships Say
  the Spire 2 (526 of 538 commits). Why the first one stopped is `[NEEDS VERIFICATION]` (the forum thread was not
  reachable).
- **Pokémon Access:** single authors; last push 2020 (tspivey) and 2022 (fork), no releases. Played on an old
  emulator; the Gen 3 fork exists because "This set of scripts is heavily based on Pokemon access"
  (audiogames.net digest, June 2022).
- **P4G Access, FF6 Screen Reader:** one main author each (P4G: Haru; FF6: BlindGuyNW 83 of 104 commits).

### Player docs, settings and keys

- **Stardew Access:** Markdown docs in the repo (`docs/setup.md`, `keybindings.md`, `config.md`, `features.md`,
  `guides/`, versioned `changelogs/`). Every key and setting is a table row with its default, and "All keybindings may
  be modified in the config except for escape". Verbosity is per-feature switches (`DisableInventoryVerbosity`,
  `DisableBushVerbosity`, `VerboseCoordinates`, `TTS`) edited in JSON; `RepeatLastTextKey` defaults to
  `LeftAlt + Space`.
- **Factorio Access:** developer-maintained feature docs in `docs/features/`, and a user wiki that "is out of date";
  in-game tutorial on Ctrl+T; a toggle (Ctrl+Alt+Shift+V) that turns the mod off for sighted co-players.
- **Say the Spire 2:** an mdBook site; "Mod settings screen (Ctrl+M) for all configuration", "per-source filtering"
  of events, and "All keyboard and controller inputs can be configured"
  (<https://github.com/bradjrenshaw/say-the-spire2> README). On install, "You should hear the mod announce "Say the
  Spire" and its version."
- **FF6 Screen Reader:** README key tables; `Shift+I` announces help for the current screen; an F8 mod menu.
- **Hearthstone Access:** a static site with Keyboard Commands, FAQ and Changelog pages.

### Feedback and discovery

- Discord is the main channel for Stardew Access, Factorio Access ("feel free to contact us at our Discord server"),
  Hearthstone Access ("you can post about them in the Discord server", channel "ask-the-developers") and Say the
  Spire 2. Stardew Access also says to post bugs in GitHub issues and lists itself on Nexus Mods.
- audiogames.net is where new releases are announced. Each month the forum's digest quotes release threads, whose
  titles carry the version: "Say the Spire 2 v0.2.0" (topic 58879), "Foot Ball Simulator Access v1.0 released",
  "Diablo Access 1.4", and more than a dozen others in one month
  (<https://buttondown.com/audiogames_digest/archive/audiogamesnet-monthly-digest-march-2026/>). Factorio Access got
  its digest entry in June 2022: "the game is now playable with a screen reader" (topic 45167,
  <https://buttondown.com/audiogames_digest/archive/audiogamesnet-monthly-digest-june-2022/>).
- P4G Access asks players to send the newest Reloaded-II log when speech reads wrong
  ([persona-4-golden-access-2026-11-16.md](persona-4-golden-access-2026-11-16.md)).

## 4. For this project

What exists: the install guide is a draft with most Reloaded-II steps `[NEEDS VERIFICATION]` and an `AppConfig.json`
edit by hand ([install-the-mod.md](../players/how-to/install-the-mod.md)). The mod speaks one of three load messages.
#283 plans a read-only `check-install.ps1` that speaks through Windows' voice. #267 is a contributor onboarding script.
An alpha feedback form exists (`.github/ISSUE_TEMPLATE/alpha-feedback.yml`).

### (a) Verdict on install accessibility today

A blind player can install Reloaded-II by ear only with effort: its runtimes install silently, and `Setup.exe --nogui`
gives a console install (both from code, not yet tried by ear). The launcher window has had partial labels since
1.27.10, but no screen-reader user has reported on it upstream. Enabling a mod and making a shortcut in it still need
review-cursor clicks, so P4G Access avoids the window. Everything the window does for us is scriptable: the install,
the mods folder, `AppConfig.json`, and a shortcut to `Reloaded-II.exe --launch`.

Smallest path: a release-folder `install.ps1` that speaks each step through the same Windows voice as #283 and does
four things. It runs Reloaded-II's pinned `Setup.exe` with `--nostartreloaded --nogui`, copies `metaphor.accessibility`
into `ModConfigDirectory`, and writes or merges `AppConfig.json` with `EnabledMods`. Then it creates a desktop shortcut
to `Reloaded-II.exe --launch "<metaphor.exe>"` and ends by running #283's check. It shares #283's detection code and
falls under the security review (install code). The player never opens Reloaded-II's window.

### (b) Five practices worth adopting

1. **One spoken installer that also installs the loader** (P4G Access, Accessible Stardew Setup, Say the Spire 2,
   Hearthstone patcher, AccessModsInstaller). Action: file `install.ps1` as #283's sibling, sharing its checks, and make
   the install guide's first step "run it".
2. **Speak the mod's name and version on load** (Say the Spire 2's install check). Action: add the version to
   "Metaphor accessibility loaded", and have #283 compare it with the release folder.
3. **One reference page with every key and setting as a table row with its default, plus a help key in game**
   (Stardew `keybindings.md` and `config.md`, FF6 `Shift+I`, Factorio Access Ctrl+T). Action: move the guide's "Keys
   and buttons" into `docs/players/reference/`, and add rebinding and per-feature verbosity switches to the settings
   work in #96.
4. **Announce each release in a versioned audiogames.net thread and ask for the log** (the digest picks these up; P4G
   asks for the Reloaded-II log). Action: at the first public alpha, open one forum thread titled with the version,
   link the alpha feedback form, and say which log file to attach.
5. **Plan for the maintainer leaving** (Hearthstone Access survived only because DevTools were handed over; Factorio
   Access outlived its creator inside an organisation with co-maintainers). Action: keep the RE tooling and findings in
   the repo as now, land #267 so a second contributor can build and deploy in one command, and keep the
   game-build gate so a patch silences the mod rather than breaking it.
