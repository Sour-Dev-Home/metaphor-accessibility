# Elevation and front/back cues for our own sounds: an outside advice list checked against primary sources (#834, #903)

- **Source:** Steam Audio's documentation at `valvesoftware.github.io/steam-audio/doc/` (C API pages `guide.html`,
  `binaural-effect.html`, `hrtf.html`; Unreal page `unreal/spatialization-settings.html`) and its repository
  `ValveSoftware/steam-audio` read through `gh api` (licence `Apache-2.0`, latest release `v4.8.1` of 2026-02-11,
  `master` at `f943b4204b876bc3096b1c3d308784536d461876`); open-access papers on PubMed Central and their abstracts or
  bibliographic records (each named below); Riot's "Ask VALORANT - Apr. 12" post on `playvalorant.com` (2021); GDC
  Vault's listing of Blizzard's GDC 2016 talk; Ubisoft's and the press's reports on Rainbow Six Siege's sound rework.
  Read with web search and page fetches; nothing downloaded or run, nobody contacted.
- **Saved:** 2026-11-24.
- **Game build:** none (general psychoacoustics and other products; nothing here is pinned to a Metaphor: ReFantazio
  build).

The maintainer was handed a list of advice on making height audible and asked for each claim to be checked. Where only
an abstract or a later paper's summary of a study was read, the row says so; where nothing primary was found, it says
`[NEEDS VERIFICATION]` or "unsupported".

## Sources read

1. **Steam Audio programmer's guide** (`capi/guide.html`): an HRTF is "a pair of filters" for each direction, encoding
   "the subtle changes to amplitude, arrival time, and spectral content" from head, torso and outer ear. It "provides
   a built-in HRTF" (`IPL_HRTFTYPE_DEFAULT`); custom HRTFs load from SOFA files, "a restricted subset"
   (`SimpleFreeFieldHRIR`). "Spatial audio is best experienced over headphones."
2. **Steam Audio binaural effect** (`capi/binaural-effect.html`): `IPL_HRTFINTERPOLATION_NEAREST` is
   "Nearest-neighbor filtering, i.e., no interpolation"; `IPL_HRTFINTERPOLATION_BILINEAR` is "Bilinear filtering",
   which "incurs a relatively high CPU overhead" and "is most useful for wide-band noise-like sounds".
   `IPLBinauralEffectParams` takes a `direction` ("Unit vector pointing from the listener towards the source"), the
   `interpolation`, a `spatialBlend` (0 is unspatialized, 1 fully) and the `hrtf`.
3. **Steam Audio Unreal spatialization settings**: bilinear "uses an HRTF generated after interpolating from four
   directions nearest to the direction of the source". Unreal's API page for the same enum (seen in search results,
   not fetched) says it "can result in a smoother sound as the listener rotates". Neither page mentions elevation.
4. **Hebrank & Wright (1974)**, "Spectral cues used in the localization of sound sources on the median plane", JASA 55,
   1829 ff. Abstract only (Duke Scholars record): spectra "between 4 and 16 kHz are required" for median-plane
   localization, with front, above and behind cues described. The band-by-band details were not read
   `[NEEDS VERIFICATION]`. Macpherson & Sabin (2013, Hearing Research 306, PMC4107714) cite it: "The primary spectral
   cues for vertical-plane localization lie above 4–6 kHz".
5. **Blauert (1969/70)**, directional bands: not read in the original. Later work summarised in search results (an
   Aalto ICA 2022 paper and others) reports 1/3-octave noise bands heard at a fixed direction whatever the source's
   position: about 8 kHz "above", 500 Hz and 4 kHz "front", 1 kHz (and about 10 kHz) "rear", varying between listeners.
   `[NEEDS VERIFICATION]` against Blauert's *Spatial Hearing*.
6. **Dobreva, O'Neill & Paige (2011)**, "Influence of aging on human sound localization", J Neurophysiol 105, 2471
   (PMC3094163): "As sound bandwidth is reduced, auditory spatial performance declines primarily in elevation and
   front-back discrimination", tied to "spectral notches in the 6- to 12-kHz range". Young listeners' vertical gain
   (1 is ideal): 0.28 for 0.1–1 kHz noise, 0.47 for 10–20 kHz, 0.69 for 3–10 kHz.
7. **Tollin, Ruhland & Yin (2013)**, J Neurophysiol 109, 1658 (PMC3602938), on cats, citing Roffler & Butler (1968,
   JASA 43, 1255) for humans: perceived elevation "often correlates more with the frequency spectra of the stimulus"
   than with the source's position, and tones "were never as easy to localize in elevation" as in azimuth.
8. **Parise, Knorre & Ernst (2014)**, "Natural auditory scene statistics shapes human spatial hearing", PNAS 111, 6104
   (abstract): natural scenes show "a clear mapping between frequency and elevation", mirrored in the outer ear's
   filtering and in perceived location. This is the higher-pitch-is-higher effect.
9. **Wenzel, Arruda, Kistler & Wightman (1993)**, "Localization using nonindividualized head-related transfer
   functions", JASA 94 (abstract, NASA NTRS record): with one other person's HRTFs, once front-back confusions were
   resolved, 12 of 16 listeners localized about as well as in free field. Romigh & Simpson (2014, Front. Neurosci. 8,
   370) summarise this and later work: non-individual HRTFs give "especially poor elevation localization and frequent
   confusions about the front-back hemisphere"; in their own data, 20% front-back reversals with a mannequin's cues
   against about 10% with the listener's own.
10. **Wightman & Kistler (1999)**, "Resolution of front–back ambiguity in spatial hearing by listener and source
    movement", JASA 105, 2841 (summaries of the abstract): front-back confusions disappeared when the listener moved
    the head, or moved the source themselves; not when someone else moved it.
11. **Begault, Wenzel & Anderson (2001)**, JAES 49, 904 (later papers' summaries; the NASA PDF was too large to fetch):
    head tracking roughly halved front-back reversals; reverberation nearly removed inside-the-head judgements but did
    not change reversals and somewhat lowered accuracy.
12. **Riot, "Ask VALORANT - Apr. 12" (2021)**, Audio Director Peter Zinda: HRTF "does not affect attenuations (volume
    change over distance) or occlusion", and "Adjusting to HRTF does take time and practice." Patch 2.06's notes, as
    quoted by the press, put only footsteps, reloads and respawns through HRTF. Nothing on height-driven filtering.
13. **Blizzard, "Overwatch – The Elusive Goal: Play by Sound"** (GDC 2016, Scott Lawlor and Tomas Neumann; GDC Vault
    listing and press write-ups): a threat-importance mix, occlusion that muffles sounds behind walls, distinct
    footsteps, and Dolby Atmos for headphones. The press quotes hearing "the Pharah flying above your head", but no
    height-driven filter was found.
14. **Ubisoft, Rainbow Six Siege**: the 2020 status report on rebuilding sound propagation, and the Siege X (2025)
    audio overhaul of "audio propagation and reverberation" (press quotes of the creative director). Nothing on
    above/below filtering was found.

## The claims, verdicted

| Claim | Verdict | Evidence |
|---|---|---|
| Steam Audio applies HRTF spectral cues for elevation | Partly | Its HRTF filters carry "spectral content" per direction (1), so yes in principle; the built-in HRTF is one generic set, and generic HRTFs place height poorly (9). The docs never mention "notches" or elevation |
| Steam Audio supports bilinear HRTF interpolation for smooth vertical movement | Partly | Bilinear exists, interpolating the four nearest measured directions (2, 3), at higher CPU cost, "most useful for wide-band noise-like sounds". "Smoother as the listener rotates", not vertical movement in particular |
| Good HRTF gives only ~60–70% elevation reliability on flat-screen games without head movement | Unsupported as a number | The direction is right: generic HRTFs give poor elevation and more front-back reversals (9), and movement fixes reversals (10, 11). No source found for 60–70% |
| High-pass swept ~80 → 250–300 Hz (12 dB/oct) as the source rises | Unsupported as a spatial cue | Elevation cues live above 4 kHz (4, 6); cutting below 300 Hz moves no pinna cue. As a learned brightness code it fits the frequency–height mapping (7, 8), so it may still work as a symbol |
| +2 to +4 dB presence boost around 9–10 kHz when above | Partly | An 8 kHz band is heard "above" (5), but about 10 kHz is reported as "rear" (5): a 9–10 kHz boost may read as behind. A boost near 8 kHz has better support `[NEEDS VERIFICATION]` |
| Steeper low-pass for sources below | Partly | Low-passed sounds lose elevation information and are heard low (6, 7, 8), so "muffled = below" fits; no source found for the specific slope |
| "Bright/thin = above, muffled/bassy = below" | Confirmed as a perceptual bias | Higher frequency content is heard higher (7, 8) |
| Distinct sounds for above / level / below | No primary source; design choice | Follows from 7 and 8 failing for narrow sounds: an explicit symbol does not depend on the listener's ears |
| 15–30 ms bright slapback for sources high up | Unsupported | Reflections help externalization, not reversals (11); nothing found tying short reflections to height |
| Narrow the pan (~85%) as the source nears overhead | Partly | Overhead, the interaural differences shrink to zero as the source nears the median plane (the reason front-back and up-down confusions exist, 9, 10), so narrowing follows geometry; 85% has no source |
| Assets must be broadband (cues in ~6–12 kHz); pure low tones carry no elevation | Confirmed | 4, 6, 7; Steam Audio's own note that bilinear helps "wide-band noise-like sounds" (2) |
| Valorant, Overwatch, Siege use such elevation tricks | Unsupported | Valorant: HRTF on footsteps, no filter changes (12). Overwatch: occlusion, importance mix, Atmos (13). Siege: propagation and reverb (14). None found describing height-driven EQ |

## Speakers, not headphones

- Steam Audio's binaural output is meant for headphones (1). On stereo speakers each ear hears both channels, so the
  HRTF's per-ear differences are blurred `[NEEDS VERIFICATION]` for Steam Audio's own speaker paths, which weren't
  read.
- Height-driven EQ (brightness up, muffle down) and the #903 behind-muffle are the same signal in both ears, so they
  survive speakers. Small or laptop speakers may roll off the top octave, which would hide an 8–10 kHz cue
  `[NEEDS VERIFICATION]` by ear.
