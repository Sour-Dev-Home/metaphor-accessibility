# Where the game picks a speaker's name and the player's name: candidates (issues #74, #106)

- **Source:** our own reading of the game executable on disk, as shipped (ADR 0002), without running the game:
  `tools/Metaphor.SignatureCheck --bytes` and `--probes` (this repository at the commit that adds this file), and a
  throwaway local disassembler over the same bytes (Iced 1.21.0, the NuGet package, in a scratch project outside the
  repository; nothing was patched, unpacked or run). Script-function addresses come from
  `squirrel-script-functions-all-2026-11-16.md`. Community sources: `RyoTune/MF.Toolkit`
  `MF.Toolkit.Reloaded/Messages/MessageService.cs` at `b1f876da5b4c1f42b5eef07fc6c2174030beb77b` and at
  `8e50b9372f5f0728687cba39c51d3481085b48ce` (2024-10-02, AGPL-3.0), and `DeathChaos25/metaphor-010-templates` at
  `cfccaa2f4e4352d2881884b1c7d61c53661259d3` (`metaphor_enums.bt`), read through the GitHub API.
- **Saved:** 2026-11-16.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018).

All addresses are relative to the game module; names in backticks without a source are ours, not the game's. Nothing
here was observed in a running game: every behaviour below is read from code and is `[NEEDS VERIFICATION]` until the
listening run in [the session plan](#what-the-session-needs).

## Short answer, to be checked in the game

1. **A speaker's displayed name is probably picked from a table by number and game flags (#74).** A function reached
   through `0x9e7ca0` (its code is at `0xa6529e0`, in `.text1`) takes a number up to 100, finds that number's 0x6C-byte
   entry in a data table, and walks five 0x12-byte slots: each slot's 16-bit word at +0x10 is a flag number (0xFFFF for
   none), tested with the same function the script function `BitGet` calls (`0x3b0ab0`). The first slot whose flag is
   set wins; with none set, the entry's own first 16 bytes are used. The 16 bytes are a message key (below), which
   `0x9e8e60` turns into a message serial number. So "Scheming Man" over "More" would be one slot's key chosen by a
   story flag. Which way round (flag set means alias, or revealed name) is open.
2. **Messages are found by the MD5 of their label.** `0x9e8e00` takes a C string, measures it and starts an MD5
   (`67452301 EFCDAB89 98BADCFE 10325476`) at `0x13d6e70`, then looks the 16 bytes up. So the 16-byte keys above are
   probably `MD5(label)`, and a key in the log can be matched to a `CharaName.msg` label offline.
3. **65535 is "no number" (#106 question 2).** The one caller of `0x9e7ca0` read so far, `0x679880`, gets a number from
   an object, compares it with `0xFFFF`, and on equality falls back to a second table (`0x9e8820`, numbers up to
   `0x99d`). The `<CHAR_NAME n>` handler itself was not found; that `65535` there means the same is an inference.
4. **The player's name (#106 question 1)** wasn't found statically. No script function returns it. The listener on
   `CreateMsgFromString` reports whether the typed name passes through it, and where it was held.

## Message tags and their codes

The message compiler builds a table of tag names and 16-bit codes at `0x9ccb00`–`0x9ccde0` (`.ecode`), from strings at
`0x2121b30`; the same codes are listed as dwords from `0x2121a60`. Read from the construction code: `NS` `0xD803`, `NE`
`0xD804`, `HERO_NAME` `0xD845`, `PLAYER_NAME` `0xD865`, `CHAR_NAME` `0xD946`, `CLASS` `0xDA47`. By the order of the
two lists, `FOLLOWER_NAME` is `0xD94E` `[NEEDS VERIFICATION]`. The high byte matches the argument count in the logged
source (`0xD8` none, `0xD9` one, `0xDA` two), which is our reading, not a documented rule.

A token walker at about `0x1478480` reads codes with `0x142cf60` and skips from `0xD803` to `0xD804` (the speaker
block); codes it doesn't special-case go through a resolver object whose table is at `0x2122630`. That resolver was
not followed further.

## Message files and serial numbers

The game's message-file table starts at `0x27b0cd0`: 0xD0-byte entries, `0x5e` of them. Entry 0 points at the string
`message/CharaName.msg` (`0x2122ad8`, length 0x15) and at `0x9f4a70`, which is `cmp ecx, 79h; setbe al; ret`: probably
"is this serial number one of CharaName's", so CharaName would own serials 0–121 `[NEEDS VERIFICATION]`. The entries
after it have similar range tests (`0x12f5`+`0x121`, `0x1417`+`0x304`, …).

| Our name | Address | Section | What it does, as read |
|---|---|---|---|
| `MessageSerialOfFile` | `0x9e8dd0` | `.ecode` | `(int* file, int label)`: the file's first serial plus the label index |
| `MessageSerialByLabel` | `0x9e8e00` | `.ecode` | `(char* label)`: MD5 of the label, looked up; serial or -1 |
| `MessageSerialByHash` | `0x9e8e60` | `.ecode` | `(byte* key)`: copies 16 bytes, looks them up; serial or -1 |
| `GetMessageBySerial` | `0x9e3d30` | `.ecode` | `(int* serial, flags*, configs*)`: reads `[rcx]`, finds the file that owns it, returns a message |
| `CreateMsgFromString` | `0x13c9f6d0` | `.text1` | MF.Toolkit's: `(char* str, flags, config1*, config2*)`, calls a manager's virtual function |

`GetMessageBySerial` is MF.Toolkit's `GetMsgBySerialId` (commit `8e50b937`: pattern ending `49 8B F0`, delegate
`nint (int*, uint*, nint)`), dropped from later commits; its pointer argument is read as an `int` at `0xa7644b8`
(`mov eax, [rsi]`). It has 253 direct callers. `CreateMsgFromString` has one direct caller, a jump at `0x1422b50`, which
16 call sites use.

## Script functions considered

From `squirrel-script-functions-all-2026-11-16.md`; none returns a name.

| Function | Address | Why it's a lead |
|---|---|---|
| `BitGet` / `BitOn` / `BitOff` | `0x9038e0` / `0x903910` / `0x903900` | `BitGet` jumps to `0xa00e3b0`, which calls `0x3b0ab0`: a bit test in an array at `[[0x272e310]+0x22c5c]`. The name table's slots use the same test. |
| `FclPlayerNameEntryStart` / `IsEnd` | `0x9063f0` / `0x9063d0` | The name entry screen. Start jumps into `.text1` (`0xa03cc00`) and passes the string `isPlayerNameEntry` (`0x17eac48`); where the result is stored isn't readable there. |
| `MsgSetVariable`, `MsgSetVariableINT` | `0x90ca60`, `0x90cc00` | Fill message variables; not decoded. |
| `FldMoringGetSpeakerID` | `0x8e21b0` | The only script function naming a speaker; not decoded. |

Strings `GAME_PLAYER_NAME_ENTRY` and `evt_IF_Command_GAME_PLAYER_NAME_ENTRY` (`0x17eef60`, `0x1813a60`) name an event
command for name entry, another lead for #106.

Community sources: MF.Toolkit has no name hooks; its item-name functions take an `int*` and return a message, the
same shape. The 010 templates' `FollowerID` and `CharacterID` enums (`metaphor_enums.bt` lines 255–274) give numbers
such as `More_Follower = 15` and `Protagonist = 1`; whether `<FOLLOWER_NAME n>` or `<CHAR_NAME n>` use them is open.

## Signatures

`tools/Metaphor.SignatureCheck/name-probes.json`, checked with `--probes` on this build: each matches once.

```text
ok   CreateMsgFromString: 1 match at 0x1334d2d0 (rva 0x13c9f6d0)
ok   GetMessageBySerial: 1 match at 0x9e3330 (rva 0x9e3d30)
ok   MessageSerialByLabel: 1 match at 0x9e8400 (rva 0x9e8e00)
ok   MessageSerialByHash: 1 match at 0x9e8460 (rva 0x9e8e60)
4 of 4 signatures matched exactly once
```

`CreateMsgFromString` was already checked in memory (`mf-toolkit-message-signatures-2026-11-15.md`); the other three
are on disk only. The name table function (`0xa6529e0`) has no signature: its code in `.text1` uses unusual encodings
(`31 F6` for `xor esi, esi`) and a constant built at run time (`xor esi, [0x74e22cc]`), so it was left for later.

## The listeners

`src/Metaphor.Hooks/NameLookupListeners.cs`, development builds only, through a new shape `IObserves4WithResult` that
calls the original first and then sees the result. Each writes to `message-log.txt`:

| Listener | Record | Example |
|---|---|---|
| `MessageSerialByLabel` | `resolve` with the label (only its length unless it is letters, digits and `_`) and serial; repeats within 2 s dropped | `resolve via=MessageSerialByLabel text="<label>" serial=12` |
| `MessageSerialByHash` | `resolve` with the 16-byte key in hex and serial; repeats within 2 s dropped | `resolve via=MessageSerialByHash hash=<32 hex> serial=12` |
| `GetMessageBySerial` | `resolve` with serial, flags, whether a message came back; repeats within 2 s dropped | `resolve via=GetMessageBySerial serial=12 flags=8 message=set` |
| `CreateMsgFromString` | `player-name` with byte length, match against the typed name, where the string was; never the text | `player-name via=CreateMsgFromString bytes=6 match=exact at=heap` |

`match` is `exact`, `contains`, `no`, or `unset` when the game process didn't get `METAPHOR_SPIKE_PLAYER_NAME`.

What would answer the issues:

- #74: while the opening line is on screen, a `MessageSerialByHash` record whose key is `MD5("CharaName_FO12_Mor_00")`,
  then `GetMessageBySerial` with the same serial. Later in the story, with More's name shown, the key of
  `CharaName_FO12_Mor`. If labels are resolved by text instead, `MessageSerialByLabel` shows them.
- #106: a `player-name` record with `match=exact` or `contains`, and `at=module+0x…` if the name is held in the
  module's data (a fixed place the mod could read) or `heap`.

To match keys to labels after the run, hash each label of `CharaName.msg` from the local `message-log.txt` (its
`compile` record lists the source) in PowerShell:
`[Convert]::ToHexString([Security.Cryptography.MD5]::HashData([Text.Encoding]::UTF8.GetBytes('<label>'))).ToLower()`.
That the key is the MD5 of the label alone, without the `@`, is `[NEEDS VERIFICATION]`.

## What the session needs

1. With the game and Reloaded-II closed: `pwsh scripts/signature-spike.ps1 -Probes tools/Metaphor.SignatureCheck/name-probes.json`,
   to check the three new signatures in memory.
2. In the same PowerShell window, set `$env:METAPHOR_SPIKE_PLAYER_NAME` to the hero name you will type (or the hero
   name of the save you load), then `pwsh scripts/listen.ps1`. `deploy-dev.ps1` starts Reloaded-II itself (it refuses
   to run while Reloaded-II is open); that Reloaded-II hands the variable on to the game is `[NEEDS VERIFICATION]`:
   `match=unset` in the log means it didn't.
3. Watch for a minute before playing: `CreateMsgFromString` is the first hook in `.text1`.
4. New game: watch the first conversation (the "Scheming Man" line), then go on to the name entry screen and type the
   name, and on to the first line that shows it. Note the times.
5. If you have one, load a save where More is shown by that name, and open a conversation with him.
6. Close the game and keep the log local; record the counts and the matching records in a raw source.
