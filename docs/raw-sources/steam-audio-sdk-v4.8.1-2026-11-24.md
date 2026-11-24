# Steam Audio v4.8.1: release files, Windows x64 binaries, licences and the binaural C API (#834)

- **Source:** the repository `ValveSoftware/steam-audio` at tag `v4.8.1` (commit
  `0da18255cca520771f363ee01f100572b39a308e`), read through `gh api`: the latest release's metadata and asset digests,
  `LICENSE.md`, `TRADEMARK_RIGHTS.md`, `core/THIRDPARTY.md`, `core/CMakeLists.txt` and `core/doc/getting-started.rst`.
  The SDK zip's file list was read from its central directory with HTTP range requests (Python's `zipfile` over a
  ranged reader); no file in it was downloaded or run.
- **Saved:** 2026-11-24.
- **Game build:** none (a third-party library; nothing here depends on a Metaphor: ReFantazio build).

## Release

- Latest release `v4.8.1`, published 2026-02-11. The C SDK is `steamaudio_4.8.1.zip`, 181,171,027 bytes,
  `sha256:4a0aa5ec1176f38f0b0993a37c2259d9e86f27e22d5e24f83ec4c3cb9a1d5449` (GitHub's asset digest). The other assets are
  the FMOD, Unity, Unreal and Wwise plugins.
- Its Windows x64 files, uncompressed sizes in bytes:

  | Path in the zip | Bytes |
  |---|---|
  | `steamaudio/lib/windows-x64/phonon.dll` | 52,950,168 |
  | `steamaudio/lib/windows-x64/phonon.lib` | 94,186 |
  | `steamaudio/lib/windows-x64/TrueAudioNext.dll` | 5,221,888 |
  | `steamaudio/lib/windows-x64/GPUUtilities.dll` | 44,032 |
  | `steamaudio/symbols/windows-x64/phonon.pdb` | 24,989,696 |
  | `steamaudio/THIRDPARTY.md` | 36,284 |

- `getting-started.rst` lists Windows 7 or later, 32-bit and 64-bit, with `phonon.dll` under `SDKROOT/lib/windows-x64`.

## Licences

- `LICENSE.md`: Apache License, Version 2.0 (GitHub reports `Apache-2.0`).
- `TRADEMARK_RIGHTS.md`: "Steam Audio" is a Valve mark; the Apache licence grants no licence to it beyond "reasonable
  and customary use in describing the origin of the Steam Audio code base", and not in a way implying endorsement.
- `core/THIRDPARTY.md` sections: Intel Integrated Performance Primitives (IPP), FFTS, PFFFT, MySOFA, Intel Embree
  (Apache-2.0), AMD RadeonRays (MIT-style), AMD TrueAudio Next (MIT), CIPIC HRTF Database, Google Spherical Harmonics
  Library (Apache-2.0).
- IPP's section is the "Intel Simplified Software License (Version October 2022)": the software "is provided in binary
  form only", may be redistributed "without modification", and "No reverse engineering, decompilation, or disassembly
  of the Software is permitted, nor any modification or alteration of the Software or its operation at any time".
- CIPIC: "permission to reproduce and/or use materials available therein for any purpose- educational, research or
  commercial", each reproduction keeping the copyright notice.

## Build options (`core/CMakeLists.txt`)

- On Windows x86/x64, Linux x86/x64 and macOS: `STEAMAUDIO_ENABLE_IPP` ("Enable Intel IPP for FFT and vector math
  operations") defaults `ON`, and `STEAMAUDIO_ENABLE_EMBREE` defaults `ON`. On Windows x64,
  `STEAMAUDIO_ENABLE_RADEONRAYS` and `STEAMAUDIO_ENABLE_TRUEAUDIONEXT` default `ON`.
- If IPP isn't found it is disabled; with neither IPP nor FFTS, the build requires PFFFT (`find_package(PFFFT
  REQUIRED)`).
- `STEAMAUDIO_STATIC_RUNTIME` ("Statically link against the C/C++ runtime library") defaults `ON`.
- Whether the release's prebuilt `phonon.dll` was built with IPP was not checked `[NEEDS VERIFICATION]`; the defaults
  and the shipped `THIRDPARTY.md` suggest it, but neither proves it.

## The binaural path (`core/doc/getting-started.rst`)

- Setup: `iplContextCreate`, then `iplHRTFCreate` with an `IPLAudioSettings` (the sample sets `samplingRate = 44100`,
  `frameSize = 1024`), then `iplBinauralEffectCreate`.
- Per frame: `iplBinauralEffectApply(effect, &effectParams, inBuffer, outBuffer)`, then `iplAudioBufferInterleave` to
  get interleaved output. Input is 32-bit float PCM.
- "Audio buffers in Steam Audio are always *deinterleaved*"; `IPLAudioBuffer.data` points to one array per channel.
  `iplAudioBufferAllocate(context, 2, frameSize, &outBuffer)` allocates a stereo frame.
- Teardown: `iplAudioBufferFree`, `iplBinauralEffectRelease`, `iplHRTFRelease`, `iplContextRelease`.
- The effect's parameters (`direction`, `interpolation`, `spatialBlend`, `hrtf`) and the headphones note are in
  [`elevation-cues-advice-check-2026-11-24.md`](elevation-cues-advice-check-2026-11-24.md), sources 1 to 3.
