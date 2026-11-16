# What the game's message functions saw in one play session (issue #55, step 2)

- **Source:** a capture of one run of `scripts/deploy-dev.ps1` (Debug mod at commit `484013d`) on the maintainer's
  machine, through Reloaded-II 1.31.0: the two development-only listening hooks (`MessageListeningHooks.cs`) wrote
  `message-log.txt` in the mod's folder. The log holds game text, so it stays local; only the short quotes below are
  kept.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `pwsh scripts/deploy-dev.ps1`. The loader installed both hooks: `CompileMsgFileListener` at module offset `0x9eaa70`
   and `GetMessageByLabelIndexListener` at `0x9e3b70`, the addresses in `mf-toolkit-message-signatures-2026-11-15.md`.
2. The maintainer started a new game and watched the first conversation. The first dialogue box showed speaker
   "Scheming Man" and the line quoted below.
3. The game was closed and the log read: 2,541,090 bytes, 60 `compile` records, 0 `lookup` records.

An earlier run of the same build crashed about 7 s after the mod loaded: Windows logged `APPCRASH` in `coreclr.dll`,
exception code `0x80131623`. The hook objects were no longer referenced after the loader returned, and both hooks
assigned their handle after activating it. Commit `484013d` keeps installed hooks referenced and stores the handle
first; the next run survived and produced this log.

## What it shows

- **`GetMessageByLabelIndex` is not how dialogue is displayed.** It was never called: not at the title screen, not
  while the event's lines were on screen.
- **`CompileMsgFile` runs when a message file is loaded, not when a line is shown.** 54 general files compile about
  8.4 s after the game starts (`EN/message/CharaName.msg`, `system/System_Message.msg`, `Item_Message_1.msg`, …);
  `system/Title_System.msg` at 43 s; event files on demand: `EN/message/event/e01_001_001.msg` at 60 s, and the
  watched scene's `EN/message/event/e00_002_001.msg` at 87 s and again at 257 s. Its fourth argument (MF.Toolkit's
  `id`) was 1–59, and `-1` for the first compile of that event file.
- **The source is text, as MF.Toolkit's test file suggested.** The watched line, in `e00_002_001.msg`:

  ```text
  #SPEAKER_ID "51"
  @MES_e00_002_001_000_00
  {
  <NS>More<NE>
  <VOICE "v_e00_002_001_000_00_c051">
  ...Who's there? I can feel you watching me from a place beyond my sight.
  <WAIT>
  }
  ```

- **The name on screen is not the `<NS>` text.** The box showed "Scheming Man". `EN/message/CharaName.msg` has
  `@CharaName_FO12_Mor` = `More` and `@CharaName_FO12_Mor_00` = `Scheming Man`. How the game picks the alias is
  `[NEEDS VERIFICATION]`.

## Tags in the logged source

Counted over all 60 files. Arguments follow the name, separated by spaces; the only closing tag is `</i>`.

| Tag | Count | Examples |
|---|---|---|
| `<WAIT>` | 16276 | |
| `<COLOR>` | 4884 | `<COLOR -1>`, `<COLOR 4>`, `<COLOR 0>` |
| `<NS>` / `<NE>` | 3634 each | around the speaker's name |
| `<VOICE>` | 3111 | `<VOICE "v_e00_002_001_000_00_c051">` |
| `<REPORT>` | 2008 | `<REPORT 0 0 0 0 0>` |
| `<BUSTUP>` | 1544 | |
| `<LIPSYNC>`, `<NOLIPSYNC>` | 445, 1 | |
| `<SKILL>`, `<ITEM>`, `<CONDITION>`, `<CLASS>`, `<ATTRIBUTE>` | 396, 234, 226, 72, 3 | `<ITEM 20637 2>` |
| `<FOLLOWER_ICON>`, `<SHOP_ICON>`, `<SELECT_ICON>`, `<PAD_ICON>`, `<MONEY_ICON>` | 201, 39, 10, 2, 2 | `<PAD_ICON 13>`, `<SELECT_ICON 3>` |
| `<V>` | 140 | `<V 0>` |
| `<ENEMY_NAME>`, `<CHAR_NAME>`, `<FOLLOWER_NAME>`, `<HERO_NAME>`, `<PLAYER_NAME>` | 112, 50, 30, 8, 2 | `<CHAR_NAME 65535>`, `<HERO_NAME>` |
| `<CHAR_PARAM>`, `<CHAR_PARAM_LEVEL>` | 16, 1 | |
| `<i>` … `</i>` | 26 | |
| `<year>`, `<ariya>` | 2, 1 | lowercase; meaning unknown |

`#SPEAKER_ID` appears 3687 times. What each tag does in the game is inferred from its name only `[NEEDS VERIFICATION]`.
