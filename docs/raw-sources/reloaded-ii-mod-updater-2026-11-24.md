# Reloaded-II's mod updater: update source, when it checks, its dialog, what it verifies

- **Source:** Reloaded-II's docs and source at commit `458987f26bc8d78fdc0f86b73fc0d6fd4b673d01` (master, commit date
  2026-09-23), and the update library it uses, Sewer56/Update, at `c50357d64cf5f82bed4e6910689403abdae9302d` (main).
  Read through `gh api` and GitHub code search; nothing was downloaded, built or run. URLs below.
- **Saved:** 2026-11-24
- **Game build:** none (this is about the mod loader, not the game).

For #632. Reloaded-II pins `Sewer56.Update.Resolvers.GitHub` 1.6.0 and `Sewer56.Update` 4.1.0
(`source/Reloaded.Mod.Loader.Update/Reloaded.Mod.Loader.Update.csproj`); the Sewer56/Update lines below are read at
its `main`, not checked against those versions [NEEDS VERIFICATION].

## Sources

`R` = `https://github.com/Reloaded-Project/Reloaded-II/blob/458987f26bc8d78fdc0f86b73fc0d6fd4b673d01/`;
`U` = `https://github.com/Sewer56/Update/blob/c50357d64cf5f82bed4e6910689403abdae9302d/`.

- `R/docs/EnablingUpdateSupport.md`, `R/docs/CreatingRelease.md`
- `R/source/Reloaded.Mod.Loader.Update/Providers/GitHub/GitHubReleasesUpdateResolverFactory.cs`
- `R/source/Reloaded.Mod.Launcher.Lib/Setup.cs`, `Startup.cs`, `Update.cs`,
  `Commands/Download/CheckForUpdatesAndDependenciesCommand.cs`, `Models/ViewModel/Dialog/ModUpdateDialogViewModel.cs`
- `R/source/Reloaded.Mod.Launcher/App.xaml.cs`, `LibraryBindings.cs`, `Pages/Dialogs/ModUpdateDialog.xaml`
- `U/Sewer56.Update/Resolvers/Sewer56.Update.Resolvers.GitHub/GitHubReleaseResolver.cs`
- `U/Sewer56.Update/Sewer56.Update.Packaging/Structures/ReleaseMetadata.cs`

## How a mod declares an update source

- The settings are in the mod's `ModConfig.json`: "configurations for updates are part of the mod metadata
  `ModConfig.json`" (`EnablingUpdateSupport.md`). The GitHub resolver's id is `"GitHubRelease"`, and its config class
  `GitHubConfig` has `UserName`, `RepositoryName`, `UseReleaseTag` (default true) and `AssetFileName` (legacy, default
  `"Mod.zip"`) (`GitHubReleasesUpdateResolverFactory.cs`).
- The version is the release tag: "If the GitHub tag version is higher than the local one, there is an update"
  (`EnablingUpdateSupport.md`). The resolver parses it with `new NuGetVersion(x.TagName)`, so the tag must be semver,
  and skips prereleases unless the user allows them (`if (!_commonResolverSettings.AllowPrereleases)`,
  `GitHubReleaseResolver.cs`).
- A release is documented as two files, the packed mod and a JSON metadata file (`CreatingRelease.md`), the metadata's
  default name being `Sewer56.Update.ReleaseMetadata.json` (`ReleaseMetadata.cs`). Without it the resolver falls back
  to an asset matching `AssetFileName`, wildcards allowed (`LegacyFallbackPattern`, `GitHubReleaseResolver.cs`), so an
  ordinary zip release looks usable. Whether the updater lays a plain zip of the mod folder out correctly was not read
  [NEEDS VERIFICATION].

## When the check runs

- At launcher start: `backgroundTasks.Add(Task.Run(CheckForUpdatesAsync)); // Fire and forget` (`Setup.cs`), which
  checks the loader, then mods, then missing dependencies.
- On demand: `CheckForUpdatesAndDependenciesCommand.cs` calls `Update.CheckForModUpdatesAsync`.
- Not on `--launch`: `App.xaml.cs` handles the command line first and, when it was a launch, shuts down before
  `new MainWindow()`; `Startup.cs`'s launch path only starts the game. `Setup.cs` is never reached.

## The update dialog

- `ModUpdateDialog`, a WPF window, shown modal (`window.ShowDialog()`, `LibraryBindings.cs`) over the launcher once the
  background check finds an update (`Update.cs`).
- Controls (`ModUpdateDialog.xaml`): a read-only `DataGrid` with a checkbox column (Enabled) and text columns for name,
  old and new version and size; a `ProgressBar`; one download `Button`; a Markdown viewer for the changelog.
- The dialog sets no `AutomationProperties` and no focus. How NVDA reads it was not tried [NEEDS VERIFICATION].

## What it downloads and verifies

- Releases are listed through the GitHub API (`_client.Repository.Release.GetAll`) and the asset fetched from its
  `BrowserDownloadUrl` with `WebRequest.CreateHttp` (`GitHubReleaseResolver.cs`). Nothing there enforces HTTPS beyond
  the URL GitHub returns.
- No hash or signature of the download is checked in the code read. The one hash use, `HashSet.Verify` for delta
  packages (`ReleaseMetadata.cs`), checks the local files before patching, not the download. `Updater.cs` was not read
  [NEEDS VERIFICATION].
- Lookup errors are swallowed (`catch (Exception) { /* Ignored */ }`), so a failed check offers nothing, silently; the
  docs warn of GitHub's "60 requests/hour per IP for unauthenticated users" (`CreatingRelease.md`).
- Missing dependencies are downloaded without a prompt (`Update.ResolveMissingPackagesAsync`, from `Setup.cs`).
