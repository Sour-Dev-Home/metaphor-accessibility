# Piper (piper1-gpl) and its voices: licence, phonemizer, languages, for offline narration (#961)

- **Source:** read with WebFetch (a page-to-markdown tool, so quotes are as the tool returned them):
  `https://github.com/OHF-Voice/piper1-gpl`, `https://raw.githubusercontent.com/OHF-Voice/piper1-gpl/main/docs/LICENSING.md`,
  `https://raw.githubusercontent.com/OHF-Voice/piper1-gpl/main/docs/VOICES.md`,
  `https://api.github.com/repos/OHF-Voice/piper1-gpl/releases/latest`. Nothing was downloaded or run.
- **Saved:** 2026-11-24.
- **Game build:** none (a third-party tool; nothing here depends on a Metaphor: ReFantazio build).

## Engine

- Licence: "The Piper engine is GPL-3.0-or-later." Described as "A fast and local neural text-to-speech engine that
  embeds espeak-ng for phonemization."
- Latest release `v1.8.0`, published 2026-09-04 (target `main`); its commit hash is [NEEDS VERIFICATION].
- Whether generated audio is covered by the GPL: not stated. The licensing page says of the status of output from the
  voices, "Piper takes no position on it." (as summarised by the fetch tool).

## Voices

- "The Piper project makes no license grant of its own over voice models"; "Whatever rights you have in a voice come from
  that voice's training data and from the person who trained it." Each voice has a `MODEL_CARD`; redistribution should
  "carry the MODEL_CARD with each voice".
- Examples given: the lessac base limits use to "research and exploration of speech technologies" and "excludes
  commercial use of the resulting synthesis"; `hifi_tts_92` "is CC BY, so it carries an attribution requirement into
  every voice fine-tuned from it."
- `docs/VOICES.md` (as summarised): voices are at `https://huggingface.co/rhasspy/piper-voices/tree/main`; "Some voices
  may have restrictive licenses, however, so please review them carefully!"; also a line "Piper is intended for personal
  use and text to speech research only" [NEEDS VERIFICATION: the fetch tool's summary, not checked against the raw file].
- Languages: the list had 44 language-locale entries across 39 languages (count by the fetch tool, [NEEDS VERIFICATION]).
- Per-voice licences, model sizes and the licence of any specific English voice: [NEEDS VERIFICATION] (each MODEL_CARD
  was not read).
