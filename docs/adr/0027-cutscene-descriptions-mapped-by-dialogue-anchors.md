---
status: proposed
---

# Cutscene descriptions anchor to scene ID and line index, mapped by dialogue alignment

Proposed for [#1013](https://github.com/Sour-Dev-Home/metaphor-accessibility/issues/1013) (#1014; builds #653 on #822,
idea credit to Arcana).

Cutscene descriptions anchor to a scene ID and line index rather than timestamps, mapped offline from dialogue and
shipped as our own text.

- **Anchors:** each targets a scene ID and line index, matching what the mod reads while surviving video edits and
  language changes.
- **Alignment:** a local tool extracts a script table (EVT/ECS in
  [event data](../findings/event-and-scene-data-static.md), movie `.msg` in
  [movie subtitles](../findings/anime-movie-subtitles-skip-dialogue-hooks.md); #1015) and aligns transcribed footage to
  it (#1016). Drafting splits into parallel shot observation and sequential writing with a story bible, script names and
  code-enforced budgets, gated by a reference set (#1020, #1017).
- **Playback:** movies speak brief text in dialogue gaps, pausing via the movie player only for overflow
  ([movie pause](../findings/movie-pause-open-static.md); mod-triggered pause is `[NEEDS VERIFICATION]`, #809).
  In-engine scenes show confirm boxes. Settings cover brief/full levels, pause, hear-once and optional
  [narration voice](../contributors/explanation/offline-narration-voice.md).
- **Data format:** `descriptions/en/scenes.json` (one file per language), keyed by scene ID and line-index anchor, with
  brief and full text (#1018).
- **What never ships:** footage, game content, frames, transcripts and drafts stay private (rule 4); capture never
  modifies the game (rule 5). Only our text ships.

## Considered options

- **Timestamps:** break on video edits and drift across languages.
- **Game data alone:** EVT IDs lack pose meanings; `.usm` movies lack timelines.

## Consequences

Silent or low-confidence scenes need manual review (#1016). Mod-triggered pause (`[NEEDS VERIFICATION]`), voice
licences (#961) and sound-effect tagging (#1013) remain open.
