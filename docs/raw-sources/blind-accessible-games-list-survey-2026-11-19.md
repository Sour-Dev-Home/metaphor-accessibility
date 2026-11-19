# Survey of the blind-accessible games list: mods and retrofits of sighted games

- **Source:** <https://github.com/ChristendomStrategist/blind-accessible-games-list> (the list, 192 entries, read
  2026-11-19), then each mod's own repository README, release list, contributor list and issues, read through
  `gh api` on the same date (`repos/<r>`, `repos/<r>/releases?per_page=100`, `repos/<r>/contributors?per_page=100`,
  `repos/<r>/issues`). Repositories were read at their default branch.
- **Saved:** 2026-11-19
- **Game build:** none (this note is about other games' mods, not Metaphor: ReFantazio)
- **Not reached / limits:** the list also links Patreon, Nexus, Thunderstore, itch.io and forum entries; none were
  read. The contributor count is capped at 100 by the API page size. "None" under licence means the GitHub API
  reported no licence file, not that the author chose one. Release counts are GitHub releases, so a mod that ships
  only through a forum or Nexus shows 0. Downloaded nothing, ran nothing. Fetched text was treated as data.

This extends, and does not repeat, these notes: `accessmods-org-survey-2026-11-16.md` (PhoenixWright, DDLC+,
UnityAccessibilityLib), `access-mod-install-and-longevity-2026-11-18.md` (Reloaded-II install by ear, release cadence
of Stardew, Factorio, Hearthstone, Pokemon access, say-the-spire2), `field-navigation-accessibility-mods-2026-11-16.md`
(navigation in FF Pixel Remaster, FFVIII, grimdark), `witcheraccess-announcement-2026-11-17.md` and
`modding-tools-survey-2026-11-13.md`. Those mods are not tabulated again. Of the 192 entries, about 150 are mods or
retrofits of sighted games; 22 with public source were read in depth (below).

## 1. The mods (all GitHub; `R` = `https://github.com/<repo>`)

Language and licence are from `repos/<r>`. Loader and speech library are from the README linked in the same row's repo
column (README at `R#readme`). "Rel" is the release count, "first/latest" the release dates, "Ctr" contributors.

| Mod (game) | Engine | Loader / hook | Speech | Lang | Licence | Repo | First / latest | Rel | Ctr |
|---|---|---|---|---|---|---|---|---|---|
| Fallout 3 Access | Gamebryo (native 32-bit) | FOSE plugin | Prism, static (MPL-2.0) | C++ | MIT | Bruticus2131/fallout3-access | 2026-06-04 / 11-06 | 25 | 1 |
| Persona 4 Golden Access | native 64-bit | Reloaded-II mod | Prism | C# (.NET 9) | GPL-3.0 | AquaRose7/Persona-4-Golden-Access | 06-25 / 10-18 | 12 | 1 |
| Voice of the Old Republic (KOTOR 1+2) | native | `dinput8.dll` proxy; Kotor-Patch-Manager; Ghidra DB | Prism | C++ | GPL-3.0 | JeanStiletto/voice-of-the-old-republic | 05-29 / 10-30 | 43 | 2 |
| Sparking Zero Access | Unreal | UE4SS (pinned build) + Lua | UniversalSpeech via own Lua C module | Lua | none | AccessForge/SparkingZeroAccess | 04-06 / 04-26 | 2 | 3 |
| DBZ Kakarot Access | Unreal 4.21 | UE4SS (installer sets up) | Prism, bundled | C | none | Ali-Bueno/dbz-kakarot-access | 07-17 / 11-19 | 17 | 2 |
| SF6 Access (Street Fighter 6) | RE Engine | REFramework per `PRINCIPLES.md` [NEEDS VERIFICATION it is the one used] | [NEEDS VERIFICATION] | C# | none | Ali-Bueno/sf6Access | 06-12 / 10-27 | 9 | 2 |
| Civ V Access | Firaxis, Lua UI | in-game mod + installer | [NEEDS VERIFICATION] | Lua | MIT | rashadnaqeeb/Civ-V-Access | 05-09 / 10-22 | 53 | 2 |
| Civ VI Accessibility Integration | Firaxis, Lua UI | installer + `extern` submodules | [NEEDS VERIFICATION] | Lua | MIT | flat-arther/Civ-VI-Accessibility-Integration | 08-03 / 09-30 | 12 | 6 |
| Vision Not Included (Oxygen Not Included) | Unity | Loadstone packages [NEEDS VERIFICATION which loader] | Prism (vendored) | C# | MIT | rashadnaqeeb/VisionNotIncluded | 03-30 / 10-13 | 51 | 2 |
| RimWorld Access | Unity | HarmonyLib | Prism | C# | MIT | aaronr7734/rimworld_access (was AccessMods/) | 2025-12-26 / 2026-10-19 | 6 | 8 |
| Against the Storm Access | Unity | BepInEx | Prism | C# | MIT | rashadnaqeeb/ats-accessibility-mod | 01-27 / 08-31 | 44 | 2 |
| Non-Visual Calculus (Disco Elysium) | Unity | BepInEx via installer | Prism | C# | MIT | rashadnaqeeb/NonVisualCalculus | 07-14 / 09-02 | 12 | 2 |
| Cult of the Lamb Access | Unity | BepInEx 5 | NVDA controller client; optional Tolk | C# | unclear | zersiax/cultaccess | 08-21 / 09-01 | 3 | 1 |
| Accessible Arena (MTG Arena) | Unity | MelonLoader (installer adds it) | Prism | C# | GPL-3.0 | JeanStiletto/AccessibleArena | 01-20 / 11-14 | 57 | 6 |
| WH40K Rogue Trader Access | Unity | Unity Mod Manager (bundled with game) | Prism, SAPI fallback | C# | MIT | alex19EP/WH40KRTAccess | 10-18 / 10-18 | 1 | 1 |
| Football Manager 2026 Access | Unity | BepInEx | Prism | [none reported] | GPL-3.0 | DnDeezNuts/Football-Manager-2026-Access | 07-24 / 11-15 | 35 | 1 |
| Outer Wilds Access | Unity | [NEEDS VERIFICATION] | Tolk, NVDA controller client | C# | MIT | PlatinumTsuki/OuterWildsAccess | 03-03 / 04-14 | 15 | 1 |
| Terraria Access | XNA/FNA | tModLoader | Tolk | C# | MIT | ChipsAhoiMcCoy/TerrariaAccess | 2026-02-28 / 06-20 | 3 | 5 |
| Project Zomboid Access | Java + Lua | game's Lua modding | Prism, SAPI fallback | Lua | unclear | liliancoghlan1-tech/zomboid-access | 10-01 / 10-15 | 7 | 2 |
| Diablo Access | DevilutionX fork | source fork (no hook) | Tolk (Windows) | C++ | unclear | mojsior/diablo-access | 01-29 / 03-10 | 6 | 100+ (upstream) |
| Blindfold (Balatro) | LOVE/Lua | Lovely Injector, `version.dll` | Prism | Lua | none | bradjrenshaw/blindfold | 07-12 / 11-01 | 12 | 3 |
| Say the Spire (Slay the Spire) | Java/libGDX | [NEEDS VERIFICATION] | [NEEDS VERIFICATION] | Java | none | bradjrenshaw/say-the-spire | 2020-09-26 / 2023-05-24 | 14 | 4 |
| FA11y (Fortnite) | external, no injection | none: runs beside the game | own TTS | Python | AGPL-3.0 | GreenBeanGravy/FA11y | 2023-08-31 / 2026-10-11 | 26 | 4 |

Evidence for the loader and speech columns: Fallout 3 README (FOSE 26 mentions, Prism v0.18.3 fetched by CMake and
built in unmodified, FOSE SDK not bundled for licensing): <https://github.com/Bruticus2131/fallout3-access#readme>.
P4G: <https://github.com/AquaRose7/Persona-4-Golden-Access#readme>. KOTOR (dinput8 proxy, Prism, Ghidra, Kotor-Patch-Manager
credit): <https://github.com/JeanStiletto/voice-of-the-old-republic#readme>. Sparking Zero (UE4SS, UniversalSpeech,
`speech_bridge.dll`): <https://github.com/AccessForge/SparkingZeroAccess#readme>. Kakarot (UE4SS, Prism, "engine Unreal
4.21"): <https://github.com/Ali-Bueno/dbz-kakarot-access#readme>. RimWorld (Harmony, Prism):
<https://github.com/aaronr7734/rimworld_access#readme>. Cult: <https://github.com/zersiax/cultaccess#readme>. ATS:
<https://github.com/rashadnaqeeb/ats-accessibility-mod#readme>. Arena:
<https://github.com/JeanStiletto/AccessibleArena#readme>. WH40K: <https://github.com/alex19EP/WH40KRTAccess#readme>.
Terraria: <https://github.com/ChipsAhoiMcCoy/TerrariaAccess#readme>. Diablo:
<https://github.com/mojsior/diablo-access#readme>. Blindfold (Loadstone installer, Lovely Injector):
<https://github.com/bradjrenshaw/blindfold#readme>. FA11y: <https://github.com/GreenBeanGravy/FA11y#readme>. SF6 principles:
<https://github.com/Ali-Bueno/sf6Access/blob/HEAD/PRINCIPLES.md>. Release, contributor and licence numbers: the `gh api`
calls named at the top, per repository.

## 2. Development workflows

- **Reverse-engineer in the open, record it as docs next to the code.** P4G keeps per-system "source-of-truth" docs
  (`BATTLE_SYSTEM.md`, `OVERWORLD.md`, `SNAPSHOT_METHOD.md`, "the live-memory investigation technique") in `database/`
  and tells contributors to read the relevant one before touching a system:
  <https://github.com/AquaRose7/Persona-4-Golden-Access#readme>. KOTOR credits a shared Ghidra database and patch
  framework (Kotor-Patch-Manager) that made hooking possible, and its issue #10 asks whether the author may share engine
  findings with that project: <https://github.com/JeanStiletto/voice-of-the-old-republic/issues/10>.
- **Manual in-game verification is the norm.** P4G states "There are no automated tests; verify a change by launching
  the game": <https://github.com/AquaRose7/Persona-4-Golden-Access#readme>. SF6 Access's status ledger marks items "built,
  not tested in game" and tells the agent "Do not launch the game", with 520 navigation checks passing as the automated
  part: <https://github.com/Ali-Bueno/sf6Access/blob/HEAD/STATUS.md>.
- **AI-assisted development is visible in the repos.** SF6 Access and Civ VI Integration both ship `AGENTS.md` and
  `CLAUDE.md` at the root (`gh api repos/<r>/contents`). SF6 keeps a `STATUS.md` "dashboard, not docs" so a session
  resumes without re-deriving state: <https://github.com/Ali-Bueno/sf6Access/blob/HEAD/STATUS.md>. Whether the other
  authors use AI is [NEEDS VERIFICATION].
- **Build on a loader, ship an installer.** Seven of the 22 ship an installer exe or script that finds the game, adds
  the loader, updates and uninstalls: Fallout 3, KOTOR, Kakarot, Civ V, Arena, Blindfold, FM26 (READMEs above; Civ V
  <https://github.com/rashadnaqeeb/Civ-V-Access#readme>, FM26 <https://github.com/DnDeezNuts/Football-Manager-2026-Access#readme>).
  Blindfold's installer is a general tool ("Loadstone") configured for one mod: <https://github.com/bradjrenshaw/blindfold#readme>.
- **Self-update check.** SF6 Access asks GitHub for the latest release once per launch and speaks a notice on the title
  screen; offline it logs and carries on (STATUS.md above). KOTOR checks for an update on F5 at the main menu (README).
- **Logs and an on-demand dump for bug reports.** Fallout 3 writes a rolling log and an F11 state dump; the log names the
  voice chosen ("Speech: speaking through NVDA"): <https://github.com/Bruticus2131/fallout3-access#readme>. P4G logs to
  the Reloaded-II log folder.
- **Per-game state-change hooks, not screen scraping.** FA11y is the exception and is deliberately external; see 5.

## 3. Tool stacks

- **Speech.** Prism is the library in 13 of the 22 rows (Fallout 3, P4G, KOTOR, Kakarot, ONI, RimWorld, ATS, NVC, Arena,
  WH40K, FM26, Zomboid, Blindfold); Tolk in 3 (Terraria, Outer Wilds, Diablo) plus Cult as an option; UniversalSpeech in
  one (Sparking Zero). Nearly every Prism mod also falls back to a SAPI/Windows voice when no screen reader runs
  (Fallout 3, KOTOR, Kakarot, WH40K, Zomboid READMEs). Prism ships as a bundled `prism.dll` or built in statically;
  Fallout 3 credits it as MPL-2.0 "built unmodified into the plugin": <https://github.com/Bruticus2131/fallout3-access#readme>.
  Cult ships NV Access's NVDA controller client and takes Tolk only if the user drops it in:
  <https://github.com/zersiax/cultaccess#readme>. Prism upstream: <https://github.com/ethindp/prism> (named by those READMEs).
- **Hooking and loaders by engine family.** Unity: BepInEx (ATS, NVC, Cult, FM26), MelonLoader (Arena), Harmony
  (RimWorld), Unity Mod Manager (WH40K). Unreal: UE4SS with Lua (Sparking Zero, Kakarot). Native: FOSE plugin
  (Fallout 3), `dinput8.dll` proxy plus a patch framework (KOTOR), Reloaded-II (P4G), Lovely Injector via `version.dll`
  (Balatro). Source fork when the engine is open (Diablo via DevilutionX).
- **Languages follow the loader:** C# for Unity and Reloaded-II, Lua for UE4SS and Lovely, C++ for FOSE and KOTOR.
- **RE tools named in READMEs:** Ghidra (KOTOR), ILSpy (Arena, one mention), a "live-memory investigation technique"
  (P4G `SNAPSHOT_METHOD.md`). P4G hard-codes addresses because ASLR is disabled in that game, and guards every unsafe
  read because `AccessViolationException` cannot be caught in .NET:
  <https://github.com/AquaRose7/Persona-4-Golden-Access#readme>.
- **Dependencies on other mods are a hazard.** P4G requires File Emulation Framework and `p4g64.customSubMenu`;
  "without them the FlowScript-based features silently no-op" (same README).

## 4. Development speed and cadence

Measured from repo creation / first release to the data date (2026-11-19). Counts are in the table.

- **Fast first release, then frequent small ones, by one or two people.** Fallout 3: first release the day the repo was
  created (2026-06-04), 25 releases by 2026-11-06 with one contributor. Civ V: 53 releases between 2026-05-09 and
  2026-10-22 with two. ONI: 51 between 2026-03-30 and 2026-10-13. KOTOR: 43 between 2026-05-29 and 2026-10-30. Arena: 57
  between 2026-01-20 and 2026-11-14 with six. ATS: 44 in seven months. (Counts from the `gh api` calls above.)
- **Release date is not 1.0.** Many releases are point or fix releases of an unfinished scope. P4G reached v2.2.1 four
  months after its first release (README: "Latest release: v2.2.1"). Sparking Zero has 2 releases but a later "current
  build repair" kept alive on the default branch rather than as a release (see 5). Time to a "complete" game is not
  stated by any of these authors; [NEEDS VERIFICATION] for any claim of "1.0" speed.
- **Slow burners.** Say the Spire: 14 releases from 2020-09-26 to 2023-05-24, last push 2025-11-25, open issues 18
  (stable single-player game, little to break). FA11y: first release 2023-08-31, still pushed 2026-11-02.
- **Quiet repos.** Outer Wilds Access last pushed 2026-04-14, 15 releases, one contributor. Cult of the Lamb Access: 3
  releases within 11 days, last push 2026-10-21. Both could be finished or paused; the repos do not say, so abandonment
  is [NEEDS VERIFICATION]. Diablo Access: last release 2026-03-10, pushed 2026-11-08, issues 16 open.
- **Team size is mostly 1-2.** Of the 22, 14 have one or two contributors. Six or more: Arena (6), Civ VI (6),
  RimWorld (8), plus the Diablo fork's upstream. Compare `access-mod-install-and-longevity-2026-11-18.md` section 3
  (bus factor) for the older mods.

## 5. Recurring pitfalls (with evidence)

- **Game patches break hooks, hardest on live-service games.** Arena's issues after MTG Arena updates:
  #58 "after update, daily objectives are read as objective graphics", #109 "after the update, all reward numbers are
  all messed up", #110 "can't play a card, something broke", #114 "doesn't work with the new update", #117, #120 "deck
  builder problem since the MTGA update 25th": <https://github.com/JeanStiletto/AccessibleArena/issues>. For an offline game,
  Fallout 3 requires downgrading Steam to 1.7.0.3 because "the current Steam build (1.7.0.4) is not supported by FOSE":
  <https://github.com/Bruticus2131/fallout3-access#readme>. Sparking Zero needed a "current-build repair" for Steam build
  24953175 (executable SHA-256 recorded) that fixes a startup init hook and moves Unreal reads onto the game thread, and
  states stock UE4SS 3.0.1 "is not the validated loader": <https://github.com/AccessForge/SparkingZeroAccess/blob/HEAD/docs/current-build-repair.md>.
- **Pin by executable hash.** KOTOR lists "Builds whose `swkotor.exe` SHA-256 doesn't match the recognised Steam or GoG
  1.0.3 hashes" as unsupported: <https://github.com/JeanStiletto/voice-of-the-old-republic#readme>.
- **Unsigned binaries trip SmartScreen.** KOTOR documents it, says a certificate "costs hundreds of euros per year", and
  publishes a SHA-256 per release with `Get-FileHash` and `certutil` steps (same README, "Windows warns the installer").
- **Screen-reader compatibility.** KOTOR issue #3 "JAWS not working" (closed): <https://github.com/JeanStiletto/voice-of-the-old-republic/issues/3>.
  Arena says "only NVDA and JAWS are tested". Moving to Prism plus a SAPI fallback is the common answer (section 3).
- **Overlays and accessibility traps outside the mod.** Civ V's README tells players to turn off the Steam overlay because
  it is inaccessible and "intercepts the Shift+Tab key": <https://github.com/rashadnaqeeb/Civ-V-Access#readme>.
- **Crashes from the mod.** RimWorld #96 "Game crash after manual or automatic save" and #117 a NullReference in a picker:
  <https://github.com/aaronr7734/rimworld_access/issues>. P4G wraps all unsafe reads (above). KOTOR #4 "speaking to
  Ajuur ... crashes the game".
- **Anti-cheat and online play.** FA11y avoids the problem by design: it "does not modify, inject into, or read" the
  game, uses Epic public APIs and its own overlay: <https://github.com/GreenBeanGravy/FA11y#readme>. The single-player
  mods read here state no anti-cheat conflict; none was found documenting a bypass. [NEEDS VERIFICATION] for SF6, which is
  online; its STATUS.md says nothing on it.
- **Install friction.** Fallout 3's manual path has six numbered steps, including a Steam downgrade, FOSE and two prerequisite
  mods; its README offers an installer instead. Arena, KOTOR, Kakarot, Civ V, FM26 and Blindfold ship installers because manual loader setup
  excludes blind users (READMEs). First-run delays: NVC warns "the first launch takes a few minutes while the mod loader
  generates files" so players do not think it froze: <https://github.com/rashadnaqeeb/NonVisualCalculus#readme>.
- **Forking and fragmentation.** KOTOR issue #12 is an open note from a fork that "went its own way (NVDA only, text instead
  of sound)": <https://github.com/JeanStiletto/voice-of-the-old-republic/issues/12>. FF Pixel Remaster has per-game repos
  (see the field-navigation note).
- **Maintainer burnout and abandonment.** No repo here states burnout. The evidence is indirect (quiet repos, section 4)
  and [NEEDS VERIFICATION]. The code-structure form of it: RimWorld #88 flagged a "3000-line God Method" and Terraria #8
  "Remove 47 dead files from abandoned refactorings": <https://github.com/ChipsAhoiMcCoy/TerrariaAccess/issues/8>.

## 6. Lessons for this project

Each lesson names the project rule or ADR it touches. They are suggestions, not decisions.

1. **Keep the build gate; add an executable hash.** Two mods pin by hash (KOTOR) or record it with the repair (Sparking
   Zero), and a live-service mod (Arena) shows the failure when you do not. Our rule 2 and architecture rules 6 and 8
   already require a build or signature check; consider logging the exe SHA-256 in each finding's header too (sections 5).
2. **Reloaded-II plus Prism plus a SAPI fallback is a proven recipe.** P4G does exactly that on a native 64-bit PC port with one contributor and 12 releases in under four months. Copy its structure for notes: one source-of-truth
   doc per system and a documented live-memory method (section 3, P4G README). Check its Prism licence against ADR 0005
   first (Fallout 3 credits Prism as MPL-2.0).
3. **Guard every native read.** P4G's `AccessViolationException` note and RimWorld's crash-on-save issue are the same
   lesson: a speech mod must never crash the game; fail silent and log (sections 3, 5).
4. **Ship an installer and per-release checksums early.** Seven of the 22 ship one and KOTOR documents SmartScreen and the
   hash check. Our spoken installer (#303, merged) is aligned; add the SHA-256 and a SmartScreen note to the player
   docs (section 5, KOTOR README). See `access-mod-install-and-longevity-2026-11-18.md` for the by-ear install work.
5. **Warn about the first launch and the Steam overlay.** NVC's "takes a few minutes" and Civ V's overlay advice cost a
   line each and prevent a stuck-looking game (section 5).
6. **Credit the RE frameworks you use, and share findings back.** KOTOR credits Kotor-Patch-Manager in the README and
   asks about sharing engine findings (issue #10); this is rule 7 in practice and a route to help on hooks.
7. **Do not assume hook libraries are version-proof.** Sparking Zero needed a specific UE4SS build and a config flag;
   FOSE would not run on the newest Steam build. Pin the Reloaded-II version and its dependency mods like we pin the game.
8. **Prefer not to need anti-cheat answers.** Metaphor is single-player; FA11y shows the other side of the choice (stay
   external) for online games. Rule 5 stands; stop and note if a protection appears.
9. **Manual verification is the industry norm, so make ours cheaper.** P4G, SF6 and others have no in-game automated
   tests; separating pure logic (SF6's 520 navigation checks) from hook code is the part that can be tested without
   launching the game (SF6 STATUS.md).
10. **Release small and often with a self-update notice.** Fast, small releases are the pattern (Civ V 53, Arena 57, Fallout 3
    25) and SF6's once-per-launch update check, silent offline, is a low-risk way to tell players about a patch fix.
    Whether our hook layer may touch the network is a security decision, not a research one.

[NEEDS VERIFICATION]: SF6's loader and speech library, Civ V/VI and Outer Wilds speech libraries and loaders, Say the
Spire's stack, and any 1.0 or burnout claim, as marked above.
