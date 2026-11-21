---
status: proposed
---

# Sound cues are tones generated in code, played through winmm's waveOut, one handle per channel

Proposed in #636's PR, following the maintainer's design in the issue (2026-11-21); channels and panning from #637.

`Metaphor.Core` builds each cue as sine notes (`CueWave`): 16-bit PCM, each note faded over 5 ms so it doesn't click,
every cue under 200 ms, placed by equal-power panning (left gain cos θ, right sin θ, θ from 0 to π/2 as the pan goes
from -1 to +1). No audio files, so nothing to license or ship. `CueMixer` has a fixed set of channels, two for one-shot
cues and one per `ToneChannel` for held tones, such as guidance (#183) and walls (#185). Each channel is its own
waveOut handle from System32's `winmm.dll`, so sounds overlap and Windows mixes them with the screen reader's speech. A
held tone streams in 20 ms buffers, four queued: pitch changes keep the wave's phase, and loudness and position glide
across a buffer, so changes don't click and are heard within 80 ms. `CuePlayer` owns the one thread that touches the
mixer; `Play`, `Hold` and `Silence` only queue and return, so the game thread never waits. A cue arriving with four
waiting, or with both cue channels busy, is dropped, as a late cue describes the wrong moment. Sources:
[winmm waveOut](../raw-sources/winmm-waveout-2026-11-21.md).

The buffers are the native-call risk. The hook layer's `WaveOutHandle` pins each header and sample buffer from open to
close, and `WaveSlots` writes a slot only after `waveOutUnprepareHeader` hands it back, and closes only after
`waveOutReset` has returned every slot. A handle that won't close keeps its buffers pinned for good. So the six waveOut
functions pass only the mod's own memory and are listed under architecture rule 10.

## Considered options

- **PlaySound** (#636's first version): simplest, but one sound at a time per process, in mono.
- **WASAPI or XAudio2:** lower latency, but COM interfaces in the hook layer for a few tones.
- **A library such as NAudio:** a pinned dependency under ADR 0005's rules for what six system calls do.

## Consequences

At most four handles are open, each holding 80 ms of buffers. Volume scales the samples, separate from the game's and
Windows'; a held tone's own volume multiplies the setting's. The approach of one waveOut handle per channel with stereo
panning comes from BlindGuyNW's FF6ScreenReader (`CREDITS.md`), whose code is unlicensed, so none was read or copied.
