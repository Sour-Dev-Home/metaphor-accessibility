# Screen reader speech libraries used by game accessibility mods (Windows)

- **Source:** GitHub REST API (`gh api`) and the READMEs and source files named below, read on the date below. NuGet
  metadata from `https://api.nuget.org/v3-flatcontainer/<id>/index.json` and the package `.nuspec`. Nothing was
  downloaded, built or run.
- **Saved:** 2026-11-15
- **Game build:** none (no game involved).

## Libraries

| Library | License | Activity (GitHub API, 2026-11-15) | Windows screen readers | Form | .NET bindings |
|---|---|---|---|---|---|
| Prism (`ethindp/prism`) | MPL-2.0 | created 2026-01-01; latest release v0.18.3 (2026-09-30); last push 2026-11-14 (HEAD `d4c24ff`) | NVDA, JAWS, ZoomText, Window-Eyes, ZDSR, PC-Talker, Boy PC Reader, plus SAPI, OneCore, UIA, clipboard, GDK speech | native C/C++ library (`prism.dll`) | `prismatoid` (NuGet 0.4.0, MPL-2.0, repo `the-byte-bender/Prismatoid`); `Refractor` (NuGet 0.1.0-preview.2, MIT, repo `trypsynth/refractor`) |
| Tolk (`dkager/tolk`) | LGPL-3.0 | "NOTE: this project is not currently being developed."; no releases; last commit `e5149f0` 2024-06-08 | JAWS, Window-Eyes, NVDA, SuperNova (x86 only), System Access, ZoomText, SAPI | native `Tolk.dll` plus per-reader DLLs | `TolkDotNet.dll` wrapper in the repo |
| NVDA Controller Client (`nvaccess/nvda`, `extras/controllerClient`) | LGPL-2.1 (`license.txt`) | version 3.0 shipped with NVDA 2026.3; latest NVDA release `release-2026.3.1` (2026-11-09); last client commit `6d64b73` 2026-09-10 | NVDA only | native `nvdaControllerClient.dll` | C# example only (`examples/example_csharp`, .NET Standard 2.0) |
| UniversalSpeech (`qtnc/UniversalSpeech`) | MIT | only release v1.0.0 (2015-02-27); last commit `25e8b49` 2025-04-04 | JAWS, NVDA (via `nvdaControllerClient.dll`), Window-Eyes, SAPI 5 | native DLL | none found |
| SRAL (`m1maker/SRAL`) | MIT | last push 2026-08-05 | not read `[NEEDS VERIFICATION]` | not read | not read |
| CrossSpeak (`khanshoaib3/CrossSpeak`) | LGPL-2.1 | last push 2024-02-18 | via Tolk on Windows | C# library over native Tolk | is itself C# |

### Prism

README (<https://github.com/ethindp/prism>, HEAD `d4c24ff787c4e6113a7e4a0e577f86952f65d52f`):

> Prism aims to unify the various screen reader abstraction libraries like SpeechCore, UniversalSpeech, SRAL, Tolk,
> etc., into a single unified system with a single unified API.

> Prism picks the best backend available on the system: the user's screen reader if one is running, or a speech engine
> otherwise.

> For windows, Windows 10 or later is required.

Build options include compatibility shims (`PRISM_ENABLE_TOLK_SHIM`, `PRISM_ENABLE_UNIVERSAL_SPEECH_SHIM`, ...) and
`PRISM_ENABLE_LEGACY_BACKENDS`. Windows backend sources under `source/backends/`: `nvda.cpp`, `jaws.cpp`,
`zoom_text.cpp`, `window_eyes.cpp`, `zdsr.cpp`, `pc_talker.cpp`, `boy_pc_reader.cpp`, `sapi.cpp`, `onecore.cpp`,
`uia.cpp`, `clipboard_windows.cpp`.

How each Windows backend reaches its reader (read from the source at the commit above):

- NVDA: `nvda.cpp` includes `<nvda_controller.h>`, defines `MIDL_user_allocate`, and binds with
  `RpcStringBindingCompose` over `ncalrpc`; the repo carries `idl/nvdaController.idl` and
  `LICENSES/nvdaController/lgpl-2.1.txt`. So the NVDA client RPC stub appears compiled into Prism, with no
  `nvdaControllerClient.dll` to load `[NEEDS VERIFICATION]` (inferred from the source, not from a build).
- JAWS, ZoomText, Window-Eyes, SAPI: `CoCreateInstance` (COM).
- OneCore: `Windows::Media::SpeechSynthesis` (WinRT).
- UIA: a `UiaNotificationProvider` (`IRawElementProviderSimple`), i.e. UI Automation notifications.
- ZDSR, PC-Talker, Boy PC Reader: `open_optional_library(...)` in `source/optional_library.cpp`, which tries Prism's
  own folder, then a folder read from `HKEY_LOCAL_MACHINE` (`RegGetValue`), loading by full path with
  `LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR`, and then falls back to a bare name:

  > Only fall back to Windows' ambient DLL search path after those locations, so a stale same-named DLL in the working
  > directory or PATH cannot shadow the installed library.

  followed by `SharedLibrary library{dll};` (load by bare name). Relevant to a rule that native libraries load only by
  full path or from System32: the fallback would need disabling (e.g. by building without those backends) `[NEEDS VERIFICATION]`.

Refractor README (<https://github.com/trypsynth/refractor>):

> .NET bindings to prism ... On Windows that means NVDA, JAWS, ZoomText, SAPI, OneCore, UI Automation and more.

> With `PublishAot` on, Refractor links prism statically. ... Without Native AOT, prism loads from the package's
> `runtimes` folder.

> Refractor is MIT licensed. The package also contains prism, which is under the Mozilla Public License 2.0

How a non-AOT Reloaded-II mod would resolve `prism.dll` from `runtimes/` (full path or search path) is not documented
`[NEEDS VERIFICATION]`. Refractor was created 2026-09-22 and is at a preview version.

### Tolk

README (<https://github.com/dkager/tolk>, commit `e5149f0`):

> NOTE: this project is not currently being developed.

> The `lib` directory contains the required screen reader API DLLs. Tolk expects these DLLs to be found either in the
> current working directory or somewhere in the `PATH`.

> By default, support for SAPI is disabled. To change this, use `Tolk_TrySAPI`

> Tolk is not thread-safe.

That search-path loading is the opposite of a full-path-only rule. Active forks exist (e.g. `qt06/tolk`, pushed
2026-07-11; `boz700908/tolk`, 2026-10-05; `ndarilek/tolk`, 2020-05-18); none was read.

### NVDA Controller Client

`extras/controllerClient/readme.md` in <https://github.com/nvaccess/nvda>:

> The client API is implemented as a dll (dynamic link library).

> Version 3.0 of the controller client was introduced in NVDA 2026.3 and provides the following new function:
> `nvdaController_isSpeaking`

> Version 2.0 ... `nvdaController_getProcessId`, `nvdaController_speakSsml`

> Developers should be aware that NVDA runs on the lock screen and secure screens. Before providing information to the
> end user (e.g. via `nvdaController_speakText`), developers should check if Windows is locked

### UniversalSpeech

README (<https://github.com/qtnc/UniversalSpeech>):

> Jaws for windows, version 10 or above via FSAPI (installed by default), or vversion 5 to 9 via jfwapi.dll COM control
> NVDA 2011.1 or above, using nvdaControllerClient.dll

## Game access mods and their backend

| Mod | Game / engine | Backend | Evidence |
|---|---|---|---|
| Stardew Access (`khanshoaib3/stardew-access`, GPL-3.0, HEAD `c3568b6`) | Stardew Valley (SMAPI, C#) | CrossSpeak, which uses Tolk on Windows | `stardew-access/ScreenReader/ScreenReaderImpl.cs` starts `using CrossSpeak;`; repo has `LICENSE-TOLK`, `LICENSE-NVDA`. CrossSpeak README: "it uses Tolk for windows" and copies `nvdaControllerClient64.dll`, `SAAPI64.dll`, `Tolk.dll` into `lib\screen-reader-libs\windows` |
| Civ V Access (`rashadnaqeeb/Civ-V-Access`, MIT, HEAD `3c7f36d`) | Civilization V (Lua UI + rebuilt game core DLL) | Tolk, exposed to Lua by a proxy DLL | `src/dlc/UI/Shared/CivVAccess_SpeechEngine.lua`: "Thin wrapper around the tolk global injected by the proxy DLL"; `dist/proxy/lua51_Win32.dll` |
| Voice of the Old Republic (`JeanStiletto/voice-of-the-old-republic`, GPL-3.0, HEAD `98cd6e9`) | KOTOR 1/2 | Prism | README: "It uses the Prism speech bridge to support every screen reader"; loads through "the `dinput8.dll` proxy" |
| Arcana (`ChipsAhoiMcCoy/arcana`, Zlib, HEAD `d4f7c16`) | Persona 3 Reload (Unreal) | Prism | README: "It uses Prism, Steam Audio, Microsoft Detours and miniaudio"; "Without one, the mod uses Windows speech" |

A GitHub repository search for "prism screen reader speech" (2026-11-15) also returned several 2026 game mods naming
Prism as their speech path (Sticky Business, Cultist Simulator, Killer Instinct). Search results only; not read.
