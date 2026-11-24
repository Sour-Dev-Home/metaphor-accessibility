# OpenAI Whisper: licence, sizes and release, for checking narration pronunciation (#961)

- **Source:** read with WebFetch (a page-to-markdown tool, so quotes are as the tool returned them):
  `https://github.com/openai/whisper` and `https://api.github.com/repos/openai/whisper/releases/latest`. Nothing was
  downloaded or run.
- **Saved:** 2026-11-24.
- **Game build:** none (a third-party model; nothing here depends on a Metaphor: ReFantazio build).

## Licence and release

- "Whisper's code and model weights are released under the MIT License."
- Latest release tag `v20250625`, published 2025-06-26. Its commit hash and the weight files' hashes are
  [NEEDS VERIFICATION].

## Sizes (README table)

| Size | Parameters | English-only | Multilingual | Required VRAM |
|---|---|---|---|---|
| tiny | 39 M | `tiny.en` | `tiny` | ~1 GB |
| base | 74 M | `base.en` | `base` | ~1 GB |
| small | 244 M | `small.en` | `small` | ~2 GB |
| medium | 769 M | `medium.en` | `medium` | ~5 GB |
| large | 1550 M | n/a | `large` | ~10 GB |
| turbo | 809 M | n/a | `turbo` | ~6 GB |

- Languages: "See tokenizer.py for the list of all available languages"; the list itself is [NEEDS VERIFICATION].
- Training-data licence: not stated on the page; [NEEDS VERIFICATION]. Whisper is used here only to check clips on the
  developer's machine, so no Whisper output ships.
