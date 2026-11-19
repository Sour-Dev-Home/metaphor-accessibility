# Sources for the spoken installer (#303): what it downloads, and why not Reloaded-II's own setup

- **Source:** Reloaded-II source at commit `458987f26bc8d78fdc0f86b73fc0d6fd4b673d01` (tag `1.31.0`'s commit, `master`
  on 2026-11-18) read with `gh api repos/Reloaded-Project/Reloaded-II/contents/<path>?ref=<commit>`; GitHub's release
  API for Reloaded-II `1.31.0` and Sewer56/Reloaded.SharedLib.Hooks.ReloadedII `1.16.3`
  (`gh api repos/<owner>/<repo>/releases`); Microsoft's .NET 9 release metadata
  (<https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json>); Microsoft Learn's
  [Redistributing Visual C++ files](https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files?view=msvc-170);
  libarchive's `tar/bsdtar.1` at tag `v3.8.1` (commit `7118f97c26bf0b2f426728b482f86508efc81d02`). Each file in the
  table was downloaded on the date below with `curl`, from the URL given, into an empty scratch folder on Windows 11
  (build 26200), hashed with `sha256sum`, and not run. The two Microsoft installers were also checked with
  `Get-AuthenticodeSignature`: status Valid for both, and `VC_redist.x64.exe`'s signer subject
  `CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US`; the .NET installer's (from a
  second download, same SHA-256) `CN=.NET, O=Microsoft Corporation, L=Redmond, S=Washington, C=US`.
- **Saved:** 2026-11-19.
- **Game build:** none; this is about the loader and its runtimes, not the game.

`R` below means <https://github.com/Reloaded-Project/Reloaded-II/blob/458987f26bc8d78fdc0f86b73fc0d6fd4b673d01>.

## Why the installer doesn't run Reloaded-II's `Setup.exe`

[access-mod-install-and-longevity-2026-11-18.md](access-mod-install-and-longevity-2026-11-18.md) proposed running the
pinned `Setup.exe --nogui`. Its code downloads from unpinned URLs, with no hash check:

- The loader itself: `DownloadReloadedAsync` fetches
  `https://github.com/Reloaded-Project/Reloaded-II/releases/latest/download/Release.zip`
  (`R/source/Reloaded.Mod.Installer.Lib/MainWindowViewModel.cs#L249-L252`), whatever version `Setup.exe` is. With
  `--dependenciesOnly` it still downloads that zip, into a temporary folder, to read its runtime configs
  (`R/source/Reloaded.Mod.Installer.Cli/Cli.cs#L40-L50`).
- The runtimes: `.NET` installers from a URL the `FrameworkDownloader` resolves at run time, and the Visual C++
  redistributables from the `aka.ms` permalinks `https://aka.ms/vs/17/release/vc_redist.x64.exe` and `...x86.exe`
  (`R/source/Reloaded.Mod.Installer.DependencyInstaller/DependencyInstaller.cs#L50-L103`), each run with
  `/install /quiet /norestart` (`#L32-L37`). The class's comment says this "[Requires Admin Permission]" (`#L108-L110`).
- An existing, non-empty install folder aborts the setup with a message box (`MainWindowViewModel.cs#L52-L58`).

So the installer does each of these steps itself, from pinned URLs with recorded hashes.

## What Reloaded-II 1.31.0 needs

- `Release.zip` holds the launcher `Reloaded-II.exe` at its root and the loader in `Loader/X64` and `Loader/X86`. It
  holds no `Mods` folder.
- `Reloaded-II.runtimeconfig.json`: `Microsoft.NETCore.App` and `Microsoft.WindowsDesktop.App` `9.0.0`.
  `Loader/X64/Reloaded.Mod.Loader.runtimeconfig.json`: the same two frameworks at `9.0.8`, `"rollForward": "Minor"`.
  So a .NET 9 Desktop Runtime of 9.0.8 or later runs both. The x86 loader is only for 32-bit games; METAPHOR.exe is
  64-bit, so the installer installs the x64 runtime only. Whether Reloaded-II's window then asks for the x86 one when
  opened is `[NEEDS VERIFICATION]`; `--launch` doesn't open it.
- `reloaded.sharedlib.hooks`, which the mod depends on (its `ModConfig.json`), is not in `Release.zip`. The loader
  loads it on every start and fails without it: "please run the launcher, it'll download"
  (`R/source/Reloaded.Mod.Loader/EntryPoint.cs#L67-L77`). The launcher's window fetches it from the GitHub releases of
  `Sewer56/Reloaded.SharedLib.Hooks.ReloadedII`, asset family `reloaded.sharedlib.hooks`
  (`R/source/Reloaded.Mod.Launcher.Lib/Update.cs#L16-L46`). The release's `.7z` holds `ModConfig.json` (`ModId`
  `reloaded.sharedlib.hooks`, `ModVersion` `1.16.3`) at its root.
- `--launch` writes `ReloadedII.json` itself before it starts the game (`loaderConfig.UpdatePaths`, then
  `IConfig<LoaderConfig>.ToPath`; `R/source/Reloaded.Mod.Launcher.Lib/Startup.cs#L78-L83`). Whether a launcher that has
  never run accepts the minimal `ReloadedII.json` the installer writes (`LauncherPath`, `ApplicationConfigDirectory`,
  `ModConfigDirectory`) and fills in the rest is `[NEEDS VERIFICATION]` by a first run.

## The pinned files

| File | Version | URL | SHA-256 | Published digest |
|---|---|---|---|---|
| Reloaded-II `Release.zip` | 1.31.0 | `https://github.com/Reloaded-Project/Reloaded-II/releases/download/1.31.0/Release.zip` | `c893da3ba8596d9266826bd5ca8aacd25fe3b53d09314324ab65c7b618a05f8c` | GitHub's asset `digest`, the same; and the maintainer's run in [reloaded-ii-log-on-normal-run-2026-11-14.md](reloaded-ii-log-on-normal-run-2026-11-14.md) |
| `Reloaded.Hooks.ReloadedII1.16.3.7z` | 1.16.3 (2024-11-29) | `https://github.com/Sewer56/Reloaded.SharedLib.Hooks.ReloadedII/releases/download/1.16.3/Reloaded.Hooks.ReloadedII1.16.3.7z` | `2b7c2e6118a3f1eb00a2e1e9105397b0d17a118a84596308c3a6a9ff3cb14b1b` | none: GitHub's `digest` is `null` for this older asset, so the hash is from this download only |
| .NET Desktop Runtime x64 | 9.0.23 (2026-11-10, the latest 9.0 and a security release) | `https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/9.0.23/windowsdesktop-runtime-9.0.23-win-x64.exe` | `eefb60bd0f3fb33c0787074cebc0467ffba32bcb01088613121649fc3bdb4a27` | Microsoft's SHA-512 in `releases.json`, `a68fe700452b90fb419d6fd7980453387a60d7dba53850a19d26d1c8b0999c031b918a7406689c91cec41ad797c7d2902d40c0f33aedfd52d40769d355b0a272`; a second download the same day had that SHA-512 (`sha512sum`, all 128 digits) and the SHA-256 at left |
| Visual C++ Redistributable x64 | 14.44.35211.0 (file version) | `https://download.visualstudio.microsoft.com/download/pr/e9604e21-4b79-4096-a4b3-1b63d4b55b4d/CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B/VC_redist.x64.exe` | `cc0ff0eb1dc3f5188ae6300faef32bf5beeba4bdd6e8e445a9184072096b713b` | the URL `https://aka.ms/vs/17/release/vc_redist.x64.exe` redirected to on that date; the path names the same SHA-256 |

## Finding the runtimes

- .NET: `dotnet --list-runtimes` lists each runtime by name, version and folder, as
  `Microsoft.AspNetCore.App 9.0.10 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]`
  (<https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions?pivots=os-windows>). The
  installer reads the same folders, `<Program Files>\dotnet\shared\<framework>\<version>`, without needing `dotnet`.
- Visual C++: "The current installed version number is stored in the
  `HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\{x86|x64|arm64}` key ... (You need
  to check under the Wow6432Node subkey only if you use Regedit to view the version of the installed x86 package on an
  x64 platform.)" and "The version number is stored in the REG_SZ string value Version". The same page: "If a more
  recent version is found, the package isn't installed ... If a package uses the /quiet flag to run, no error message
  appears ... an error result is returned to the caller." So the installer runs it only when the key is missing.

## Unpacking

Windows' own `tar.exe` (`System32`, bsdtar 3.8.9 with liblzma on this build) lists and extracts the `.7z` above. On
older Windows 10 builds its `tar.exe` may lack 7-Zip support: `[NEEDS VERIFICATION]`. bsdtar's manual, on `-P`: "By
default, absolute pathnames (those that begin with a / character) have the leading slash removed both when creating
archives and extracting from them. Also, tar will refuse to extract archive entries whose pathnames contain .. or whose
target directory would be altered by a symlink." The installer never passes `-P`, and checks each file's hash before
unpacking it.
