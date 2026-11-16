---
status: accepted
---

# Speech goes through Prism, loaded by full path, with a fixed list of backends

The announcer speaks through Prism (`ethindp/prism`, MPL-2.0), pinned to v0.18.3. The prebuilt `prism.dll` is fetched by
SHA-256 at publish time, never committed, and hash-checked again before the mod loads it by full path from its own
folder, an exception to architecture rule 4. The mod creates backends only from a fixed list: NVDA, JAWS, ZoomText,
OneCore, then SAPI. It never lets Prism pick (`create_best`, `acquire_best`) or poll availability, because the ZDSR,
PC-Talker and Boy PC Reader backends load a DLL by bare name, and in v0.18.3 only when initialised
(`docs/raw-sources/screen-reader-speech-libraries-2026-11-15.md`). An architecture test enforces this. Those readers
stay unsupported, with no upstream fix, until players ask. Ground rule 4 now lets releases ship third-party libraries
under GPL-3.0-compatible licences, each with its licence and a pinned version, never game content.

## Considered options

- **Build Prism from source without those backends.** The strongest guarantee, but it adds a C++ toolchain to every
  build and breaks the byte-identical rebuild check. Revisit if the fixed list stops being enough.
- **Let Prism pick and accept the fallback.** Simplest, but a DLL planted in the game folder or on `PATH` would load.

## Consequences

The guarantee rests on Prism's internals at v0.18.3, so each upgrade re-reads where those backends load DLLs and
re-pins both hashes. Narrator has no Prism backend, so its users hear OneCore. Packages ship Prism's licences and a
`SOURCE.md` naming its source tag; a public release must also offer that source, since `prism.dll` contains NVDA's
LGPL-2.1 controller client.
