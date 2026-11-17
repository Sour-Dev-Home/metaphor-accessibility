# The exe section each hook signature matches in

- **Source:** a scan of the game's `METAPHOR.exe` on disk, read as data (never run), with a throwaway Node.js v26.3.0
  script on the maintainer's machine. Signatures: `src/Metaphor.Core/hook-signatures.json` at `6e521b0`.
- **Saved:** 2026-11-17.
- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (`sha256sum` of the exe, run
  first).

## Steps

1. Read the PE header: `e_lfanew` at `0x3c`, the section count and optional-header size from the COFF header, then
   each 40-byte section entry's name, raw size and raw offset.
2. For each signature, find every file offset where its pattern matches (`??` matches any byte), and name the section
   whose raw range holds that offset.

## Result

Every signature matched exactly once. Only section names are recorded here.

| Signature | Section |
|---|---|
| `CompileMsgFile` | `.ecode` |
| `GetMessageByLabelIndex` | `.ecode` |
| `sq_newclosure` | `.ecode` |
| `sq_gettop` | `.ecode` |
| `sq_getstring` | `.text1` |
| `sq_getuserdata` | `.ecode` |
| `MsgRequestByName` | `.ecode` |
| `MsgRequestByNameTwin` | `.ecode` |
| `MsgRequestById` | `.ecode` |
| `MsgRequestByNameAuto` | `.ecode` |
| `MsgRequestByIdAuto` | `.ecode` |
| `MsgRequestSelectByName` | `.ecode` |
| `MsgSelectedId` | `.ecode` |
| `CursorManagerInput` | `.ecode` |
| `criAtomExPlayer_SetCueId` | `.text1` |
| `CreateMsgFromString` | `.text1` |
| `GetMessageBySerial` | `.ecode` |
| `MessageSerialByLabel` | `.ecode` |
| `MessageSerialByHash` | `.ecode` |
| `PadRead` | `.ecode` |
| `KeyboardRead` | `.ecode` |
| `MsgOpenBody` | `.text1` |
| `MsgCloseBody` | `.text1` |
| `TextPaneDraw` | `.ecode` |

`msg-window-functions-2026-11-16.md` calls `TextPaneDraw` a `.text1` hook; its signature's match is in `.ecode` on
this build. `sq_getstring` is only called (`SquirrelRegistrationListener`'s `Calls`), never hooked.
