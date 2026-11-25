# A message line's voice clip plays through one function that takes the clip's name, and a flag marks it playing

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod's message log, which hooks the game's functions, as those community mods do
- **Sources:** none, see Steps; builds on [`voice-clip-families.md`](voice-clip-families.md),
  [`voice-language-persisted-in-system-save.md`](voice-language-persisted-in-system-save.md) and
  [`voice-language-values.md`](voice-language-values.md)
- **Functions:**
  - MsgVoiceCommand | 0x9fa010 | .ecode | Dialogue | known only | Finds a line's voice command, takes its clip name and plays it; sets `0x27b5930`. | finding
  - VoicePlayByName | 0x7c3a00 | .ecode | Dialogue | known only | Stops a playing voice, then plays the clip named by its `std::string` argument. | finding
  - VoicePlayCue | 0x7c3d00 | .ecode | Dialogue | known only | Starts the cue (CRI play at `0x1493f00`) and the lip-sync controller. | finding
  - LipSyncIsActive | 0x102e3e0 | .ecode | Dialogue | known only | Returns the byte at `+0x50` of the controller at `0x2ee89b0`. | finding

For #57. A line is tied to its clip by its `<VOICE "clip">` tag, which compiles to a command in the line; a line has a
clip exactly when it carries that tag (already the mod's rule, `VoiceClips`). When the line shows, `0x9fa010` finds the
command and calls `0x7c3a00` with the clip's name, which plays it and starts a lip-sync controller whose byte at
`base+0x2ee8a00` reads as "playing". So the mod can know at play time which clip started (a hook on `0x7c3a00`), and
poll that byte for the end. The voice language is the one the mod already reads (`VoiceLanguageGetter`, `0x2dff8e4`).
Static only; which value the byte holds during and after a clip is `[NEEDS VERIFICATION]`.

## Evidence

- **The session's voiced lines took this path.** In the 2026-11-22 22.50 session's message log, 13 cues have the call
  chain `0x7c4206>0x1493f00`, all on one archive (`acb=0x457206c0`); UI sounds come through `0x92e475` and layout
  sounds through `0xa49325`. Gallica's dungeon-start line (`MES_h_dng_start_00_001`, shown at 691135 ms) carries
  `<VOICE "v_co_11062_c011">` in its source, and its cue (id 3477) follows at 691223 ms on that path. `0x7c4206` is in
  `0x7c3d00`, whose one analysed caller is `0x7c3a00`.
- **`0x7c3a00` takes the name.** Its first argument is a `std::string` (it frees the buffer when the capacity is above
  15 and resets it to empty on the way out). It creates the `"LipSync"` timer once, stops the controller at
  `0x2ee89b0` first when `0x102e3e0` says it's active, and hands the string to `0x7c3d00`.
- **`0x9fa010` is the voice command.** Its argument holds the compiled line; it walks the line's 16-int command records
  for `0xd916`, `0xd818` and `0xd819`. On `0xd916` it builds the clip name into a string (`0x9fe410`), matches it against
  a table of name prefixes (`0x898e10`, a jump stub), stores the match's value at `0x27b5930` and calls `0x7c3a00`. On
  `0xd818` (unless a `0xd819` is present) it calls `0x7c3a00` with an empty name. With none of the three it leaves
  `0x27b5930` at -1. That `0xd916` is the compiled `<VOICE>` tag is inferred from the name it carries
  `[NEEDS VERIFICATION]`; `0xd818` may be lip-sync without a clip. Its analysed caller is `0x9eed40`, in the message
  manager's code.
- **The active flag.** `0x102e3e0(ctrl)` returns the byte at `ctrl+0x50`; `0x7c3a00` passes `0x2ee89b0`, so the byte is
  `base+0x2ee8a00`. `0x7c3d00` also sets `0x2ee89ac` from `0x102f870`, which returns the `u32` at `+0x54` when the byte
  at `+0x51` is set (a length or a time) `[NEEDS VERIFICATION]`.
- **Signatures.** Both prologues match once on disk (scratch probe file, not committed):
  `MsgVoiceCommand` `48 89 5C 24 20 48 89 4C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 D9 48 81 EC 90 00 00 00
  C7 05 ?? ?? ?? ?? FF FF FF FF B3 01` at rva `0x9fa010`; `VoicePlayByName` `4C 8B DC 49 89 5B 18 49 89 6B 20 49 89 4B
  08 56 57 41 56 48 81 EC 90 00 00 00 49 8B E8 48 8B DA 48 8B F1 45 33 F6 4C 39 35` at rva `0x7c3a00`.

## What this settles and what it doesn't

- **Which clip a line has:** its `<VOICE>` tag, as now. The session's named field lines carry `v_co_` clips (Gallica's
  above), which `voice-clip-families.md` measured as short generic barks (median 54 ms per word), so Auto reads them in
  full by design. Whether a given English `v_co_` clip speaks the whole line can't be read from the exe: the audio is
  encrypted (that note), so it stays a by-ear question.
- **The voice language:** settled before; Auto already gets it from `VoiceLanguageGetter`.
- **"Hold speech while a voiced line plays":** the start is `0x7c3a00` (or the dev-only cue log's `0x7c4206` chain);
  the end is a candidate, the byte at `base+0x2ee8a00`.

## Live check

Inspector batch, Debug build with `-OptIn inspector`, English voice, in the field next to a party member's voiced
remark (a dungeon start, as at 691135 ms):

```text
# #57: when a message line's voice clip plays and stops (docs/findings/message-voice-play-path-static.md).
# Game build 548ddc95... (Steam 18330018).
[before-line]
base
rtti base+0x2ee89b0
# The controller's playing byte (+0x50), its +0x51/+0x54, and the last line's voice-table value (-1 = no voice).
read base+0x2ee8a00 8
read base+0x27b5930 4
watch base+0x2ee8a00 1
watch base+0x27b5930 4
[while-voice-plays]
# Press Enter while the clip is still audible.
changes
read base+0x2ee8a00 8
[after-voice-ends]
# Press Enter a second after the clip ends, before advancing the line.
changes
[unvoiced-line]
# Press Enter on a line with no <VOICE> tag (an NPC without voice).
changes
read base+0x27b5930 4
```

Expected if the reading is right: `rtti` names a lip-sync class; the byte at `0x2ee8a00` is 1 while the clip plays and
0 after; `0x27b5930` is -1 after the unvoiced line. Also read the message log's `cue` lines with `0x7c4206` to match
each start.

## Steps

Ghidra 12.2 shared project (`scripts/dev/ghidra.ps1`), build above, 2026-11-23.

1. The 2026-11-22 22.50 session's message log (local, not committed): counted `cue` lines by archive and call chain;
   read the records around 691135 ms and the compiled source of `MES_h_dng_start_00_001`.
2. `decompile 0x7c4206` (lands in `0x7c3d00`), `callers 0x7c3d00`, `decompile 0x7c3a00`, `callers 0x7c3a00`,
   `decompile 0x9fa010`, `callers 0x9fa010`, `decompile 0x102e3e0`, `0x102e730`, `0x102e870`, `0x102f870`,
   `global 0x27b5930`.
3. `dotnet run --project tools/Metaphor.SignatureCheck -c Release -- --bytes <exe> 0x9fa010 48` and `0x7c3a00 48`, then
   `--probes <exe> <scratch file>` with the two patterns above: one match each.

Nothing in `.text1`, `.xcode` or `.xtext` was decompiled.
