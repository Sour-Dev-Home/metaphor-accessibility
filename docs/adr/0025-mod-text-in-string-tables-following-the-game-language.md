---
status: proposed
---

# The mod's own words live in one string table per language, picked by the game's text language

Proposed for #962 (2026-11-24); nothing beyond English is built until the maintainer accepts it.

The mod speaks the game's own text wherever the game has a word for it, and keeps its own words in string tables, one
per language, chosen by the language the game shows its text in. The
[inventory](../contributors/reference/mod-strings-inventory.md) found about 220 of the mod's own lines and 100 setting
descriptions, all English literals in code, and that the game's own text already follows its language.

- **Game words first.** A line that names something the game names (an item, a place, "Chest", "On") takes the game's
  message by label, as the game's text does today. A mod word is only for what the game never says.
- **Tables:** one JSON file per language, `src/Metaphor.Core/Strings/<language>.json`, embedded in the assembly: a
  flat map from a stable key (`field.guidingTo`) to a whole sentence with named placeholders (`"Guiding to {name}"`).
  English is the source and the fallback for a missing key. No new dependency and no satellite assemblies, which
  Reloaded-II's load context would have to find (`[NEEDS VERIFICATION]` if `.resx` is reconsidered).
- **Grammar** stays in the table: whole sentences, never fragments joined in code; a plural is a key per CLDR plural
  category (`one`, `other`, and `few` or `many` where a language has them), picked by a small rule per language;
  ordinals and lists are keys too.
- **Language:** the game's text language, read in memory like the voice language (`VoiceLanguageGetter`). Its global
  and values are `[NEEDS VERIFICATION]`; the suggested `0x27a8c74`
  ([finding](../findings/voice-language-persisted-in-system-save.md)) needs a static pin and a run in a second language.
  An unknown value falls back to English. A setting may override it.
- **Checks:** a test fails when a language's table lacks an English key's placeholders, has keys English doesn't, or a
  source file speaks a literal outside the table (after the move). Tests keep asserting English by fixing the language.
- **Out of scope for now:** log lines (stay English), the installer and `scripts/player/` (later, by Windows display
  language), player docs.

## Considered options

- **`.resx` and `ResourceManager`:** .NET's standard, culture fallback built in, but satellite assemblies under
  Reloaded-II and a binary-ish format translators edit through tools.
- **gettext `.po`:** the best translator tooling, but needs a parser dependency (ground rule 3, ADR 0005's pinning).
- **Machine translation at run time:** no; it needs a network (architecture rule 3) or a large model.

## Consequences

About 300 call sites move to keys, feature by feature, each with its tests; until then English literals and table keys
live side by side. The settings reference is generated from the English table. Each new language is a table plus a
by-ear pass by a speaker of it. Which languages the game ships, and their folder names beside `EN`, are
`[NEEDS VERIFICATION]` from an install's archive listing.

Open decisions for the maintainer: JSON or `.po`; the first second language; who translates (players by PR,
machine-drafted then checked, or both); whether a setting can override the game's language.
