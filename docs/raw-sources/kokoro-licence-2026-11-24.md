# Kokoro-82M: licence, training data, voices and phonemizer, for offline narration (#961)

- **Source:** read with WebFetch (a page-to-markdown tool, answers are summaries of the page, so quotes are as the tool
  returned them): `https://huggingface.co/hexgrad/Kokoro-82M` (model card),
  `https://huggingface.co/hexgrad/Kokoro-82M/raw/main/VOICES.md`, `https://github.com/hexgrad/misaki`,
  `https://pypi.org/pypi/kokoro/json`, `https://pypi.org/pypi/misaki/json`,
  `https://raw.githubusercontent.com/espeak-ng/espeak-ng/master/COPYING`. Nothing was downloaded or run.
- **Saved:** 2026-11-24.
- **Game build:** none (a third-party model; nothing here depends on a Metaphor: ReFantazio build).

## Model

- Licence: "This is an Apache-licensed model" (card text); metadata `apache-2.0`.
- Size: "an open-weight TTS model with 82 million parameters". The file size in bytes is [NEEDS VERIFICATION].
- Training data: "trained exclusively on permissive/non-copyrighted audio data and IPA phoneme labels"; excluded
  "synthetic audio from open TTS models or 'custom voice clones'"; "A few hundred hours of audio".
- Versions: v1.0 published 2025 Jan 27, SHA256 `496dba118d1a58f5f3db2efc88dbdc216e0483fc89fe6e47ee1f2c53f18ad1e4`
  (the card's hash for the v1.0 weights). The Hugging Face git commit is [NEEDS VERIFICATION].
- Languages and voices: v1.0 "8 & 54" (languages and voices). The card's usage and misaki's README cover English,
  Japanese, Korean, Chinese and Vietnamese; the full list of 8 is [NEEDS VERIFICATION].
- Generated audio: the card has no statement on who owns or may use output, beyond "We welcome the deployment of the
  model in real use cases."

## Voices

- The card states no licence for the voices. `VOICES.md` has a licence column only for some voices: `jf_gongitsune`,
  `jf_nezumi`, `jf_tebukuro`, `jm_kumo` and `ff_siwis` say "CC BY" (with a source link); `jf_alpha`'s cell is blank; every
  other voice, including the English ones, has no entry. The licence of the English voices is therefore
  [NEEDS VERIFICATION].

## Tool chain

- `kokoro` 0.9.4 on PyPI (uploaded 2025-04-05): Apache-2.0, needs Python >=3.10,<3.13, depends on `misaki[en]>=0.9.4`,
  `torch`, `transformers`, `numpy`, `huggingface-hub`, `loguru`.
- `misaki` (GitHub `hexgrad/misaki`, "Apache-2.0 license"; PyPI 0.9.4 Apache-2.0): the English extra pulls in
  `espeakng-loader` and `phonemizer-fork`. README: "To fallback to espeak" for out-of-vocabulary English; also used for
  some other languages. Licences of `espeakng-loader` and `phonemizer-fork` are [NEEDS VERIFICATION].
- espeak-ng's `COPYING` is the GNU General Public License v3 (29 June 2007). Its section 2 says "the output from running a
  covered work is covered by this License only if" the output itself constitutes a covered work; the text does not
  mention audio. espeak-ng is not in `requires_dist`; it is installed separately.
