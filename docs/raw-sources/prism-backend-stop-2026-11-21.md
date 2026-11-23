# Prism v0.18.3's `prism_backend_stop`, and what it does on the mod's backends (#676)

- **Source:** <https://github.com/ethindp/prism> (MPL-2.0), tag `v0.18.3` = commit
  `94329ebeccbaee7f70b3494a290c9dfaac0184ee` (the tag pinned in `src/Metaphor.Hooks/prism.json`), read with
  `gh api -H "Accept: application/vnd.github.raw" "repos/ethindp/prism/contents/<path>?ref=94329ebeccbaee7f70b3494a290c9dfaac0184ee"`
  for `include/prism.h`, `source/prism.cpp` and `source/backends/{nvda,jaws,onecore,sapi}.cpp`.
- **Saved:** 2026-11-21
- **Game build:** none (this note is about a third-party library)
- Read only; nothing here was heard. ZoomText's backend (`source/backends/zoom_text.cpp`) was not read
  `[NEEDS VERIFICATION]`.

Read for #676: stopping a dialogue line that is still being spoken when the dialogue window closes.

## The call

- `include/prism.h` lines 413-414: `PrismError prism_backend_stop(PrismBackend *backend)`, `PRISM_NONNULL(1)`,
  `PRISM_CALL` = `__cdecl` on Windows (line 30). Same return type as `prism_backend_speak` (`PRISM_OK` = 0).
- `include/prism.h` line 166: the backend vtable's `stop(void *instance)` slot.
- `source/prism.cpp` lines 349-354: exported (`PRISM_API`); it calls the backend's `stop()` and maps its error.

## What each backend's `stop()` does, against its interrupt

| Backend | `stop()` | `speak(text, interrupt = true)` does first |
|---|---|---|
| NVDA | `nvda.cpp` lines 224-230: `nvdaController_cancelSpeech` | lines 181-184: the same call |
| JAWS | `jaws.cpp` lines 126-132: `controller->StopSpeech()` | not read |
| OneCore | `onecore.cpp` lines 339-356: pauses the player and clears its source when playing or paused | lines 250-252: calls `stop()` |
| SAPI | `sapi.cpp` lines 768-775: `voice->Speak(nullptr, SPF_PURGEBEFORESPEAK, nullptr)` | lines 597-598: the same call |

So on NVDA, OneCore and SAPI, `stop` is the cut an interrupting line already makes, with no new text after it.
OneCore keeps one source at a time, so there is no queue behind it. Whether NVDA's cancel and SAPI's purge also drop
text queued behind the line is not in these files `[NEEDS VERIFICATION]`.
