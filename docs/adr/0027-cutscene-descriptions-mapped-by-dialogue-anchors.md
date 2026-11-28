---
status: proposed
---

# Cutscene descriptions anchor to scene ID and line index, mapped by dialogue alignment

Proposed for [#1013](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/1013) (#1014; builds #653 on #822,
idea credit to Arcana).

- **Anchors:** a scene ID and line index, which the mod already reads, and which survive video edits and languages.
- **Alignment:** a local tool extracts a script table (EVT/ECS,
  [event data](../findings/event-and-scene-data-static.md); movie `.msg`,
  [movie subtitles](../findings/anime-movie-subtitles-skip-dialogue-hooks.md); #1015) and aligns transcribed footage to
  it (#1016). Shots are observed in parallel, then written in story order, gated by a reference set (#1017, #1020).
- **Code owns structure:** IDs, timestamps and gap boundaries come from the script table and alignment, never from a
  model, which only fills text into slots; empty or malformed text is rejected.
- **Playback:** movies speak brief text in dialogue gaps and pause only for overflow
  ([movie pause](../findings/movie-pause-open-static.md); a mod-triggered pause is `[NEEDS VERIFICATION]`, #809).
  In-engine scenes show confirm boxes.
- **Data:** our text per anchor, brief and full, one file per language following the game's, as in
  [ADR 0025](0025-mod-text-in-string-tables-following-the-game-language.md); layout and triggers in #1018.
- **Never ships:** footage, frames, transcripts, drafts or game content (rule 4); capture never changes the game (rule 5).

## Considered options

- **Timestamps:** break on video edits and drift across languages.
- **Game data alone:** EVT timelines name animations by number only
  ([event data](../findings/event-and-scene-data-static.md)); what movies hold beyond subtitles is
  `[NEEDS VERIFICATION]`.

## Consequences

Silent or low-confidence scenes need manual review (#1016). The pause, voice licences (#961) and sound-effect tagging
(#1013) stay open.
