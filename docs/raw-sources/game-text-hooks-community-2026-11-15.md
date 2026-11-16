# Where game text flows, per community mods (Metaphor, plus Atlus prior art)

- **Source:** source files read through the GitHub REST API (`gh api`) and the Codeberg API/raw files, at the commits
  named below; GameBanana search API (`https://gamebanana.com/apiv11/Util/Search/Results?...`). Nothing downloaded,
  built or run. Builds on `modding-tools-survey-2026-11-13.md`.
- **Saved:** 2026-11-15
- **Game build:** none stated by any source below. Signatures are the mods' own; none was checked against our build
  (`548ddc95...` in `signature-spike-2026-11-15.md`) `[NEEDS VERIFICATION]`.

## MF.Toolkit (`RyoTune/MF.Toolkit`, commit `b1f876da5b4c1f42b5eef07fc6c2174030beb77b`, 2025-02-28)

`MF.Toolkit.Reloaded/Messages/MessageService.cs` declares these game functions (Reloaded-II hooks or wrappers, found by
signature scan):

```csharp
private delegate MSG* GetItemMsg(int* itemId, MsgFlag* msgFlags);
private delegate MSG* CreateMsgFromString(nint str, MsgFlag flags, MsgConfig1* config1, MsgConfig2* config2);
private delegate MSG* GetMessageByLabelIndex(int* msgId, int labelIdx, MsgFlag* flags, MsgConfigs* configs);
private delegate nint GetMsgBySerialId(int* param1, int* param2, nint param3);
private delegate nint CompileMsg(nint buffer, nint sourceStr, int length);
private delegate uint CompileMsgFile(nint msgSrc, int msgSrcLength, nint msgFile, int param4, byte param5);
```

| Name in source | Pattern | Use |
|---|---|---|
| `CreateMsgFromString` | `40 53 48 83 EC 30 41 80 79 ?? 00` | wrapper (called) |
| `GetItemNameMsg from FUN_140a545e0` | `48 83 EC 28 8B 01 FF C8`, then rel32 call at `+0x2d` | hook |
| `GetItemEffectMsg from FUN_140b3cc30` | `44 89 44 24 ?? 55 53 56 57 41 54 41 55 41 56 41 57 48 8B EC 48 83 EC 38 4C 8B F9`, rel32 at `+0x8e` | hook |
| `GetItemDescriptionMsg` | `48 89 5C 24 ?? 57 48 83 EC 20 48 89 CB 48 89 D7 8B 09 E8` | hook |
| `CompileMsgFile` | `48 89 5C 24 ?? 48 89 4C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC E0 00 00 00 45 8B E1` | hook |
| `GetMessageByLabelIndex` | `48 89 5C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 55 41 56 41 57 48 8B EC 48 81 EC 80 00 00 00 49 8B F1` | wrapper |

The `CompileMsgFile` hook reads the file path and the message **source text** before the game compiles it:

```csharp
var msgPath = Marshal.PtrToStringAnsi(msgFile)!;
var gameMessages = new GameMessageList(msgSrc, msgSrcLength);
...
var result = this.compileMsgFileHook!.OriginalFunction(newMsgSrc.Pointer, newMsgSrc.Length, msgFile, id, param5);
```

So per MF.Toolkit, Metaphor's messages arrive as text source compiled at load time, keyed by file path, which is where
it merges mod messages. `MSG` is an empty struct in `MF.Toolkit.Interfaces/Messages/MessageTypes.cs` (layout not
published); `MsgFlag` has comments such as `Flag4 = 1 << 3, // Used by names.`

MSG source syntax, from the test file `MF.Toolkit.Tests/MessageParser/SpeakIdTest.msg`:

```text
#SPEAKER_ID "FFFFFF"
@adsdfsadfsdfasdfsa
{
<NS>asdfsadfsa<NE>
<BUSTUP 0 0 0>
<VOICE "sadfsdfds">
dsafdsfds
<WAIT>
}
```

The meaning of each tag (`<NS>`/`<NE>` as name start/end, etc.) is not documented `[NEEDS VERIFICATION]`.

Other text-adjacent hooks: `Metaphor/Services/SpriteService.cs` (`SetSpriteSeq`, `PlaySpriteSeq(nint param1, nint
spriteStr, nint seqStr, ...)`), and `Squirrel/SquirrelPatterns.cs` with Squirrel API signatures (`sq_getstring`,
`sq_newclosure`, `sq_getuserdata`, ...). No hook on the function that **displays** a dialogue line or menu text was
found in MF.Toolkit.

## OpenGFD and riri-mod-tools (`rirurin`)

- OpenGFD (`rirurin/opengfd`, HEAD `167739405d2e1f0520980186b7ad6fbcf3f9e559`, 2026-03-19): file tree is graphics,
  effects, animation, kernel and sound modules. `opengfd/src/graphics/font.rs` exists but is empty at that commit. No
  message, dialogue or text module; a code search for `msg` found only `opengfd/src/kernel/init.rs`. The repo ships a
  `DEBUG_FONT.DDS` debug font under `opengfd-reloaded/data/modfiles/`.
- riri-mod-tools README (HEAD `a708e94f20323d298c3629f24997c523fb1cb15e`): its `PrintLog` example "hooks onto the
  `PrintLog` squirrel function defined in Metaphor waits until `sq_newclosure` is used to register `PrintLog`, where the
  hook is activated". `PrintLog` is a script logging function; nothing says it carries dialogue `[NEEDS VERIFICATION]`.
  The pattern itself (catch Squirrel native-function registration by name through `sq_newclosure`) is a way to find
  any named native the scripts call.

## MetaphorFix (`codeberg.org/Lyall/MetaphorFix`, commit `a74b056d76065f069953c27eb474d29bcdf017b0`)

`src/dllmain.cpp` has `Graphics`, `WindowManagement`, `IntroSkip`, `Resolution`, `AspectRatioFOV`, `HUD`, `Misc`. No
text, font or message hooks. `IntroSkip` documents the title-screen state machine read from `rax` at a mid-hook,
e.g. "0x30 = Atlus Logo", "0x3C = Opening Movie", "0x43 = Press Any Key" (full game).

## Other Metaphor mods

- `dreamastergames-collab/metaphor-greek-patcher`: a translation that "Reads `MetaphorGreek.cpk` (the translated text +
  fonts ...)" and appends it to `base.cpk`. File-level, no runtime hook.
- GameBanana (game 21201) searches for "speech", "accessibility", "tts", "blind", "narrat" returned 0 mods; GitHub
  repository searches for "metaphor screen reader" and "metaphor refantazio accessibility" returned nothing.

## Persona 5 Royal and other Atlus prior art

- No P5R accessibility, TTS or screen-reader mod found: GameBanana (P5R is game 8464) "speech", "accessibility", "tts",
  "blind", "narration" returned 0 mods; GitHub searches likewise empty.
- P5R text is BMD. FileEmulationFramework's BMD emulator (`Sewer56/FileEmulationFramework`, `docs/emulators/bmd.md`,
  HEAD `064c244`) merges `.msg` edits into `.bmd` files at load ("tested with: Persona 3 Portable (PC), Persona 4 Golden
  (PC), Persona 5 Royal (PC)"). File editing, not a display hook.
- `AnimatedSwine37/p5rpc.lib` (HEAD `6cb4bda`) lets mods call P5R flowscript functions; no dialogue display hook.
- Closest prior art: **Arcana** (`ChipsAhoiMcCoy/arcana`, Zlib, HEAD `d4f7c16`), a screen-reader mod for Persona 3
  Reload, which is Unreal Engine, not GFD. CONTRIBUTING: "`src/ue`: reads the game through Unreal Engine's own
  reflection." "Game text is read verbatim." "Never use a low-level keyboard hook. It breaks NVDA." Its README describes
  dialogue rules worth borrowing: voiced lines are not read by default (speaker name only), unvoiced lines always are,
  and a cue when a line waits for input. Its technique does not carry over to GFD.
