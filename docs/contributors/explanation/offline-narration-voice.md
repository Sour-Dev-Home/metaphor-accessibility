# An offline voice for cutscene narration clips

Use Kokoro-82M, run on the developer's machine at build time, to pre-render the narration clips, and ship only the audio
files, once its English voices' licences are confirmed; Piper is the fallback only if a voice with a clear licence is
found. The question is #961 (clips for cutscene descriptions); the languages question is #962. Sources:
[Kokoro](../../raw-sources/kokoro-licence-2026-11-24.md), [Piper](../../raw-sources/piper-licence-2026-11-24.md),
[Whisper](../../raw-sources/whisper-licence-2026-11-24.md). Where those say [NEEDS VERIFICATION], so does this page.

## Why Kokoro first

- **Model:** Apache-2.0, 82 million parameters, trained on data its card calls "permissive/non-copyrighted". That fits
  [ADR 0005](../../adr/0005-prism-speech-backend.md)'s rule for what ships, though the model itself would not ship.
- **Tool chain:** `kokoro` and `misaki` are Apache-2.0, pinnable on PyPI (0.9.4). The English fallback uses espeak-ng,
  which is GPL-3.0. It runs only in the developer's tool chain, and espeak-ng's licence covers output only if the output
  is itself a covered work; it doesn't say anything about audio. This is the reading of the text, not legal advice.
  The repository is GPL-3.0 anyway, so the tool chain is compatible even if the clips were affected.
- **Generated audio:** the Kokoro card sets no condition on output and welcomes real use.

## The gap that blocks a decision

The card gives no licence for the voices. `VOICES.md` marks five Japanese and French voices "CC BY" and lists nothing
for the English ones. Until a primary source states an English voice's terms, shipping its clips is a guess. CC BY
voices would need credit in `CREDITS.md`.

## Alternatives

- **Piper** (`piper1-gpl`, v1.8.0): engine GPL-3.0-or-later, embeds espeak-ng, 39 languages (a count to verify), the
  widest coverage for #962. Its voices carry no licence from the project; each has a `MODEL_CARD`, and some (the lessac
  base) exclude commercial use and some carry attribution. That is a per-voice check, and a restrictive one risks
  the "GPL-3.0-compatible" rule.
- **Whisper** (MIT code and weights, v20250625): not a voice. Run it locally to transcribe each clip and compare the
  names (characters, places) with the script, so a bad pronunciation is caught without listening to every clip. It
  mishears rare fantasy names, so a mismatch means "listen to this one", not "this is wrong".

## Decisions for the maintainer

1. Is a voice with unconfirmed terms acceptable to try, or must its licence be confirmed in a primary source first?
2. Do the clips count as shipped assets under ground rule 4 (our data), and does `CREDITS.md` name the model and voice?
3. Pin by Hugging Face commit and file SHA-256, and keep the model out of the repo and the zip?
4. Is Whisper a required check, or optional?
5. Does #962 (more languages) pick the engine? Kokoro covers few languages; Piper many.

## A by-ear test needs

Played clips need no screen reader; the listener needs headphones. Render the same 10 to 15
sentences from the real script with 3 or 4 Kokoro voices and 1 or 2 Piper voices, including the hardest names and a
long sentence. Compare for names, pacing and fatigue at 2 to 3 minutes. The setup is one command per engine, and none
of it is verified here.
