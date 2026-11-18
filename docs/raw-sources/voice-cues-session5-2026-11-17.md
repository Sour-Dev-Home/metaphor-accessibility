# Capture session 5: voice cues, voice volume and the voice language (issue #103)

- **Source:** capture evening #229, session 5, on the maintainer's machine: the Debug mod from branch `capture-1` at
  `219282c`, launched through Reloaded-II with `opt-in-voice-volume.txt`, `opt-in-voice-cues.txt` and
  `opt-in-voice-banks.txt` (`hooks 31/31`, none skipped), Steam Input on, OBS recording with narration (video time = log
  time − 51 s). `message-log.txt` (35 minutes, 1.18 million lines) and the narration hold game text, so they stay local;
  only record kinds, counts, cue numbers, player handles and times are kept here, counted with `grep` and `awk`. Record
  formats: `src/Metaphor.Core/MessageLog.cs` and `src/Metaphor.Core/Messages/VoiceCueCheck.cs` on `capture-1`. The
  listeners: `docs/raw-sources/cri-voice-probes-2026-11-17.md` (on `capture-1`).
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## What the run did

The maintainer moved the voice slider in Settings, set it to 0, then back to maximum (video 14:08 to 14:40, log 899 s
to 931 s), and talked through voiced and unvoiced lines in several conversations. The voice language was not switched:
the whole run was in English voice.

## Results

### 1. Tagged lines start a voice cue; untagged lines don't

The run logged 87 `request` records (80 from `MsgRequestByName`, 7 from its twin) and 80 `voice-check` records (one
per checked line; window 1,000 ms after the request), and 190
`cue` records on one player, `0xf648030`, all from the same cue sheet (`acb=0x49226700`) and with cue numbers in the
thousands (other players' cues are small numbers: menu and field sounds). Calling that player the voice player:

| Line | Lines | Voice-player cue within 1,000 ms | None |
|---|---|---|---|
| `<VOICE>`-tagged | 45 | 44 | 1 |
| Untagged (kind 0) | 33 | 0 | 33 |
| Untagged (kind 1) | 2 | 0 | 2 |

The one tagged line without a cue in its window (request at log 859.856 s, video 13:29) got its voice-player cue at
861.894 s, 2,038 ms after the request: late, not missing. So every tagged line started a voice cue, and no untagged
line did. Every untagged line's check still lists cues, but only on other players (sound effects), so a "voiced" test
must name the voice player or its cue sheet, not "any cue".

### 2. The request comes first

The 44 cues in the window came 0 to 258 ms after `MsgRequestByName` (median 102 ms; 14 within 1 ms). The dialogue
window flag (`docs/raw-sources/dialogue-window-flag-session5-2026-11-17.md`) shows the window at the next pad read, 5
to 17 ms after the request. So the request runs before the line is drawn and before its voice starts.

### 3. Voice volume: the three CRI listeners logged nothing

No `category-volume`, `player-volume` or `player-category` record appears in the whole run: not at start-up, not while
the slider moved, not when it was set to 0 or back. Every cue's `cat=` is `?` for the same reason. So in this build the
game doesn't apply its voice-volume option through `criAtomExCategory_SetVolumeById`, `criAtomExPlayer_SetVolume` or
`criAtomExPlayer_SetCategoryById` at the addresses Ryo's patterns found, or those addresses aren't those functions.
Which call it uses (by-name category volume, AISAC, a bus volume, or the game's own mixer) is `[NEEDS VERIFICATION]`.

### 4. Voice language: the AWB paths name it

134 `acb-load` records; their AWB paths start with a language folder or a common one:

| Path prefix | Loads |
|---|---|
| `COMMON/sound/…` | 95 |
| `EN/sound/battle/character/…` | 36 |
| `EN/sound/event/…` | 2 |
| `EN/sound/` (one bank at its root, 9.1 s after start, 1.77 MB of ACB data) | 1 |

Character voices (battle and event) load from `EN/`, effects from `COMMON/`. That the folder is `JP/` (or another
name) with Japanese voice selected is `[NEEDS VERIFICATION]`: the language wasn't switched. That the root `EN/sound/`
bank is the voice player's cue sheet `0x49226700` is likely but not shown, since `acb-load` doesn't log the handle it
returns.

## Left for #103

- A run with Japanese voice selected, to see the `acb-load` prefix change (a second launch, as the language may only
  apply on restart).
- Where the voice-volume option is applied: a new lead, since the three CRI setters never ran.
- How the mod reads the language at run time: the path prefix seen by an `acb-load` listener is one read-only route;
  a settings file or memory value is still to find.
