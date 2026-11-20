# Prism v0.18.3: starting a screen-reader backend again, and what a quit reader returns

- **Source:** <https://github.com/ethindp/prism> (MPL-2.0), tag `v0.18.3` = commit
  `94329ebeccbaee7f70b3494a290c9dfaac0184ee`, read with
  `gh api -H "Accept: application/vnd.github.raw" "repos/ethindp/prism/contents/<path>?ref=94329ebeccbaee7f70b3494a290c9dfaac0184ee"`.
  The tag is the one pinned in `src/Metaphor.Hooks/prism.json`.
- **Saved:** 2026-11-20
- **Game build:** none (this note is about a third-party library)
- Not run: nothing was built or executed; only source and docs were read. Timings below are not measured.

Read for #527: the mod picks its backend once at load, so a screen reader started after the game is never used.

## Every `create` is a fresh instance

`doc/src/api/registry-functions.md` lines 244-270, `prism_registry_create`:

> Each call to `prism_registry_create` creates a new, independent backend instance regardless of whether other
> instances of the same backend exist. This is in contrast to `prism_registry_acquire`, which returns a cached instance
> if one is available.

`source/frozen_registry.cpp` lines 139-143: `FrozenRegistry::create(BackendId)` looks the ID up in its entry list
(`find`, lines 53-58, a read of a list built in the constructor) and calls that entry's factory; it writes nothing
shared, unlike `acquire`'s cache.

So creating and initialising a backend again later is the supported way to ask "is it running now?". Prism's own
availability poll (`prism_availability_*`) is the other way, and ADR 0005 rules it out.

## What `initialize` does when the reader isn't running

`source/prism.cpp` lines 305-309: `prism_backend_initialize` calls the backend's `initialize_tracked()`, which
(`source/backend.h` lines 106-112) calls `initialize()` under a mutex and returns its error.

- `source/backends/nvda.cpp` lines 129-176: builds an `ncalrpc` binding to the endpoint `NvdaCtlr.<session>.<desktop>`
  (the desktop is `GetThreadDesktop(GetCurrentThreadId())`), then calls `nvdaController_testIfRunning(binding)` and
  checks the server supports the controller interface. Either failing returns `BackendNotAvailable` and frees the
  binding. No window search and no COM: one local RPC attempt.
- `source/backends/jaws.cpp` lines 42-59: `FindWindow("JFWUI2", nullptr)`; no window returns `BackendNotAvailable`.
  Otherwise `CoCreateInstance(CLSID_JawsApi)`; `S_OK` succeeds, `REGDB_E_CLASSNOTREG`/`E_NOINTERFACE` return
  `BackendNotAvailable`, any other result `Unknown`.
- `source/backends/zoom_text.cpp` lines 46-71: `FindWindow("ZXSPEECHWNDCLASS", "ZoomText Speech Processor")`; no window
  returns `BackendNotAvailable`. Otherwise `CoCreateInstance(CLSID_ZoomText)` and `get_Speech`, mapped as for JAWS.

Whether `CoCreateInstance` succeeds on a thread that never called `CoInitializeEx` (such as a .NET thread-pool thread)
was not checked here `[NEEDS VERIFICATION]`; if it doesn't, it returns an error other than the two above, so JAWS and
ZoomText answer `Unknown` and are skipped.

## What a started NVDA backend returns once NVDA has quit

`source/backends/nvda.cpp` lines 178-197, `speak`: with `interrupt`, `nvdaController_cancelSpeech` failing returns
`InternalBackendError`; then `nvdaController_speakText` failing returns `InternalBackendError`. Lines 216-222, `output`:
`speak` first, returning its error, then `braille`, whose `nvdaController_brailleMessage` failing also returns
`InternalBackendError` (lines 199-214). `BackendError` (`source/backend.h` lines 21-46) and `PrismError`
(`include/prism.h` lines 117-143) share their numbering, and `to_prism_error` (`source/prism.cpp` lines 56-58) casts
one to the other, so the caller sees `PRISM_ERROR_INTERNAL` (9) either way.

So a failed line can't tell "NVDA is gone, nothing was said" from "spoken, but braille failed" by its status. Whether
the RPC fails at once or waits when NVDA has quit was not checked `[NEEDS VERIFICATION]`.

## Freeing

`source/prism.cpp` lines 289-293: `prism_backend_free` deletes the handle; `NvdaBackend`'s destructor
(`nvda.cpp` lines 75-80) frees its RPC binding.
