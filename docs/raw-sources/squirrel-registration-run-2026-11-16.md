# The game's script API, logged as it was registered (issue #55, step 2)

- **Source:** a capture of one run of `scripts/deploy-dev.ps1` (Debug mod at commit `484013d` plus the registration
  listener committed with this file) on the maintainer's machine, through Reloaded-II 1.31.0. Technique:
  `mf-toolkit-squirrel-registration-2026-11-16.md`.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

## Steps

1. `pwsh scripts/deploy-dev.ps1`. The loader installed three development-only hooks (`hooks 3/3`); the new one,
   `SquirrelRegistrationListener`, on `sq_newclosure` at module offset `0x13ca450`, calling `sq_gettop`,
   `sq_getstring` and `sq_getuserdata`, each found by its signature.
2. The game was left at its start-up screens for 60 s, untouched, then closed. It didn't crash.
3. The `register` lines of `message-log.txt` were counted and sorted.

## What it shows

- **MF.Toolkit's technique holds on this build.** 32,706 registrations between 3.5 s and 10.6 s after the game started:
  1,514 distinct names. 20 are Squirrel's own (`array`, `assert`, `print`, …), registered directly; the other 1,494
  are the game's, each with its native address inside a userdata value, as MF.Toolkit describes.
- **By prefix:** `Fld` 736, `Sortie` 92, `Msg` 61, `CMD_` 58, `Resrc` 41, `Seq` 31, `Quest` 27, `Event` 20. The `Msg`
  and `Event` families are listed in `squirrel-script-functions-2026-11-16.md`; the rest stay in the local log.
- **Dialogue has a script-facing family, `Msg…`** (61 functions, `0x90bd70`–`0x90ccd0`). By name:
  - showing a line: `MsgOpen` `0x90c1b0`, `MsgRequest` `0x90c420`, `MsgRequestID` `0x90c4f0`, `MsgRequestAuto`
    `0x90c4b0`, `MsgRequestNarration` `0x90c580`, `MsgRequestSubtitles` `0x90c820`, `MsgRequestSystemMessage`
    `0x90c8b0`;
  - reply choices: `MsgRequestSelect` `0x90c5d0` and its variants, `MsgGetSelectedID` `0x90bf80`, `MsgGetSelectNum`
    `0x90bf70`, `MsgIsSelectWindowOpened` `0x90c1a0`;
  - placeholders: `MsgSetVariable` `0x90ca60`, `MsgSetVariableINT` `0x90cc00`, `MsgSetVariableMapName` `0x90cc60`;
  - lifecycle: `MsgClose` `0x90be80`, `MsgCheckEnd` `0x90bdf0`, `MsgCanClose` `0x90bde0`.
- **Event scenes have control functions** (`EventStart` `0x905cd0`, `EventUIBegin` `0x905ce0`, `EventEnd` `0x905440`),
  and no text functions of their own by name.

## Not yet known

What each function takes and returns, which of them the opening event's script calls for a spoken line, and whether
the addresses are the functions themselves or thin wrappers (they sit `0x10`–`0x70` apart) are all
`[NEEDS VERIFICATION]`.

`sq_getstring` matched at `0x138da250`, about 300 MB from the other three Squirrel functions (`0x13c9cd0`–`0x13ca450`)
and near the CriFs functions whose patterns differ between the demo and the release
(`crifs-v2-hook-metaphor-patterns-2026-11-15.md`, `signature-spike-2026-11-15.md`). Whether that region is code the
protection moved is `[NEEDS VERIFICATION]`; the listener only calls the function, as MF.Toolkit does.
