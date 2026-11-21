# FF6ScreenReader: its audio approach, and why none of its code is used

- **Source:** https://github.com/BlindGuyNW/FF6ScreenReader at commit `40a3427140c3fc481dab1609cd2f1f33df9b4617`
  (2026-03-17), checked with `gh api repos/BlindGuyNW/FF6ScreenReader` (its `license` field is null: the repository has
  no licence) and `gh api repos/BlindGuyNW/FF6ScreenReader/git/trees/HEAD?recursive=1` for its file names. The approach
  is as #637 describes it.
- **Saved:** 2026-11-21.
- **Game build:** none; this is another game's mod (Final Fantasy VI Pixel Remaster), not Metaphor.

## What was taken

Only the idea, from #637: generate sine tones in code and play them through Windows' waveOut, one handle per channel,
with stereo panning, so navigation sounds overlap and have a direction.

Its audio files, by name only: `FFVI_Mod/Utils/SoundPlayer.cs`, `FFVI_Mod/Utils/AudioChannel.cs`,
`FFVI_Mod/Utils/ToneGenerator.cs`, `FFVI_Mod/Core/AudioLoopManager.cs`.

## What wasn't

Unlicensed code is all rights reserved, so for #637 none of those files was opened, and nothing was copied or
paraphrased. `CueMixer`, `WaveSlots` and `WaveOutHandle` are written from Microsoft's documentation
([winmm waveOut](winmm-waveout-2026-11-21.md)).
