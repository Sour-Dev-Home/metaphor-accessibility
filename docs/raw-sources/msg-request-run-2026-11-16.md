# Which function a spoken event line goes through (issue #55, step 2)

- **Source:** a capture of one play session through `scripts/deploy-dev.ps1` (Debug mod at commit `65325c4`), on the
  maintainer's machine, through Reloaded-II 1.31.0. Hooks: `src/Metaphor.Hooks/MsgRequestListeners.cs`, on the five
  functions in `msg-request-functions-2026-11-16.md`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `pwsh scripts/deploy-dev.ps1`: `hooks 8/8`.
2. The maintainer started a new game and played the opening scene past the first two lines from "Scheming Man", then
   the game was closed.
3. The `request` and `compile` records of the local `message-log.txt` were read. Game text stays in the local log.

## Results

```text
72146 ms compile id=-1 path=EN/message/event/e00_002_001.msg chars=3782
79183 ms request via=MsgRequestByName kind=0 text="MES_e00_002_001_000_00" options=0000000000000000+057f1700
80364 ms request via=MsgRequestByName kind=0 text="MES_e00_002_001_000_01" options=0000000000000000+057f1700
83615 ms request via=MsgRequestByName kind=0 text="MES_e00_002_001_000_02" options=0000000000000000+057f1700
93117 ms request via=MsgRequestByName kind=0 text="MES_e00_002_001_000_03" options=0000000000000000+057f1700
94267 ms request via=MsgRequestByName kind=0 text="MES_e00_002_001_000" options=0000000000000000+057f1700
```

- **The spoken lines went through `MsgRequestByName` (`0x9f8240`), one call per line, kind 0.** None of the other four
  hooked functions was called, and `GetMessageByLabelIndex` wasn't either.
- **Its pointer argument is a C string: the message's label** in the scene's message file, without the `@`. The first,
  `MES_e00_002_001_000_00`, is the line the maintainer heard first (speaker `<NS>More<NE>`, with a `<VOICE>` tag and
  `<WAIT>`), in `EN/message/event/e00_002_001.msg`, which the game compiled 7 s earlier.
- Calls follow the player's pace (1–10 s apart), so the call comes when a line is shown, not when the scene loads.
  That it comes before the line is drawn, not after, is `[NEEDS VERIFICATION]`.
- The options block was the same each time; the low 8 bytes zero. What the high 4 bytes are is
  `[NEEDS VERIFICATION]`.
- The label alone doesn't carry the text: the text is in the compiled message file, which `CompileMsgFile` sees first.
- The last call's label has no `_NN` suffix; what it shows is `[NEEDS VERIFICATION]`.
- How "Scheming Man" is chosen for `More` is still open.
