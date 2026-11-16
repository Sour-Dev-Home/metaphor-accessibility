# Reloaded-II mod entry point

- **Source:** Reloaded-II source on GitHub at commit `458987f26bc8d78fdc0f86b73fc0d6fd4b673d01` (`master` on the date
  below), read via the GitHub REST API (`gh api .../contents/<path>?ref=<commit>`), and the NuGet version list for
  `Reloaded.Mod.Interfaces` (`https://api.nuget.org/v3-flatcontainer/reloaded.mod.interfaces/index.json`). Nothing was
  downloaded or run.
- **Saved:** 2026-11-15.
- **Game build:** none; this is about the loader, not the game.

`B` below means <https://github.com/Reloaded-Project/Reloaded-II/blob/458987f26bc8d78fdc0f86b73fc0d6fd4b673d01>, and `T`
means `B/source/Reloaded.Mod.Template/templates/configurable`. Builds on `reloaded-ii-build-setup-2026-11-13.md` (the
template's packages) and `reloaded-ii-app-and-mod-config-2026-11-15.md` (`ModConfig.json`, `ModId`). Checked for #43.

## How the loader finds and starts a mod

- It loads the mod's managed DLL from `ModConfig.json`: `ModDll`, relative to the folder holding `ModConfig.json`
  (`B/source/Reloaded.Mod.Loader.IO/Config/ModConfig.cs#L167-L174`). `ModR2RManagedDll32`/`64` are used instead only
  when one is set and that file exists (`#L213-L217`).
- The entry point is the first non-abstract type in that assembly assignable to `IModV1`
  (`B/source/Reloaded.Mod.Loader/Mods/PluginManager.cs#L274-L289`), created with `Activator.CreateInstance` (`#L255`),
  so it needs a public parameterless constructor.
- The loader calls `Start` on it. An exception from it is logged as `Error while starting mod: <id>` and rethrown
  (`PluginManager.cs#L333-L346`). So the entry point must catch its own failures. What a rethrow does to the game is
  [NEEDS VERIFICATION].
- `IMod` is `IModV1` plus `IModV2` (`B/source/Reloaded.Mod.Interfaces/IMod.cs#L3`). `IModV2.StartEx(IModLoaderV1 loader,
  IModConfigV1 config)` defaults to calling `Start(loader)` (`B/source/Reloaded.Mod.Interfaces/Internal/IModV2.cs#L10`),
  and `IModV1.Start` defaults to doing nothing (`Internal/IModV1.cs#L14`).
- Which one runs: `ModInstance.Start` calls `StartEx(loader, ModConfig)` when the mod is an `IModV2`, and `Start(loader)`
  otherwise (`B/source/Reloaded.Mod.Loader/Mods/Structs/ModInstance.cs#L71-L80`). So an `IMod` needs only `StartEx`.
  `IModV1` also has `Disposing`, `Suspend`, `Resume`, `Unload`, `CanUnload` and `CanSuspend`
  (`Internal/IModV1.cs#L6-L60`).
- The template implements `IMod` in `T/Template/Startup.cs`: `StartEx` casts the loader to `IModLoader` and the config
  to `IModConfig`, and gets its logger with `(ILogger)_modLoader.GetLogger()`. It answers `CanUnload` and `CanSuspend`
  from the mod; the comment says `false` disables those buttons in the launcher and the calls never come.

## Loader API the entry point needs

- `IModLoaderV2.GetDirectoryForModId(string modId)` returns the folder holding that mod
  (`B/source/Reloaded.Mod.Interfaces/Internal/IModLoaderV2.cs#L10`). The loader fills it with the mod's full folder
  path (`PluginManager.cs#L325-L328`).
- `IModLoaderV1.GetLogger()` returns an `ILoggerV1` (`Internal/IModLoaderV1.cs#L28`). `ILoggerV2` adds
  `WriteLine(string message)` (`Internal/ILoggerV2.cs#L16`).

## Template project and `ModConfig.json`

- The template's project references `Reloaded.Mod.Interfaces` 2.4.0 with `ExcludeAssets="runtime"`, so the loader
  supplies that assembly at run time (`T/Reloaded.Mod.Template.csproj#L47`). It copies `ModConfig.json` to the output
  as `Content` with `CopyToOutputDirectory` `Always` (`#L37-L39`).
- The template's `ModConfig.json` fields (`T/ModConfig.json`): `ModId`, `ModName`, `ModAuthor`, `ModVersion` (a string,
  `"1.0.0"`), `ModDescription`, `ModDll`, `ModIcon`, `ModR2RManagedDll32`, `ModR2RManagedDll64`, `ModNativeDll32`,
  `ModNativeDll64`, `IsLibrary`, `ReleaseMetadataFileName`, `PluginData`, `IsUniversalMod`, `ModDependencies` (the
  template lists `reloaded.sharedlib.hooks`), `OptionalDependencies`, `SupportedAppId`, `ProjectUrl`.
- `Reloaded.Mod.Interfaces` at this commit is version 2.5.0 and targets `netcoreapp3.1`
  (`B/source/Reloaded.Mod.Interfaces/Reloaded.Mod.Interfaces.csproj#L4`, `#L16`). NuGet lists 2.5.0 as the newest
  version. Whether a mod built against 2.5.0 loads in an older launcher is [NEEDS VERIFICATION].

## A Metaphor mod for comparison: RyoTune/MF.Toolkit

Read at commit `b1f876da5b4c1f42b5eef07fc6c2174030beb77b` (`main` on the date above). `M` means
<https://github.com/RyoTune/MF.Toolkit/blob/b1f876da5b4c1f42b5eef07fc6c2174030beb77b/MF.Toolkit.Reloaded>.

- Same entry point shape as the template: `public class Startup : IMod` with `StartEx(IModLoaderV1, IModConfigV1)`
  (`M/Template/Startup.cs#L14`, `#L51`).
- Same package setup: `Reloaded.Mod.Interfaces` 2.4.0 with `ExcludeAssets="runtime"` (`M/MF.Toolkit.Reloaded.csproj#L51`).
- Its `ModConfig.json` sets `ModVersion` as a fixed string (`"1.2.6"`, `#L5`), `ModDll` to its assembly (`#L7`), and
  `SupportedAppId` to `["metaphor.exe"]` (`#L74-L76`).
