# Metaphor: ReFantazio community modding tools survey

- **Source:** web survey. Main URLs:
  - GitHub: <https://github.com/DeathChaos25/mrfpc.modloader>, <https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII>,
    <https://github.com/RyoTune/Ryo>, <https://github.com/RyoTune/MF.Toolkit>, <https://github.com/rirurin/opengfd>,
    <https://github.com/DeathChaos25/MetaphorAPKPack>, <https://github.com/rirurin/metaphor-apk-rs>,
    <https://github.com/tge-was-taken/GFD-Studio>, <https://github.com/Pherakki/BlenderToolsForGFS>,
    <https://github.com/DeathChaos25/metaphor-010-templates>, <https://github.com/Reloaded-Project/Reloaded-II>
  - Codeberg: <https://codeberg.org/Lyall/MetaphorFix>
  - GameBanana game section (game ID 21201): <https://gamebanana.com/games/21201>, read through the public API
    (`https://gamebanana.com/apiv11/Mod/Index?...&_aFilters[Generic_Game]=21201`,
    `https://gamebanana.com/apiv11/Tool/Index?...&_aFilters[Generic_Game]=21201`, `https://gamebanana.com/apiv11/Mod/<id>/ProfilePage`)
  - CriFs support table: <https://sewer56.dev/CriFs.V2.Hook.ReloadedII/>
  - Repository metadata (last push, license, latest release) came from the GitHub REST API via `gh api` on the date below.
- **Date saved:** 2026-11-13
- **Game build(s) the sources mention:** PC (Steam app 2679460, linked from
  <https://github.com/rirurin/metaphor-apk-rs>). The only game patch number stated by a primary tool source is the
  "1.09 game update" (MetaphorFix v0.8.4 release notes, <https://github.com/Lyall/MetaphorFix/releases/tag/v0.8.4>).
  A news article mentions "version 1.11 patch notes" (<https://www.techradar.com/gaming/consoles-pc/metaphor-refantazios-latest-patch-makes-improvements-to-its-main-menu-by-adding-a-new-party-formation-option>)
  `[NEEDS VERIFICATION]` (secondary source, seen only as a search summary). No loader or tool found states the exact
  game build it was verified on. The current Steam build number was not checked `[NEEDS VERIFICATION]`.

Nothing was downloaded or run for this survey. "Maintained" below means only what the dates show; nobody tested
whether any tool works on the current game build.

## Engine

The OpenGFD README describes "Atlus' GFD Engine, used in Persona 5 (Royal), Metaphor: Refantazio, Catherine Full Body
and the Persona Dancing games" (<https://github.com/rirurin/opengfd>). That is a community source, not an Atlus
statement. GFD-Studio also ships Metaphor material presets and a Metaphor shader
(`GFDStudio/Presets/Metaphor/`, `GFDLibrary/Materials/MaterialParameterSet_Metaphor.cs` in
<https://github.com/tge-was-taken/GFD-Studio>), which supports the same conclusion. Game files sit in CRI CPK archives
(CriFs support table lists the game, <https://sewer56.dev/CriFs.V2.Hook.ReloadedII/>). Game scripts are Squirrel
(`.cnut`), "Squirrel version is likely 3.2" (<https://github.com/RyoTune/NutCracker-Metaphor>).

## Summary table

| Tool | Category | Primary source | Latest seen | License | Language / framework | Open source |
|---|---|---|---|---|---|---|
| Reloaded-II | Mod loader/manager | [GitHub](https://github.com/Reloaded-Project/Reloaded-II) | 1.31.0, 2026-09-23 | GPL-3.0 | C# / .NET | Yes |
| Metaphor Essentials (`mrfpc.modloader`) | Mod loader (Reloaded-II mod) | [GitHub](https://github.com/DeathChaos25/mrfpc.modloader), [GameBanana](https://gamebanana.com/mods/548481) | 1.0.2, 2024-11-15 | LGPL-2.1 (repo); CC BY-NC-ND 4.0 (GameBanana page) | C# / Reloaded-II | Yes |
| CRI FileSystem V2 Hook (`crifs.v2.hook`) | CPK file redirection (Reloaded-II mod) | [GitHub](https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII) | 2.6.1, 2026-06-14 | LGPL-3.0 | C# / Reloaded-II | Yes |
| Ryo Framework | Audio and movie replacement (Reloaded-II mod) | [GitHub](https://github.com/RyoTune/Ryo), [GameBanana](https://gamebanana.com/mods/548484) | 2.11.5 release, 2026-05-12 (repo pushed 2026-11-04) | "General Mod License" (repo) | C# / Reloaded-II | Source public |
| Metaphor Modding Toolkit (`MF.Toolkit`) | Modding library: messages, Squirrel scripts, file redirection | [GitHub](https://github.com/RyoTune/MF.Toolkit), [GameBanana](https://gamebanana.com/mods/548487) | 1.2.6, 2025-02-28 (GitHub); 1.2.4 (GameBanana) | AGPL-3.0 (repo); CC BY-NC-ND 4.0 (GameBanana page) | C# / Reloaded-II | Yes |
| Costume Framework (`MF.CostumeFramework`) | Costume/outfit framework | [GitHub](https://github.com/RyoTune/MF.CostumeFramework), [GameBanana](https://gamebanana.com/mods/548496) | 1.0.2, 2025-05-31 | AGPL-3.0 (repo) | C# / Reloaded-II | Yes |
| OpenGFD | Engine reimplementation / hooking library (Reloaded-II mod) | [GitHub](https://github.com/rirurin/opengfd) | 0.2.0, 2025-10-05 (pushed 2026-03-19) | LGPL-3.0 | Rust + C# / Reloaded-II | Yes |
| riri-mod-tools | Hooking framework (Rust on Reloaded-II) | [GitHub](https://github.com/rirurin/riri-mod-tools) | 0.3.1, 2026-02-28 | MIT | Rust + C# | Yes |
| MetaphorAPKPack | Texture container (APK) unpack/repack | [GitHub](https://github.com/DeathChaos25/MetaphorAPKPack), [GameBanana](https://gamebanana.com/tools/18206) | 1.0.0, 2024-09-27 | GPL-3.0 | C# / .NET 8 | Yes |
| metaphor-apk-rs | Texture container (APK) library + tool | [GitHub](https://github.com/rirurin/metaphor-apk-rs) | 0.2.0, 2025-10-22 | GPL-3.0 | Rust | Yes |
| metaphor-010-templates | Table/data format templates (010 Editor) | [GitHub](https://github.com/DeathChaos25/metaphor-010-templates) | no release; pushed 2025-02-02 | none shown | 010 Editor templates | Source public |
| GFD-Studio | Model editor | [GitHub](https://github.com/tge-was-taken/GFD-Studio) | 0.10.3, 2025-09-10 | none shown by the API | C# | Source public |
| BlenderToolsForGFS | Model import/export (Blender) | [GitHub](https://github.com/Pherakki/BlenderToolsForGFS) | v0.3.1, 2025-01-03 | GPL-3.0 | Python / Blender | Yes |
| NutCracker-Metaphor | Squirrel script decompiler | [GitHub](https://github.com/RyoTune/NutCracker-Metaphor) | no release; pushed 2024-09-27 | none shown | `[NEEDS VERIFICATION]` | Source public |
| metaphor-ssbp-to-sspj | UI sprite (SpriteStudio) converter | [GitHub](https://github.com/rirurin/metaphor-ssbp-to-sspj) | 0.1.0, 2025-10-16 | GPL-3.0 | Rust | Yes |
| cri-archive-lib | CRI ACB/AWB/CPK/ACF reader | [GitHub](https://github.com/rirurin/cri-archive-lib) | 0.1.1, 2026-01-15 | GPL-3.0 | Rust | Yes |
| Useful Modding Logging | Debug logging for script functions | [GameBanana](https://gamebanana.com/mods/550945) | 1.0.1, 2025-01-22 | CC BY-NC-ND 4.0 | Reloaded-II mod | No source link |
| MetaphorFix | Fix/patch mod (ultrawide, graphics) | [Codeberg](https://codeberg.org/Lyall/MetaphorFix) | 0.8.5, 2025-06-04 | MIT (GitHub mirror) | C++ (ASI or Reloaded-II) | Yes |

## Tools

### Reloaded-II

- What: "universal DLL injection based Mod Loader and Mod Management Framework" (search summary of
  <https://github.com/Reloaded-Project/Reloaded-II>). Every Metaphor loader and framework below is a Reloaded-II mod.
- Latest: 1.31.0, published 2026-09-23; license GPL-3.0; C# (GitHub API).
- Game version: universal; it names no Metaphor build.

### Metaphor Essentials (`mrfpc.modloader`)

- What: "Enables more advanced mod support for Metaphor: ReFantazio (PC)", "based on Reloaded II". Modder features:
  "Support for replacing CPK file contents via other mods", "Support for hot reload", "Automatically merges .tbl
  files". User features: intro skip, pause on focus loss toggle, force 4k assets. It points to the Persona Essentials
  docs (<https://sewer56.dev/p5rpc.modloader/>) and says "you would create a MFEssentials folder instead of a
  P5REssentials folder". Source: README, <https://github.com/DeathChaos25/mrfpc.modloader>.
- Dependencies (from `ModConfig.json`): `reloaded.sharedlib.hooks`, `Reloaded.Memory.SigScan.ReloadedII`,
  `crifs.v2.hook`. `"SupportedAppId": [ "metaphor.exe" ]`.
- Latest: 1.0.2, 2024-11-15 (GitHub releases and GameBanana <https://gamebanana.com/mods/548481>). No commits since, so
  it looks unmaintained. Forks: `rirurin/mrfpc.modloader` (same last push date).
- License: LGPL-2.1 in the repo; the GameBanana page shows CC BY-NC-ND 4.0.
- Game version: none stated in the README, the GameBanana page or `ModConfig.json`. Whether it works on the current
  build: `[NEEDS VERIFICATION]`.
- A Nexus Mods listing reportedly calls it a loader that "adds Metaphor: ReFantazio support to Vortex Mod Manager"
  (search summary of <https://www.nexusmods.com/metaphorrefantazio/mods/top?offset=20>; the page returned HTTP 403)
  `[NEEDS VERIFICATION]`.

### CRI FileSystem V2 Hook (`crifs.v2.hook`)

- What: "a mod based on Reloaded II that allows you to add/replace files in games that use CRI Middleware and the CPK
  container" (<https://github.com/Sewer56/CriFs.V2.Hook.ReloadedII>). Lists `metaphor.exe` in its `ModConfig.json`
  (GitHub code search).
- Support table (<https://sewer56.dev/CriFs.V2.Hook.ReloadedII/>): two rows, "Metaphor: ReFantazio (Clean)" and one for
  the DRM-protected executable, both "Build Date: Oct 2 2023 10:26:25", "CRI FS Version: 2.85.1". That build date is
  the CRI middleware library's, not the game's. The second row notes anti-tamper obfuscation; this survey records
  that only as a fact, see the rules in `CLAUDE.md`.
- Latest: 2.6.1, 2026-06-14; LGPL-3.0; C#. Actively maintained.

### Ryo Framework

- What: "Ryo Framework adds the ability to add or replace game audio and movies with zero file editing" (`ModConfig.json`
  in <https://github.com/RyoTune/Ryo>; `metaphor.exe` is in its `SupportedAppId` and it has Metaphor-specific CRI
  signature patterns in `CriAtomExPatterns.cs`). The README is only a title. Guide:
  <https://ryotune.github.io/guides/audio/audio-replacement-ryo/>.
- Latest: GitHub release 2.11.5 (2026-05-12); `ModConfig.json` on the default branch says 3.0.0; repo pushed 2026-11-04.
  The GameBanana page (<https://gamebanana.com/mods/548484>) is older, last modified 2024-10-26.
- License: "General Mod License" (<https://ryotune.github.io/licensing/mod-license/>); GameBanana shows CC BY-NC-ND 4.0.
- Game version: none stated.

### Metaphor Modding Toolkit (`MF.Toolkit`)

- What: "A collection of features to make modding easier", with MSG (message) editing, Squirrel script support and
  file redirection; ships a Squirrel compiler (`sq.exe`); mods use it by adding a Reloaded mod dependency; an API
  package is on NuGet (GameBanana <https://gamebanana.com/mods/548487>, read through the API). The GitHub README is only
  a title (<https://github.com/RyoTune/MF.Toolkit>, description "Library providing various utility functionality for
  use in mods").
- Latest: GitHub 1.2.6 (2025-02-28); GameBanana 1.2.4 (2025-01-22). License AGPL-3.0 in the repo, CC BY-NC-ND 4.0 on
  GameBanana. Game version: none stated.
- This is the closest thing to a BMD/BF-style message and script tool for Metaphor. Atlus Script Tools does not list
  Metaphor among its supported games (<https://github.com/tge-was-taken/Atlus-Script-Tools>, README "Supported games").

### Costume Framework (`MF.CostumeFramework`)

- What: "Easily add new costumes to the game!"; mods depend on it through Reloaded-II
  (<https://gamebanana.com/mods/548496>). Repo <https://github.com/RyoTune/MF.CostumeFramework> (README is a title only).
- Latest: 1.0.2, 2025-05-31; AGPL-3.0 (repo). Game version: none stated.

### OpenGFD and riri-mod-tools

- OpenGFD: "A Rust based reimplementation of Atlus' GFD Engine ... made up of a set of components from GFD which can be
  used to interoperate with an existing GFD game. As such, the main use of this crate is as a Reloaded-II mod and
  dependency." Build with `BuildMetaphor.ps1` (<https://github.com/rirurin/opengfd>). Needed for zstd-compressed APKs
  (<https://github.com/rirurin/metaphor-apk-rs>). Latest 0.2.0 (2025-10-05), pushed 2026-03-19, LGPL-3.0, Rust nightly
  + .NET 9 ("9.0.4 or earlier" because 9.0.5 crashes Reloaded-II, per its README).
- riri-mod-tools: "A toolkit for writing Reloaded-II mods in Rust". Its README shows the OpenGFD Metaphor config with
  `SupportedGames = [ "metaphorrefantazio", "metaphor.exe" ]`, signature caching, a hook on Metaphor's `PrintLog`
  Squirrel function, and per-version hooks matched "to a given executable hash"
  (<https://github.com/rirurin/riri-mod-tools>). Latest 0.3.1 (2026-02-28), MIT.
- Related: riri-imgui-hook, "Rust based Dear Imgui hook for D3D11/D3D12", GPL-3.0, no release
  (<https://github.com/rirurin/riri-imgui-hook>). Whether it targets Metaphor: `[NEEDS VERIFICATION]` (it appeared in a
  code search for `metaphor.exe` in `ModConfig.json`).
- Game version: none stated by either.

### Texture containers: MetaphorAPKPack and metaphor-apk-rs

- MetaphorAPKPack: "command-line tool made for packing/unpacking PAK files". In practice it reads `.apk` texture
  containers: it extracts DDS files plus a `FileList.txt`, and repacks DDS with LZ4 HC
  (<https://github.com/DeathChaos25/MetaphorAPKPack>). Needs .NET 8. Release 1.0.0 (2024-09-27), repo pushed
  2025-09-07, GPL-3.0. Also on GameBanana's Tools section (<https://gamebanana.com/tools/18206>), the only tool listed
  there for this game.
- metaphor-apk-rs: Rust library (`ApkReader`/`ApkWriter`) and a `metaphor-apk-pack` tool, "a Rust reimplementation of
  DeathChaos' MetaphorAPKPack, with support for more compression formats" (<https://github.com/rirurin/metaphor-apk-rs>).
  0.2.0 (2025-10-22), GPL-3.0.

### Table and data formats: metaphor-010-templates

- 010 Editor templates (`.bt`) for the game's data tables: `Skill.bt`, `Enemy.bt`, `ItemWeapon.bt`, `Encount.bt`,
  `ShopTable.bt`, `metaphor_structs.h`, and others (repo file list,
  <https://github.com/DeathChaos25/metaphor-010-templates>; mirror at `rirurin/metaphor-010-templates`). No README, no
  release, no license file; last push 2025-02-02.

### Models: GFD-Studio and BlenderToolsForGFS

- GFD-Studio: "P3D, P4D, P5D & Persona 5 Model Editor" (description of the fork
  <https://github.com/rirurin/GFD-Studio>); the main repo <https://github.com/tge-was-taken/GFD-Studio> has Metaphor
  material presets and a Metaphor toon shader. Latest 0.10.3 (2025-09-10), pushed 2026-11-01. The README does not
  mention Metaphor; how complete its Metaphor support is: `[NEEDS VERIFICATION]`.
- BlenderToolsForGFS: "A Blender plugin for importing and exporting GFS files". Its support table lists
  "Metaphor Refantazio (PC)" with version "TBC" and partial import and export (<https://github.com/Pherakki/BlenderToolsForGFS>).
  v0.3.1 (2025-01-03), GPL-3.0.

### Scripts: NutCracker-Metaphor

- "This program roughly decompiles Squirrel .cnut files ... I don't plan on maintaining this"
  (<https://github.com/RyoTune/NutCracker-Metaphor>). Unmaintained, last push 2024-09-27.

### UI sprites and audio

- metaphor-ssbp-to-sspj: "A WIP tool for converting Sprite Studio 6 formatted binary projects (ssbp) ... into XML files"
  (<https://github.com/rirurin/metaphor-ssbp-to-sspj>). 0.1.0, GPL-3.0.
- cri-archive-lib: "Crate for reading Criware table formats (ACB, AWB, CPK, ACF)"; its README example reads the game's
  BGM ACB (<https://github.com/rirurin/cri-archive-lib>). 0.1.1, GPL-3.0.

### Useful Modding Logging

- "Enables logging for certain game scripting functions and other functions that may be useful to modders", output in
  the Reloaded-II console (<https://gamebanana.com/mods/550945>). 1.0.1, 2025-01-22, CC BY-NC-ND 4.0, no source link.

### MetaphorFix

- "A fix for Metaphor: ReFantazio that adds ultrawide/narrower support and much more" (<https://codeberg.org/Lyall/MetaphorFix>).
  Installs as an ASI plugin (bundled `winmm.dll` ASI loader) or as a Reloaded-II package. C++. The GitHub repo is
  archived and points to Codeberg (<https://github.com/Lyall/MetaphorFix>, MIT per the GitHub API).
- Latest: 0.8.5, 2025-06-04 (Codeberg releases API). Game version: v0.8.4 notes say a fix "broke after the 1.09 game
  update" (<https://github.com/Lyall/MetaphorFix/releases/tag/v0.8.4>); no release states a current supported build.
- Useful as a reference for a hook-based fix on this game, not as a loader.

### Trainers, cheat tables and save editors

- A Cheat Engine table is listed on GamePressure
  (<https://www.gamepressure.com/download/metaphor-refantazio-cheat-table-ct-for-cheat-engine-v9-mod/z714f93>); secondary
  listing only, author source not found `[NEEDS VERIFICATION]`. GameBanana has a "Cheats Menu" mod
  (<https://gamebanana.com/mods/587805>, 2.0.2), not examined.
- No save editor was found.
- Several GitHub repos named like "Metaphor-ReFantazio-Cheat" or offering a "free download" of the game turned up in
  search. They look like spam or malware lures and were not opened or evaluated.

## Excluded

- A modder's online co-op project was reported in the press
  (<https://gamesradar.com/games/jrpg/2024s-best-single-player-game-metaphor-refantazio-threatens-to-be-2025s-best-multiplayer-game-as-one-modder-works-to-add-online-co-op-to-the-jrpg>).
  Online features are out of scope for this project; not investigated.

## Gaps / not found

- **A Metaphor port of Atlus Script Tools (BF/BMD/flowscript):** not found. Atlus Script Tools does not list the game, and
  the game's scripts are Squirrel. MF.Toolkit covers MSG editing instead.
- **AtlusFileSystemLibrary for Metaphor:** no Metaphor reference; repo last pushed 2020-07-09
  (<https://github.com/tge-was-taken/AtlusFileSystemLibrary>).
- **A standalone CPK unpacker documented for Metaphor:** none found with a Metaphor-specific README; CriFs (runtime) and
  cri-archive-lib (reading) are the closest. Whether general CRI tools extract this game's CPKs: `[NEEDS VERIFICATION]`.
- **Nexus Mods pages:** every fetch returned HTTP 403, so Nexus details (including a Nexus copy of MetaphorAPKPack and
  the Vortex claim) come from search summaries only `[NEEDS VERIFICATION]`.
- **Amicitia wiki or another Metaphor-specific wiki:** not found in this survey `[NEEDS VERIFICATION]`.
- **Save editors:** none found.
- **A stated supported game build:** no loader, framework or tool states one. The community's target build is therefore
  unknown `[NEEDS VERIFICATION]`; the best available sign is that these mods target the Steam release (`metaphor.exe`,
  Steam app 2679460).
- **Persona Essentials "p5rpc.modloader":** confirmed as the code Metaphor Essentials is based on, but it has no
  Metaphor support itself (no "metaphor" hits in <https://github.com/Sewer56/p5rpc.modloader>).

## Recommended starting point

Build new mods as **Reloaded-II** mods (<https://github.com/Reloaded-Project/Reloaded-II>) that depend on the existing
Metaphor stack instead of writing a loader. For file replacement, depend on **CRI FileSystem V2 Hook**, which is
maintained (2.6.1, 2026-06-14) and lists the game (<https://sewer56.dev/CriFs.V2.Hook.ReloadedII/>), plus **Metaphor
Essentials** for `.tbl` merging (<https://github.com/DeathChaos25/mrfpc.modloader>). For messages and Squirrel scripts,
use **MF.Toolkit** (<https://gamebanana.com/mods/548487>); for audio and movies, **Ryo** (<https://github.com/RyoTune/Ryo>).
For code hooks, **OpenGFD with riri-mod-tools** is the most active Metaphor-specific framework, and riri-mod-tools
already supports per-executable-hash hooks (<https://github.com/rirurin/riri-mod-tools>), which fits this repo's rule
to check the build before patching. Two cautions: Metaphor Essentials has had no commits since 2024-11-15, and none of
these tools states which game build it was verified on, so the first step should be to confirm on the current build
that the stack loads before building on it `[NEEDS VERIFICATION]`.
