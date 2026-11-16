# Spike session 1: choices, voice cues, menu focus and names (issues #82, #93, #103, #74, #106)

- **Source:** three runs on the maintainer's machine through Reloaded-II 1.31.0, Debug mod from branch
  `spike-prep-session-1` (dev listeners from `docs/raw-sources/msg-select-functions-2026-11-16.md`,
  `ryo-cri-atom-patterns-2026-11-16.md` and `name-lookup-candidates-2026-11-16.md`): the signature spike (Release),
  `scripts/listen.ps1 -Seconds 75` then about 15 min of play (part B), and a launch through Reloaded-II with the
  `.text1` opt-in and a test player name (part C). NVDA was off; with no screen reader running, Prism spoke through
  Windows OneCore (the mod's log: `speech: OneCore`). Logs hold game text, so they stay local; only labels, counts
  and timings are kept.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. Signature spike over the 23 probes in `tools/Metaphor.SignatureCheck/*-probes.json` (merged into one file).
2. Part B: `listen.ps1` (`hooks 14/14`, the opt-in hook skipped; the game survived 75 s). The maintainer arrowed through
   the title menu, started a new game, played the opening scene, moved between the choice's options and picked one,
   named the hero, and played to an early quest, then saved.
3. Part C: the two files from `docs/contributors/how-to/` (`opt-in-create-msg-from-string.txt`,
   `spike-player-name.txt` with a test name) placed in the mod's folder, the game launched through Reloaded-II
   (`hooks 15/15`), watched for a minute, the save loaded, the status screen opened, NPCs spoken to.

## Results

- **Signatures:** 22 of 23 match once on disk; `criAtomExPlayer_Start` matches 3 times (known, unhooked). In memory, 20
  match at the disk address; `CompileMsgFile` and `MsgRequestByName` match 0 times because this branch predates #105
  (the scan ran after the Release hooks patched them). #105 is still to be confirmed.
- **#82, choices: confirmed.** One `select` record per choice, `kind=0`, `first` = the prompt's label, `second` = the
  choice's label: `MES_e00_002_001_004` / `MES_e00_002_001_005` in the opening scene, and
  `MES_npc_0001_001_KING_47_001` / `…_00` in an NPC conversation. The prompts never went through `MsgRequestByName`,
  which is why the line before each choice was silent; the line after (`…_001_01`) was requested and spoken.
- **#93, selected ID:** the hooked function (`0x9fb150`) is polled about every 16 ms and returns -1 until a choice is
  made, then the chosen index (1 after the opening choice, 4 s after it opened), which it holds. It is the choice's
  result, not live focus.
- **#93, sound cues:** `criAtomExPlayer_SetCueId` logged no cue during the title screen and menu (none before 518 s);
  title music and menu sounds take another route `[NEEDS VERIFICATION]`. During dialogue, one cue sheet (handle
  `0x4a027020`) played cue 6 about 25 ms before each line request, and cues 0 then 1 while the choice was open (moving,
  then confirming, by timing only). The stack walk reported every caller as `(unknown)`, so the code that sets them
  wasn't traced.
- **#103, voice:** every voiced line in the opening scene got a cue on one sheet (handle `0x4e33c830`, ids 5, 6, 7, 8,
  then 12–18) within 1 ms after its `MsgRequestByName` request; the prompt `_004` got its cue (9) in the same
  millisecond as its `select`. So the line request comes first and the voice follows. The voice-language setting
  wasn't found.
- **#74, #106, names:** `MessageSerialByLabel` and `MessageSerialByHash` never fired; `GetMessageBySerial` fired 639
  times, mostly in minutes 8–10 (intro and new game), none at the first "alias" line. `CreateMsgFromString` (the
  `.text1` hook) installed and ran for the whole of part C with no crash or visible effect, but was never called, so
  no sighting of the test name. Neither question is answered. The hook stays opt-in; if the protection ever reacts
  to a detour in `.text1` (a crash or refusal to run), the hook is dropped rather than worked around (ground rule 5).
- **Dialogue routes:** part B requested 25 lines (`MES_e00_…`, `MES_D00_01_e0…`, `MES_ini_KING`), part C 4
  (`MES_npc_…`), all `kind=0` through `MsgRequestByName`. **Tutorial pop-ups** were shown and not spoken; no request
  of any kind was logged for them, so they take another route.
