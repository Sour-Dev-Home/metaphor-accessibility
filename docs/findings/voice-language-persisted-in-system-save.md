# The voice language is kept in `system.sav`, not `pcconfig.dat`; the mod reads it in memory

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Reading the game's own files offline (no ADR 0002 row covers data files, which no protection guards);
  and opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable
- **Sources:** none, see Steps; builds on [`voice-language-values.md`](voice-language-values.md),
  [`title-names-voice-tutorials-static.md`](title-names-voice-tutorials-static.md) (section 4) and
  [`voice-language-getter-2026-11-20.md`](../raw-sources/voice-language-getter-2026-11-20.md)
- **Functions:**
  - AppChangeVoiceLanguage | 0x7bb010 | .ecode | Settings | known only | Stores the new voice language, then applies it. | finding
  - VoiceLanguageSetter | 0x7bc6d0 | .ecode | Settings | known only | Writes the voice language as set. | finding
  - VoiceLanguageGetter | 0x3dc620 | .ecode | Settings | read | Returns the applied voice language, `base+0x2dff8e4`. | finding

For #103 question 3 and #57. The answer for the mod is the one #538 already ships: read the applied voice language in
memory (`base+0x2dff8e4`, 1 Japanese, 2 English) through `VoiceLanguageGetter`, after the build check
(`src/Metaphor.Hooks/VoiceLanguageRead.cs`). It changes the moment the setting does, so Auto mode can say only the name
with English voice and read the full line with Japanese voice. On disk the setting survives a relaunch, and it isn't in
`pcconfig.dat`: it's most likely one of `system.sav`'s twelve records `[NEEDS VERIFICATION]`, which one isn't known.
Reading the file would add nothing the memory read lacks.

## Evidence

- **Save folder.** `%APPDATA%\SEGA\METAPHOR\Steam\<Steam ID>\` holds `pcconfig.dat` (2,850 bytes), `system.sav`
  (160 bytes), `save0000.sav` to `save0016.sav` and `steam_autocloud.vdf`. The exe holds the names `save%04d.sav`,
  `system.sav` and `save_demo%04d.sav` together in `.udata` (rva `0x2262420`), and `pcconfig.dat` apart (rva
  `0x181e7a8`, read by `0x7e7130` and `0x7e7f30`). `global` finds no analysed instruction that loads the `.sav` names.
- **`pcconfig.dat` is plain text**: a first line of three numbers, then 117 `KEY=value` lines. 24 are `PC_` display,
  volume, camera and control options and the other 93 are key bindings (`OPEN_MENU=8,0,9`). No key contains `LANG`
  or `VOICE`. So the voice setting, which `ConfigPcConfigMisc` sets (section 4 of the static note), is
  not stored with the other PC options.
- **`system.sav` is records, not encrypted.** A `u32` 0x16 (22, the same first word every `saveNNNN.sav` starts
  with), then twelve records of `u16 id, u16 0x10, u32 size, value`, ids 0 to 11, then 13 trailing bytes. Ids 6, 7
  and 8 hold 2 and id 9 holds 1 in today's file. With voice 2 = English and the text language, likely `0x27a8c74`,
  also 2, the voice is one of ids 6 to 8 `[NEEDS VERIFICATION]`: one file can't tell which, and the setting at the time
  of the last write isn't recorded.
- **Every write in analysed code.** `global 0x27a8c78` has one write (`0x7bc6d0`) and one read (`0x7bbc40`).
  `0x7bc6d0` is called from `AppChangeVoiceLanguage` (`0x7bb010`) and the settings page (`0x807460`).
  `AppChangeVoiceLanguage`'s callers are `0x7331d0`, `0x7338a0`, `0x7da480` and `0x7db320`. Each passes a voice value
  from `0x3dc620` or `0x7bbc40` with a callback typed `void(app::AppLang)` (its RTTI name), so the game's type for the
  value is `app::AppLang`. `0x7331d0` re-applies the applied value once, guarded by a flag at `+0x165`. No analysed
  function copies a value from a loaded file into `0x27a8c78`, so the load at startup goes through protected or
  unanalysed code `[NEEDS VERIFICATION]`.
- **The settings block.** `0x3dcc20` fills `0x2dff8b0`: `+0x30` (`0x2dff8e0`) and `+0x38` from `0x7bbc30` (text
  language), `+0x34` (`0x2dff8e4`) and `+0x3c` from `0x7bbc40` (voice). Its other fields come from a store at
  `[[0x2ca6638]+0x18]+8`, by ids 0x11 to 0x27. Whether that store is `system.sav`'s contents isn't known.

The user's own save files were read in place, read-only, and not copied. No byte of them is in this note beyond
record ids and small values; the Steam ID is left out.

## Steps

Windows 11 x64, PowerShell 7, 2026-11-22. The game wasn't run.

1. SHA-256 of `METAPHOR.exe` (from `Find-MetaphorGame`): the build above.
2. List `%APPDATA%\SEGA\METAPHOR\Steam\*\`. Hex-dump `system.sav` (`Format-Hex`); read `pcconfig.dat` as ASCII and split
   on newlines.
3. `grep -aob` over the exe for `save%04d.sav`, `system.sav` and `pcconfig.dat`; convert each file offset to an rva with
   the PE section table (`.udata` raw `0x17bee00`, virtual `0x17c0000`).
4. Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`): `global 0x2262420`, `0x2262430`, `0x181e7a8`, `0x27a8c78`;
   `callers 0x7bc6d0`, `0x7bb010`, `0x7bbc40`; `decompile 0x3dcc20`, `0x7331d0`, `0x7da480`, `0x7db320`. Nothing in
   `.text1` was followed.

## What would settle it

An inspector or file check, no listening needed:

1. Before launching, copy `system.sav` aside by hand. In game, switch the voice language (Settings, the misc page)
   and back out so the game writes its system data. Quit and copy it again.
2. Compare the two: the record that went from 2 to 1 (English to Japanese) is the voice; one that didn't move is not.
   Repeat with the text language, if the game has one, to pin `0x27a8c74`.
3. For #103 question 1, still open from `voice-language-values.md`: play one `<VOICE>` line with each setting and check
   that the voice player's cue changes with the language.
