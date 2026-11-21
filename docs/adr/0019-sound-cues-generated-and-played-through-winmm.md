---
status: proposed
---

# Sound cues are tones generated in code, played through winmm's waveOut, one handle per channel

Proposed in #636's PR from the maintainer's design (2026-11-21); channels and panning from #637.

`Metaphor.Core` builds cues as sine notes (`CueWave`): 16-bit PCM, faded over 5 ms against clicks, under 200 ms, placed
by equal-power panning. No audio files to license or ship. `CueMixer` has a fixed set of channels: two for one-shot cues
and one per `ToneChannel` for held tones (#183, #185). Each is its own waveOut handle from System32's `winmm.dll`, so
sounds overlap and Windows mixes them with speech. Held tones stream in four 20 ms buffers: pitch keeps its phase,
loudness and position glide, and a change is heard within 80 ms. `CuePlayer`'s own thread owns the mixer; `Play`,
`Hold` and `Silence` only queue, so the game never waits. A cue arriving with four waiting, or both cue channels busy,
is dropped. Sources: [winmm waveOut](../raw-sources/winmm-waveout-2026-11-21.md).

`WaveOutHandle` pins its headers and buffers from open to close; `WaveSlots` reuses a slot only after
`waveOutUnprepareHeader` returns it, and closes only after `waveOutReset` returns every slot. A handle that won't close
keeps its pins. So the six waveOut functions pass only the mod's own memory (architecture rule 10).

## Considered options

- **PlaySound** (#636's first version): one sound at a time per process, mono.
- **WASAPI or XAudio2:** lower latency, but COM in the hook layer for a few tones.
- **NAudio:** a pinned dependency for six system calls.

## Consequences

At most four handles, each with 80 ms of buffers. Volume scales the samples. The approach comes from BlindGuyNW's
FF6ScreenReader (`CREDITS.md`), whose code is unlicensed and was not read.
