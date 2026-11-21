---
status: proposed
---

# Sound cues are tones generated in code, played through winmm's PlaySound

Proposed in #636's PR, following the maintainer's design in the issue (2026-11-21).

`Metaphor.Core` builds each cue as an in-memory WAV of sine notes (`CueWave`): 16-bit mono, each note faded over 5 ms
so it doesn't click, every cue under 200 ms. No audio files, so nothing to license or ship. The hook layer plays it with
`PlaySoundW` from System32's `winmm.dll`, synchronously, on a thread `CuePlayer` owns. `Play` only queues and returns,
so the game thread never waits; with two cues waiting, a new one is dropped, as a late cue describes the wrong moment.
Windows mixes the sound with the screen reader's speech. Sources:
[winmm PlaySound](../raw-sources/winmm-playsound-2026-11-21.md).

Synchronous playback keeps the native call safe: the array stays valid until the call returns, so `PlaySoundW` is listed
under architecture rule 10.

## Considered options

- **SND_ASYNC:** no thread of our own, but the buffer must outlive the call: pinned memory and a guess at its end.
- **waveOut or WASAPI:** cues could overlap, but buffers, callbacks and handles in the hook layer for 0.1 s sounds.
- **A library such as NAudio:** a pinned dependency under ADR 0005's rules for what one system call does.

## Consequences

Cues play one at a time, each waiting at most 200 ms. Volume scales the samples, separate from the game's and Windows'.
Another `PlaySound` call in the game process would cut ours off; the game isn't known to make one
`[NEEDS VERIFICATION]`. Overlapping cues would mean a waveOut sink behind the same `ICueSink`.
