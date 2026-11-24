# The mod's player-facing text: what is ours and what is the game's

An inventory for #962, taken 2026-11-24 on `main` at `30ca116f`, by grep over `src/`, `installer/` and
`scripts/player/`. Counts are approximate. [ADR 0025](../../adr/0025-mod-text-in-string-tables-following-the-game-language.md)
proposes what to do with it.

Every line the mod says in its own words is an English literal or interpolated string beside the logic: there is no
strings class, `.resx` or other seam. Lines that are the game's own text already follow the game's language, because
the mod reads them from the compiled message files the game loads, by label, hash or serial, with the language folder
(`EN/message/...`) stripped (`MessageSerials.FromMessageFolder`).

## The mod's own English

| Where | About | Examples |
|---|---|---|
| Setting keys (`[SettingKey]` spoken names and descriptions) | about 100 keys | each feature enum; `docs/players/reference/settings.md` is generated from them |
| `Metaphor.Core` | 35 lines | cue names (`CueCatalogue.cs`), stand-ins for unresolved tags (`GameTerms.cs`: "item", "skill"), key and button names (`BindingNames.cs`) |
| `Metaphor.Features.Battle` | 14 lines | "Enemy turn" (`TurnLine.cs`), "weak to" and the other affinities (`EnemyAffinities.cs`), ordinals "First" to "Tenth" (`HitLines.cs`) |
| `Metaphor.Features.Dialogue` | 8 lines | "Choice, {n} options" (`DialogueAnnouncer.cs`), "Message log, line {i} of {n}" (`MessageLogFocus.cs`) |
| `Metaphor.Features.Field` | 45 lines | kind and direction words (`Nearby.cs`), "Guiding to {x}" (`Beacon.cs`), camera tilt lines (`TiltWarning.cs`) |
| `Metaphor.Features.Menus` | 60 lines | the mod's settings menu (`SettingsMenu.cs`), "Page {n} of {m}" (`TutorialPopups.cs`), screen labels (`new Screen("...")`), the welcome line |
| `Metaphor.Features.Updates` | 1 line | the update notice (`UpdateNotice.cs`) |
| Installer exe and `scripts/player/` | 50 lines | spoken and printed install messages |
| `Metaphor.Hooks` | none | its strings are log lines, which stay English |

English grammar is built into code in a few places: a plural (`Nearby.cs`, "step" or "steps"), ordinals (`HitLines.cs`),
word order ("{n} of {total}", "{name}, {n} steps {direction}") and lists joined with ", " and " and "
(`BattleRewards.cs`).

## Already the game's text

Message-window lines, speaker names (`CharaName.msg` through `CharaID.bin`), item, skill, class, enemy, quest, mission
and map names, battle banners, tutorial pages and most menu rows (read from the game's own resolve calls). The decoders
are in `src/Metaphor.Core/Messages/` (`MessageLibrary`, `MessageSerials`, `GameNames`, `GameTerms`) and fed by the hook
layer's message listeners.

## Mixed

Most lines a player hears in the field and in battle wrap a game name in mod English: "Guiding to {name}", "Mission:
{title}", "Gained {items}", "{name}, {n} steps {direction}". Translating these needs whole-sentence templates, not
translated fragments.

## Where the mod uses its own word because no game label is pinned

`SettingsPageRows.cs` (value names "On", "Off", "Auto"), the `GameTerms` stand-ins, "Door" and "Chest" in
`Nearby.KindWord`, "Studied" and "rank" in `ArchetypeTreeFocus.cs`, and the HP, MP and status wording. Each is a
candidate for a game label once one is found.

## What the mod knows of the language

Only the voice language (`VoiceLanguage`, read through `VoiceLanguageGetter`). The text language is not read; a global
for it is suggested, unconfirmed, in
[`voice-language-persisted-in-system-save.md`](../../findings/voice-language-persisted-in-system-save.md). Nothing reads
the Steam language.

## Tests

About 420 test assertions compare English text (`Metaphor.Core.Tests` 186, `Metaphor.Features.Menus.Tests` 121,
`Metaphor.Features.Field.Tests` 59, `Metaphor.Features.Battle.Tests` 30, others fewer), plus the Pester tests of the
install scripts. No snapshot files hold spoken text.
