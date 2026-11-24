---
status: proposed
---

# HRTF for the mod's own sounds goes through Steam Audio, on headphones only, loaded by full path

Proposed for #834 (maintainer, 2026-11-24). It ships only if players on headphones still confuse front and back, or
can't tell height, after hearing `field.behindMuffle` and `field.heightPan` (#914).

A headphones setting, off by default, runs the mod's broadband cues (#839, ADR 0019) through Steam Audio's binaural
effect (Apache-2.0, pinned to `v4.8.1`). Game audio is untouched; speakers keep stereo cues
([why](../contributors/explanation/elevation-and-front-back-cues.md)).

- **Loading:** a second exception to architecture rule 4, like `prism.dll` (ADR 0005). The hook layer loads only
  `phonon.dll`, by full path from the mod's folder after a SHA-256 check, and falls back to stereo on any failure.
- **Seam:** `CuePlacement.Place` passes bearing and elevation beside `HeldTone.Pan`. `CueMixer` hands each channel's
  20 ms slot to a `Metaphor.Core` spatializer interface (mono in, stereo out) that the hook layer implements with
  `iplBinauralEffectApply`, converting to Steam Audio's deinterleaved float buffers
  ([SDK notes](../raw-sources/steam-audio-sdk-v4.8.1-2026-11-24.md)).
- **Licence:** Steam Audio lists Intel IPP under a binary-only, no-modification licence, on by default for Windows x64.
  Whether the prebuilt `phonon.dll` contains it is `[NEEDS VERIFICATION]`; if so, we build it from the tag without IPP
  (PFFFT instead), so everything shipped stays GPL-3.0-compatible (ground rule 4).

## Considered options

- **Stereo cues only** (#914): the default, and works on speakers.
- **Our own HRTF convolution:** no native DLL, but we'd own the filters.

## Consequences

The prebuilt `phonon.dll` is about 53 MB. A generic HRTF still swaps front and back often, so the by-ear check
decides. A source build adds the C++ toolchain ADR 0005 avoided. Upgrades re-check licences and the hash.
