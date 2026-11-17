# Prism v0.18.3: the `output` call (speech plus braille)

- **Source:** <https://github.com/ethindp/prism> (MPL-2.0), tag `v0.18.3` = commit
  `94329ebeccbaee7f70b3494a290c9dfaac0184ee`, read with
  `gh api "repos/ethindp/prism/contents/<path>?ref=94329ebeccbaee7f70b3494a290c9dfaac0184ee"`. The tag is the one pinned
  in `src/Metaphor.Hooks/prism.json`.
- **Saved:** 2026-11-16
- **Game build:** none (this note is about a third-party library)
- Not run: nothing was built or executed; only source was read. Whether a braille display shows the text is untested.

## Exported function

`include/prism.h` lines 408-411 declare:

```c
PRISM_API PRISM_NODISCARD PRISM_NONNULL(1, 2)
    PRISM_NULL_TERMINATED_STRING_ARG(2) PrismError PRISM_CALL
    prism_backend_output(PrismBackend *backend, const char *PRISM_RESTRICT text,
                         bool interrupt);
```

Same shape as `prism_backend_speak` (lines 392-395). `PRISM_CALL` is `__cdecl` on Windows (line 30). The header also
exports `prism_backend_braille(PrismBackend *backend, const char *text)` (lines 403-406), braille alone.

## Status codes

`PrismError` (lines 117-143) is a C enum counting from `PRISM_OK = 0`, so `PRISM_ERROR_NOT_INITIALIZED` = 1,
`PRISM_ERROR_INVALID_PARAM` = 2, **`PRISM_ERROR_NOT_IMPLEMENTED` = 3**.

## Behaviour (`source/`)

- `source/prism.cpp` lines 342-348: `prism_backend_output` returns `PRISM_ERROR_INVALID_UTF8` for invalid UTF-8,
  otherwise calls the backend's `output` and maps its error.
- `source/backend.h` lines 145-148: the base backend's `output` returns `BackendError::NotImplemented`, so a backend
  that doesn't override it answers NOT_IMPLEMENTED.
- The five backends on ADR 0005's list all override `output`:
  - `source/backends/nvda.cpp` lines 216-222 and `jaws.cpp` lines 118-124: `speak`, then `braille`; each returns the
    first error. So if braille fails, the line has **already been spoken** when the error comes back.
  - `onecore.cpp` line 324, `sapi.cpp` lines 752-754, `zoom_text.cpp` lines 125-127: `output` is `speak` alone.

## Consequence for the mod

With today's list, every backend implements `output`, so the fallback to `speak` on NOT_IMPLEMENTED is a guard for a
future backend or Prism version. Falling back on any other error would speak a line twice on NVDA or JAWS when braille
fails.
