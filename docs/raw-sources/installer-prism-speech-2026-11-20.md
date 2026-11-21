# Prism v0.18.3 from a PowerShell script, and loading a DLL with a restricted dependency search (#601)

- **Source:** <https://github.com/ethindp/prism> (MPL-2.0), tag `v0.18.3` = commit
  `94329ebeccbaee7f70b3494a290c9dfaac0184ee`, read with
  `gh api -H "Accept: application/vnd.github.raw" "repos/ethindp/prism/contents/<path>?ref=94329ebeccbaee7f70b3494a290c9dfaac0184ee"`.
  The tag is the one pinned in `src/Metaphor.Hooks/prism.json`. Microsoft Learn,
  [LoadLibraryExW](https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-loadlibraryexw), read
  from its source, `MicrosoftDocs/sdk-api` file `sdk-api-src/content/libloaderapi/nf-libloaderapi-loadlibraryexw.md` at
  commit `d43984d7c322f7d7d35704d6d1ad36dc3539703b`.
- **Saved:** 2026-11-20
- **Game build:** none (this note is about a third-party library and Windows)
- Not run against a screen reader: nothing here was heard. The import list below is from a string scan, not a PE
  parser.

Read for #601: the installer speaks through the running screen reader with the `prism.dll` the release ships.

## `prism_init` takes no config

`include/prism.h` line 334: `PrismContext *prism_init(PrismConfig *cfg)`. `source/prism.cpp` lines 99-133: `cfg` is
checked against `nullptr` before each use; with `nullptr` the context uses the global registry and starts no backend
enumerator (the availability poll ADR 0005 rules out), which is started only when `cfg->availability_callback` is set.
So a caller can skip `prism_config_init` (line 331), which returns the 56-byte `PrismConfig` by value, and pass `NULL`.
`prism_shutdown(ctx)` (`prism.cpp` line 135) deletes the context.

## The other calls and IDs

`include/prism.h`, all `PRISM_CALL` = `__cdecl` on Windows (line 30):

- line 90: `typedef uint64_t PrismBackendId;`
- lines 260, 261, 269: `PRISM_BACKEND_NVDA` `0x89CC19C5C4AC1A56`, `PRISM_BACKEND_JAWS` `0xAC3D60E9BD84B53E`,
  `PRISM_BACKEND_ZOOM_TEXT` `0xAE439D62DC7B1479` (the same IDs `src/Metaphor.Hooks/PrismSpeech.cs` uses).
- line 369: `PrismBackend *prism_registry_create(PrismContext *ctx, PrismBackendId id)`
- line 381: `void prism_backend_free(PrismBackend *backend)`
- line 390: `PrismError prism_backend_initialize(PrismBackend *backend)`, `PRISM_OK` = 0
- lines 392-395: `PrismError prism_backend_speak(PrismBackend *backend, const char *text, bool interrupt)`, `text`
  NUL-terminated UTF-8 (`prism-output-braille-2026-11-16.md`).

Without `interrupt`, NVDA's backend calls `nvdaController_speakText` alone, with no `nvdaController_cancelSpeech`
first (`prism-backend-redetection-2026-11-20.md`, `source/backends/nvda.cpp` lines 178-197). Whether NVDA then queues
the text after what it is saying or speaks it at once was not checked `[NEEDS VERIFICATION]`.

## What `prism.dll` imports

A string scan of the pinned `prism.dll` (SHA-256 `7c7d09c8...a467`, from `prism-windows-x64.zip`) for `*.dll` names
finds only Windows system DLLs: `ADVAPI32`, `KERNEL32`, `OLEAUT32`, `RPCRT4`, `SHLWAPI`, `UIAutomationCore`, `USER32`,
`XmlLite`, `ole32` and `api-ms-win-*` sets. No `vcruntime140.dll` or `msvcp140.dll`, so it should load before the
installer has put the Visual C++ Redistributable in place `[NEEDS VERIFICATION]` on a PC without it.

## `LoadLibraryExW` flags

From the page above:

- `LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR` = `0x00000100`: "the directory that contains the DLL is temporarily added to the
  beginning of the list of directories that are searched for the DLL's dependencies. Directories in the standard search
  path are not searched. The *lpFileName* parameter must specify a fully qualified path."
- `LOAD_LIBRARY_SEARCH_SYSTEM32` = `0x00000800`: "%windows%\system32 is searched for the DLL and its dependencies.
  Directories in the standard search path are not searched."
