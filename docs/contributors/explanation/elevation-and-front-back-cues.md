# Height and front/back in the mod's own sounds, without HRTF first

The maintainer was given advice on making height audible (Steam Audio's HRTF, elevation-driven filters, pan narrowing,
broadband sounds) and asked for it to be checked before acting on it (2026-11-24). Each claim, its verdict and its
source are in [`elevation-cues-advice-check-2026-11-24.md`](../../raw-sources/elevation-cues-advice-check-2026-11-24.md).
This page says what that means for the navigation kit (#823). Numbers are issues on
`Sour-Dev-Home/metaphor-accessibility`.

## Where we are

The kit's cues are sine notes and loops (`CueWave`, [ADR 0019](../../adr/0019-sound-cues-generated-and-played-through-winmm.md)),
or files from `tools/Metaphor.NavSounds` (#839) built from the same sines, played through winmm with equal-power stereo
pan and volume. `CameraFrame.Pan` is `-sin(bearing)`: a source dead ahead and one dead behind give the same pan, and
nothing carries height. #650 asks for "above" and "below"; #903 asks for a muffle behind.

## What the evidence says, in short

- **Pure tones carry no height.** Elevation cues are the outer ear's filtering above about 4 kHz; narrowing the
  bandwidth costs elevation and front/back first. A sine has nothing there for an HRTF, or an ear, to shape.
- **Brighter is heard higher, duller lower.** This bias holds even for narrow sounds, so a brightness code works as a
  learned symbol on speakers and headphones alike. The advice's low-frequency high-pass sweep (80 to 300 Hz) has no
  spatial basis; its 9–10 kHz "above" boost sits near a band reported as "behind"; a band near 8 kHz is the one heard
  overhead.
- **Generic HRTFs place height poorly and swap front and back often** (about one trial in five in one study). Moving
  your own head, or moving the source yourself, removes most swaps. The advice's "60–70%" has no source.
- **Steam Audio** does what the advice says in kind (spectral HRTF filters, bilinear interpolation of four measured
  directions), but its docs say it is for headphones and bilinear is for "wide-band noise-like sounds".
- **The game claims are unsupported.** Valorant, Overwatch and Siege are documented using HRTF or Atmos, occlusion and
  propagation, not height-driven EQ.
- **Unsupported, so not planned:** 15–30 ms slapback for high sources, and the exact 85% pan figure.

One point the advice missed: the player turning the camera is the listener moving the source. With held tones that
glide within 80 ms (ADR 0019), turning left moves a sound ahead to the right and a sound behind to the left, so plain
stereo already resolves front/back for anyone who turns. One-shot pings only help if they sound again during the turn.

## Recommended order

| # | Step | Issue | By ear? |
|---|---|---|---|
| 1 | Broadband timbres in the generator: harmonics or a noise layer reaching about 12 kHz on the sonar, ping and wall-tone sounds, each kind still told apart by pitch and rhythm. Everything below depends on it | #839, #827, #830, #831 | Yes: kinds still distinct, not harsh |
| 2 | Behind-muffle: low-pass and a small volume drop past a bearing threshold, the same in both ears so it works on speakers. Pre-rendered "behind" variants from the generator keep tests byte-exact; the sine path needs a one-pole filter in `CueWave` | #903 | Yes: front/back easier |
| 3 | Height as a symbol: above / level / below past a threshold, said in the N list and played as a distinct variant (pitch offset or extra layer), like #904's tiers. Optionally brighter above (near 8 kHz, not 9–10) and low-passed below, which shares #903's filter | #650, #904 | Yes: three heights told apart |
| 4 | Re-ping during a turn: sound one-shot cues again when the camera heading changes past a step, so turning resolves front/back | #827, #831 | Yes, light |
| 5 | Pan narrowing with height: `pan = -sin(bearing) · cos(elevation)`, from the height #650 reads. Geometric, so a unit test covers it; it matters only for large height differences | #650 | Light check |
| 6 | Steam Audio HRTF, behind its native-library ADR, only after 1–5 are heard and players still confuse front/back or height on headphones. It does nothing on speakers, and a generic HRTF adds little height over step 3 | #834 | Yes, on headphones |

Steps 1 to 3 are generator and `Metaphor.Core` work with no new library. Step 6 is a native DLL with a full-path load,
a pinned version (Steam Audio `v4.8.1`, Apache-2.0) and a security review.
