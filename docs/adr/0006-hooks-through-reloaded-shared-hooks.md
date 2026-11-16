---
status: accepted
---

# Hooks go through Reloaded-II's shared hooks library, installed only by the hook loader

The mod hooks game functions with Reloaded-II's shared hooks library, as the official template does: it depends on
`reloaded.sharedlib.hooks`, references the `Reloaded.SharedLib.Hooks` 1.9.0 package, and gets `IReloadedHooks` from the
loader (`docs/raw-sources/reloaded-ii-shared-hooks-library-2026-11-15.md`). The loader already loads that mod on every
run, so players install nothing extra. Its two interface DLLs (LGPL-3.0) ship with the mod, with their licence and a
pointer to their source, under ground rule 4. Addresses come from our own signature scan (`hook-signatures.json`, read a
chunk at a time with `ReadProcessMemory`), the same signatures the offline check reads. Every hook is a `[GameHook]`
class, and only the hook loader installs it: on a game build its attribute names, where its signature matches exactly
once (ground rule 2). An architecture test fails the build when other hook-layer code uses the hooking library or
writes memory.

## Considered options

- **Another hooking library, or our own.** No benefit over the one the loader already loads, and more to review.
- **Reloaded's signature scanner (`Reloaded.Memory.SigScan`).** One more dependency, and the offline check and the mod
  would no longer share one matcher.

## Consequences

Hook code depends on the shared library's API staying compatible across Reloaded-II updates; the smoke test catches a
break. A failed hook is logged and the rest still install; the status file reports hooks loaded out of hooks expected.
