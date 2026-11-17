# Reloaded-II: a mod's user-config folder (`GetModConfigDirectory`)

- **Source:** the `Reloaded.Mod.Interfaces` 2.4.0 NuGet package referenced by `Metaphor.Hooks` (its XML doc file,
  `lib/netcoreapp3.1/Reloaded.Mod.Interfaces.xml`), and a build of `Metaphor.Hooks` against it. Read 2026-11-16.
- **Saved:** 2026-11-16.
- **Game build:** none; this is about the loader.

- `Reloaded.Mod.Interfaces.Internal.IModLoaderV3.GetModConfigDirectory(string modId)` is documented as "Gets the directory
  storing the mod user configuration for a mod with a specified id."
- The `IModLoader` that `ModEntryPoint` already holds exposes it: `modLoader.GetModConfigDirectory(config.ModId)`
  compiles against 2.4.0 with no new reference.
- The folder defaults to `User/Mods/<ModId>` next to the launcher
  (`reloaded-ii-config-location-2026-11-13.md`, `LoaderConfig.cs#L8-L12`), which is where the mod's `ModConfig.json`
  user settings sit. That is separate from the mod's own install folder (`GetDirectoryForModId`), which an update
  replaces.
- [NEEDS VERIFICATION] in the game: the returned path, that the folder exists, and that the settings file survives a
  mod update.
