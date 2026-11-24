# Every corner advice line goes through one `.ecode` function that names it by label or label hash

- **Game build:** `548ddc955c176867f062c44f94c03dd9ac33caefb278a5ac388e792aedf09504` (Steam build 18330018)
- **Method:** Opening the shipped executable in a disassembler as-is, accepting that protected parts may be unreadable;
  our own Reloaded-II mod reading the running game's memory and hooking its functions, as those community mods do
- **Sources:** [`squirrel-script-functions-all-2026-11-16.md`](../raw-sources/squirrel-script-functions-all-2026-11-16.md);
  otherwise none, see Steps
- **Functions:**
  - ReportMessageOpen | 0xf81cd0 | .ecode | Messages and Text | known only | Queues or shows one report (corner advice) line: `(component, key*, ?, int*, int*, int*)`, the key a label or its hash.
  - UUIC_Common_ReportMessage::vf13 | 0xf82bc0 | .ecode | Field | known only | The corner component's update: pops its queue of `0x28`-byte entries on a timer and shows the next through `.text1`.
  - ReportLabelToIndex | 0x9e81e0 | .ecode | Messages and Text | known only | Hashes a label key and looks up its message file id and label index.
  - ReportHashToIndex | 0x9e8250 | .ecode | Messages and Text | known only | Looks up a 16-byte label hash's message file id and label index.

For #587. **Found.** Gallica's top-left advice, and the same corner's lines from Junah and Heismay, are "report"
messages: entries of `message/report/*.msg` with a `<REPORT …>` tag, shown by `UUIC_Common_ReportMessage` and
`ReportMessageLayout`. All of them enter through `0xf81cd0` (named `ReportMessageOpen` here): every field report job
(on getting an item, on being found, time passing and ten more), the script functions `MsgReportOpenEx` and
`MsgReportOpenType`, and the battle report component. Its second argument points at a key: 8 bytes, then a kind byte;
kind 0 hashes the key as a label, kind 1 reads 16 bytes at it as the label's hash. The mod already holds every report
file's text from its compile observer, so a Release read-only hook at `0xf81cd0`'s entry can name the line and speak
it. The layout's animations run in `.text1`, which is why neither the `ui-anim` log nor `GetMessageBySerial` saw them.

## Evidence

### The corner lines are report messages

- **Text.** The 2026-11-23 19.22.38 log (the maintainer's local Debug log, mod `4d936b2`; grep only) compiles
  `report/Support_Field_GALICA.msg` at each field load (263409, 422221, 901667 ms and on). Its three entries
  (`SUP01_Field005_0001` to `_0003`) are Gallica's lines about appraisal, each with `<NS>Gallica<NE>`, a `<VOICE
  "v_co_…">` and `<REPORT 1 1 0 0 0>` or `<REPORT 1 0 0 0 0>`. Other report files compile beside it: `Field_Common.msg`,
  `Dungeon_Common.msg`, `Support_Battle_GALLICA.msg` at boot; the area's own (`D01_01.msg`, `Report_CTY01_KING.msg`,
  `T3N4_01.msg`) at each load.
- **Heismay's corner line.** The 2026-11-23 13.52.36 live notes put Heismay's top-left "We'll just have to…" at about
  3240000 ms. The same log's `report/T3N4_01.msg` (compiled at 3095562 ms) holds it: `@MES_T3N4_01_QUEST_097_talk_01_2`,
  `<NS>Heismay<NE>`, `<REPORT 1 0 0 0 0>`, "We'll just have to see for ourselves what awaits."
- **Not fetched by the hooks.** No report file's compile id appears in a `lookup` record, and no `resolve` serial lies
  in a report file (report files have no serial range in the message-file table,
  [`field-notices-and-map-travel-static.md`](field-notices-and-map-travel-static.md)).

### One function opens them

- **Callers of `0xf81cd0`** (Ghidra): `app::fld::fldReportJob::<kind>::vf3` for `Custom`, `OnBeingDamaged`,
  `OnBeingFound`, `OnBeingLost`, `OnBreakingMask`, `OnDamaging`, `OnFindingGenerator`, `OnFinishingBattle`,
  `OnGettingItem`, `OnProvoking`, `OnRecovering`, `OnStartingShingan` and `TimeElapsing`; `0x90c300` and `0x90c3a0`,
  the bodies of the script functions `MsgReportOpenEx` and `MsgReportOpenType` (their registered addresses, `.ecode`);
  `0xf1c590`, which passes its object's `+0x18` with a copy of the key (it sits among `UUIC_Battle_ReportMessage`'s
  slots, `0xf1c480`–`0xf1c570`, so it is most likely that component's open `[NEEDS VERIFICATION]`);
  `0xf827e0`; and one data reference from `.xcode`. `MsgReportOpen` (`0x90c290`) is a thunk into `.text1`, not
  followed. The report jobs and `MsgReportOpenEx` get the component from thunk `0x612350` (into `.text1`).
- **The key.** `0xf81cd0` reads the byte at key `+0x8`: 0 calls `0x9e81e0`, which turns the 8 bytes at key `+0x0` into
  a 16-byte value (`0x13d6e70`) and looks it up; 1 calls `0x9e8250`, which copies 16 bytes from the pointer at key
  `+0x0` and looks them up. Both look up in the message manager (`[0x2f04960]`, `0x9e60f0`) and return two ints, a
  message file id and a label index. At the `OnGettingItem` call site (`0x6f8b67`–`0x6f8bb0`) the kind byte is 1 and the
  pointer is an element of a 16-byte-stride array. That the 16 bytes are the MD5 of the label's name, as in
  [`objective-map-icon-points-static.md`](objective-map-icon-points-static.md)'s tables and the skill table
  ([`skill-cost-from-skill-tables.md`](skill-cost-from-skill-tables.md)), is `[NEEDS VERIFICATION]`. So is what kind 0's
  8 bytes are: a pointer to the label's name is the lead, since the script passes its argument there.
- **What it does with them.** It compares the file id with five ids (four from `0x9e8010` called with the constants
  `0x30`, `0x31`, `0x39` and `0x32`, one from a `.text1` thunk; most likely the report files' ids,
  `[NEEDS VERIFICATION]`) to set a type 0 to 5, then, with the component's state at `+0x18` equal to 2 (showing), hands the entry to `.text1`
  (`0x150193de0`'s thunk); otherwise it queues it with `0xf82920`. `UUIC_Common_ReportMessage::vf13` (`0xf82bc0`)
  later pops the queue (an MSVC deque at `+0x38`–`+0x50` of `0x28`-byte entries) after a timer and shows the next.
  So a line can be spoken when it is queued, shortly before it shows.
- **Signature.** The first 32 bytes at `0xf81cd0` (`48 89 5C 24 08 48 89 74 24 18 55 57 41 56 48 8B EC 48 83 EC 70 49
  8B F1 49 8B D8 48 8B C2 4C 8B F1`) occur once in `.ecode` on disk.
- **Why no layout record.** `ReportMessageLayout::vf20` (`0xd085a0`) starts its animations (`0x79d`, `0x7a1`, `0x7a5`,
  `0x7ad`, `0x7b1`) through thunk `0xf0052a0` and `0xa700a0`, never `0xa70010`.

## Steps

1. Ghidra 12.2 project of this build (`scripts/dev/ghidra.ps1 batch`): `vtable` of `UUIC_Common_ReportMessage`,
   `UUIC_Battle_ReportMessage`, `ReportMessageLayout`, `app::msg::Report`, `app::fld::fldReport`, `fldReportJob`;
   `decompile` of `0xf81b30`, `0xf82bc0`, `0xd085a0`, `0xd08ab0`, `0x90c290`, `0x90c300`, `0x90c3a0`, `0xf81cd0`,
   `0x9e81e0`, `0x9e8250`, `0x9f5f60`–`0x9f5fc0`, `0xf82920`, `0xf1c590`, `0xf827e0`; `callers 0xf81cd0`,
   `callers 0x612350`; `disasm 0x6f8bb0`.
2. Script function addresses: the saved list in Sources (`MsgReportOpen` to `MsgReportOpenTypeID`, `FldMsgReport*`).
3. The 32-byte prologue counted over the exe's `.ecode` raw data (PowerShell 7, read only).
4. Logs: `grep "compile id=.* path=EN/message/report"` and the compiled source after each record; the live notes'
   times for the Junah and Heismay lines.

## Build plan

1. **Signature.** `ReportMessageOpen` in `hook-signatures.json` with the prologue above (section `.ecode`), and the
   offline check's probe.
2. **Hook.** A Release, read-only `[GameHook(GameBuilds.Steam18330018)]` observer of the first pointer-sized arguments
   (component, key; the rest unread). It reads 16 bytes at the key: a pointer and the kind byte. Kind 1: read 16 bytes at
   the pointer, a hash. Kind 0: read a short C string at the pointer, a label `[NEEDS VERIFICATION]`. It reads nothing
   when no feature listens.
3. **Text.** A report library in `Metaphor.Core` keeps every compiled file under `message/report/` from the compile
   observer (`MessageFileCompiled`, as `MessageSerials` does for its files), keyed by label and by the label's MD5.
   It gives the speaker (`<NS>…<NE>`) and the cleaned text.
4. **Feature.** `cornerAdvice` (rule 18), on by default with a setting to turn it off, as the maintainer decided on
   2026-11-22: queue the speaker's name and the line, never interrupting. The voice still plays, so the line can follow the voice
   setting the way `voicedLines` does.
5. **Tests.** A fake key of each kind matches a fake compiled report file; an unknown hash says nothing. Debug log:
   kind, hash or label, matched label. By ear: walk a field until Gallica's corner advice shows, and on a quest
   with a party member's corner line; the log's matched label settles the MD5 and kind 0 questions on the first run.
